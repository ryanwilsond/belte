using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Shared;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed partial class ILEmitter : ModuleBuilder {
    internal readonly static Lock GlobalCecilLock = new();
    internal readonly static ConcurrentSet<TypeReference> Imports = [];

    private readonly MethodReference _belteCompilerGeneratedAttributeCtor;
    private readonly TypeReference _belteCompilerGeneratedAttribute;

    private readonly BelteDiagnosticQueue _diagnostics;
    private readonly AssemblyDefinition _assemblyDefinition;
    private readonly List<AssemblyDefinition> _assemblies;
    private readonly List<AssemblyDefinition> _backupAssemblies;
    private readonly BoundProgram _program;
    private readonly ImmutableArray<NamedTypeSymbol> _topLevelTypes;
    private readonly ImmutableArray<NamedTypeSymbol> _linearNestedTypes;
    private readonly ImmutableArray<(MethodSymbol, BoundBlockStatement)> _methodBodies;
    private readonly bool _isDll;
    private readonly bool _debugMode;

    private readonly Dictionary<SpecialType, TypeReference> _specialTypes = [];
    private readonly Dictionary<TypeSymbol, TypeDefinition> _types = [];
    private readonly ConcurrentDictionary<MethodSymbol, MethodDefinition> _methods = [];
    private readonly ConcurrentDictionary<MethodDefinition, (MethodSymbol, BoundBlockStatement)> _methodBodyMap = [];
    private readonly ConcurrentDictionary<FieldSymbol, FieldDefinition> _fields = [];
    private readonly ConcurrentDictionary<MethodSymbol, TypeReference[]> _methodTypeParameters = [];
    private readonly string _belteDllName;
    private readonly string _tfm;
    private readonly string _version;

    private Dictionary<string, MethodReference> _stlMap;

    // <Globals> class members
    private readonly Lock _globalsClassLock = new();
    private TypeDefinition _globalsClass;
    private FieldDefinition _c9;
    private FieldDefinition _c9__0_0;
    private MethodDefinition _cInit;
    private MethodDefinition _cctor;
    private MethodDefinition _ctor;
    private MethodDefinition _init;
    internal FieldDefinition randomField;

    private ILEmitter(
        BoundProgram program,
        string assemblySimpleName,
        bool debugMode,
        bool reduced,
        BelteDiagnosticQueue diagnostics) {
        _diagnostics = diagnostics;
        _program = program;
        _debugMode = debugMode;
        _isDll = program.compilation.options.outputKind == OutputKind.DynamicallyLinkedLibrary;

        _tfm = DotnetReferenceResolver.GetTFM();
        var refPackPath = DotnetReferenceResolver.ResolveNetCoreAppRefPath(_tfm, out _version);

#if !DEBUG
#pragma warning disable IL3000
#endif

        var objectDll = typeof(object).Assembly.Location;
        var consoleDll = typeof(Console).Assembly.Location;
        _belteDllName = typeof(Belte.Runtime.Utilities).Assembly.Location;

        if (string.IsNullOrEmpty(_belteDllName))
            _belteDllName = Path.Join(AppContext.BaseDirectory, "Belte.Runtime.dll");

        if (string.IsNullOrEmpty(objectDll))
            objectDll = Path.Join(refPackPath, "System.Runtime.dll");

        if (string.IsNullOrEmpty(consoleDll))
            consoleDll = Path.Join(refPackPath, "System.Console.dll");

        _assemblies = [
            AssemblyDefinition.ReadAssembly(objectDll),
            AssemblyDefinition.ReadAssembly(_belteDllName),
        ];

        if (reduced) {
            _backupAssemblies = [];
        } else {
            _backupAssemblies = [
                AssemblyDefinition.ReadAssembly(consoleDll),
            ];
        }

#if !DEBUG
#pragma warning restore IL3000
#endif

        var references = program.compilation.referenceManager.assemblies.Select(a => a.location);

        foreach (var reference in references) {
            try {
                var assembly = AssemblyDefinition.ReadAssembly(reference);
                _assemblies.Add(assembly);
            } catch (BadImageFormatException) {
                _diagnostics.Push(Error.InvalidReference(reference));
            }
        }

        var assemblyName = new AssemblyNameDefinition(assemblySimpleName, new Version(1, 0));

        _assemblyDefinition = AssemblyDefinition.CreateAssembly(
            assemblyName,
            assemblySimpleName,
            _isDll ? ModuleKind.Dll : ModuleKind.Console
        );

        var belteRuntimeData = File.ReadAllBytes(_belteDllName);
        var embeddedResource = new EmbeddedResource(_belteDllName, ManifestResourceAttributes.Private, belteRuntimeData);

        _assemblyDefinition.MainModule.Resources.Add(embeddedResource);

        ResolveTypes();
        ResolveMethods();
        GenerateSTLMap();

        _topLevelTypes = program.GetTypesToEmit(includeGraphicsWellKnownTypes: true);

        var linearBuilder = ArrayBuilder<NamedTypeSymbol>.GetInstance();

        foreach (var set in _program.nestedTypes)
            linearBuilder.AddRange(set.Value);

        _linearNestedTypes = linearBuilder.ToImmutable();
        _methodBodies = program.GetAllMethodBodies();

        _belteCompilerGeneratedAttributeCtor = _assemblyDefinition.MainModule.ImportReference(
            typeof(BelteCompilerGeneratedAttribute).GetConstructor(Type.EmptyTypes)
        );

        _belteCompilerGeneratedAttribute = _assemblyDefinition.MainModule.ImportReference(
            typeof(BelteCompilerGeneratedAttribute)
        );
    }

    internal static void Emit(
        BoundProgram program,
        string moduleName,
        string outputPath,
        BelteDiagnosticQueue diagnostics) {
        var debugMode = program.compilation.options.optimizationLevel == OptimizationLevel.Debug;
        var reduced = program.compilation.options.noStdLib;
        var emitter = new ILEmitter(program, moduleName, debugMode, reduced, diagnostics);

        if (SupportedProjectType(program, diagnostics))
            emitter.EmitToFile(outputPath, debugMode);
    }

    internal static string EmitToString(
        BoundProgram program,
        string moduleName,
        bool programOnly,
        BelteDiagnosticQueue diagnostics) {
        var emitter = new ILEmitter(program, moduleName, false, false, diagnostics);

        if (SupportedProjectType(program, diagnostics))
            return emitter.EmitToString(programOnly);

        return "<unsupported-project-type>";
    }

    private static bool SupportedProjectType(BoundProgram program, BelteDiagnosticQueue diagnostics) {
        var options = program.compilation.options;

        if (options.outputKind == OutputKind.GraphicsApplication && !options.isScript) {
            diagnostics.Push(Error.Unsupported.GraphicsDll());
            return false;
        }

        return true;
    }

    private void EmitToFile(string outputPath, bool debugMode) {
        EmitInternal();

        if (!_program.compilation.options.enableOutput)
            return;

        var isDll = _program.compilation.options.outputKind == OutputKind.DynamicallyLinkedLibrary;

        if (!isDll)
            EmitRuntimeConfig(outputPath);

        var dllPath = Path.ChangeExtension(outputPath, ".dll");

        if (debugMode) {
            var debugPath = Path.ChangeExtension(outputPath, ".pdb");

            using var symbolStream = File.Create(debugPath);

            var writerParameters = new WriterParameters {
                WriteSymbols = true,
                SymbolStream = symbolStream,
                SymbolWriterProvider = new PortablePdbWriterProvider()
            };

            _assemblyDefinition.Write(dllPath, writerParameters);
        } else {
            _assemblyDefinition.Write(dllPath);
        }

        if (!isDll)
            EmitAppHost(Path.ChangeExtension(outputPath, ".exe"), dllPath);
    }

    private void EmitRuntimeConfig(string outputPath) {
        var runtimeConfigPath = Path.ChangeExtension(outputPath, ".runtimeconfig.json");

        if (File.Exists(runtimeConfigPath))
            File.Delete(runtimeConfigPath);

        var content = $"{{\"runtimeOptions\": {{\"tfm\": \"net{_tfm}\",\"framework\": {{\"name\": \"Microsoft.NETCore.App\",\"version\": \"{_version}\"}}}}}}";

        File.WriteAllText(runtimeConfigPath, content);
    }

    private void EmitAppHost(string outputPath, string dllPath) {
        var dllName = Path.GetFileName(dllPath);
        var appHostPath = DotnetReferenceResolver.ResolveAppHostPath(_version);
        Microsoft.NET.HostModel.AppHost.HostWriter.CreateAppHost(appHostPath, outputPath, dllName);
    }

    private string EmitToString(bool programOnly) {
        EmitInternal(programOnly);

        var stringWriter = new StringWriter();

        using (var indentedTextWriter = new System.CodeDom.Compiler.IndentedTextWriter(stringWriter, "    ")) {
            foreach (var type in _topLevelTypes) {
                if (!programOnly || type.IsFromCompilation(_program.compilation)) {
                    var typeDefinition = _types[type.originalDefinition];
                    WriteType(stringWriter, indentedTextWriter, typeDefinition);
                }
            }

            stringWriter.Flush();
        }

        return stringWriter.ToString();

        void WriteType(
            StringWriter writer,
            System.CodeDom.Compiler.IndentedTextWriter indentedTextWriter,
            TypeDefinition typeDefinition) {
            using var classCurly = new CurlyIndenter(indentedTextWriter, typeDefinition.ToString());
            foreach (var field in typeDefinition.Fields)
                indentedTextWriter.WriteLine(field);

            indentedTextWriter.WriteLine();

            foreach (var method in typeDefinition.Methods) {
                if (!method.IsAbstract && !method.IsPInvokeImpl && !IsBelteCompilerGenerated(method)) {
                    using (var methodCurly = new CurlyIndenter(indentedTextWriter, method.ToString())) {
                        foreach (var instruction in method.Body.Instructions)
                            indentedTextWriter.WriteLine(instruction);
                    }

                    indentedTextWriter.WriteLine();
                } else {
                    if (method.HasPInvokeInfo)
                        indentedTextWriter.WriteLine($"[PInvoke(\"{method.PInvokeInfo.Module}\")]");

                    indentedTextWriter.WriteLine(method.ToString());
                    indentedTextWriter.WriteLine();
                }
            }

            foreach (var nestedType in typeDefinition.NestedTypes) {
                if (!IsBelteCompilerGenerated(nestedType))
                    WriteType(writer, indentedTextWriter, nestedType);
            }
        }
    }

    private bool IsBelteCompilerGenerated(ICustomAttributeProvider symbol) {
        return symbol.CustomAttributes.Any(
            a => a.AttributeType.FullName == _belteCompilerGeneratedAttribute.FullName
        );
    }

    internal TypeReference GetType(TypeSymbol type, bool byRef = false, bool import = true) {
        var typeRef = GetTypeCore(type);

        if (byRef)
            typeRef = typeRef.MakeByReferenceType();

        if (typeRef.IsGenericParameter)
            return typeRef;

        if (import)
            return _assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeRef);

        return typeRef;

        TypeReference GetTypeCore(TypeSymbol type) {
            if (type.specialType == SpecialType.Nullable) {
                var underlyingType = type.GetNullableUnderlyingType();
                var genericArgumentType = GetType(underlyingType);

                if (!underlyingType.isValueType)
                    return genericArgumentType;

                var typeReference = new GenericInstanceType(NetTypeReference.Nullable);
                typeReference.GenericArguments.Add(genericArgumentType);
                return typeReference;
            }

            if (type is ArrayTypeSymbol array) {
                var elementType = GetType(array.elementType);

                if (array.rank == 1)
                    return elementType.MakeArrayType();
                else
                    return elementType.MakeArrayType(array.rank);
            }

            if (type is PointerTypeSymbol pointer) {
                var elementType = GetType(pointer.pointedAtType);
                return elementType.MakePointerType();
            }

            if (type is FunctionPointerTypeSymbol)
                throw ExceptionUtilities.Unreachable();

            if (type is FunctionTypeSymbol f)
                return GetFuncType(f.signature);

            if (type.specialType != SpecialType.None && _specialTypes.TryGetValue(type.specialType, out var value))
                return value;

            if (type is TemplateParameterSymbol t) {
                if (t.templateParameterKind == TemplateParameterKind.Method) {
                    var key = (MethodSymbol)type.containingSymbol.originalDefinition;

                    if (!_methodTypeParameters.ContainsKey(key))
                        TryGetPEMethodTypeParameters(key);

                    var containingMethodTypeParameters = _methodTypeParameters[key];
                    return containingMethodTypeParameters[t.ordinal];
                }

                var containingType = GetTypeCoreInternal(type.containingType);
                return containingType.GenericParameters[t.ordinal];
            }

            return GetTypeWithContainingGenerics((NamedTypeSymbol)type);
        }

        TypeReference GetTypeWithContainingGenerics(NamedTypeSymbol type) {
            var foundType = GetTypeCoreInternal(type);

            // Acceptable inside specific contexts like typeof
            if (type.ContainsErrorType() || type.IsEnumType())
                return foundType;

            var chain = new Stack<NamedTypeSymbol>();
            var current = type;

            while (current is not null) {
                chain.Push(current);
                current = current.containingType;
            }

            var allTypeArgs = new List<TypeReference>();

            while (chain.Count > 0) {
                var s = chain.Pop();

                if (s.arity > 0) {
                    foreach (var arg in s.templateArguments)
                        allTypeArgs.Add(GetType(arg.type.type));
                }
            }

            if (allTypeArgs.Count > 0) {
                var typeReference = new GenericInstanceType(foundType);

                foreach (var generic in allTypeArgs)
                    typeReference.GenericArguments.Add(generic);

                _assemblyDefinition.MainModule.ImportReferenceThreadSafe(Resolve(typeReference));
                // TODO Resolving may be unnecessary here?
                // _assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeReference);
                return typeReference;
            }

            return foundType;
        }

        TypeReference GetTypeCoreInternal(NamedTypeSymbol type) {
            if (type.originalDefinition is PENamedTypeSymbol or MissingMetadataTypeSymbol)
                return ResolveType(type.originalDefinition);

            if (_types.TryGetValue(type.originalDefinition, out var found))
                return found;

            CreateTypeDefinitionAndBases(type);
            return _types[type.originalDefinition];
        }
    }

    private TypeDefinition Resolve(TypeReference reference) {
        if (reference is TypeDefinition td)
            Debug.Print($"Unnecessary resolve call: {td}");

        lock (GlobalCecilLock)
            return reference.Resolve();
    }

    private MethodDefinition Resolve(MethodReference reference) {
        lock (GlobalCecilLock)
            return reference.Resolve();
    }

    private FieldDefinition Resolve(FieldReference reference) {
        lock (GlobalCecilLock)
            return reference.Resolve();
    }

    private void TryGetPEMethodTypeParameters(MethodSymbol method) {
        var reference = GetMethod(method);

        if (reference is GenericInstanceMethod g)
            _methodTypeParameters.Add(method, g.GenericArguments.ToArray());
    }

    private TypeReference GetFuncType(FunctionMethodSymbol signature) {
        if (signature.returnsVoid && signature.parameterCount == 0) {
            var typeRef = ImportType("System.Action");
            _assemblyDefinition.MainModule.ImportReferenceThreadSafe(Resolve(typeRef));
            // TODO Resolving may be unnecessary here?
            // _assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeRef);
            return typeRef;
        } else if (signature.returnsVoid) {
            var typeRef = ImportType($"System.Action`{signature.parameterCount}");
            var genericRef = new GenericInstanceType(typeRef);

            foreach (var p in signature.GetParameterTypes())
                genericRef.GenericArguments.Add(GetType(p.type));

            _assemblyDefinition.MainModule.ImportReferenceThreadSafe(Resolve(genericRef));
            // TODO Resolving may be unnecessary here?
            // _assemblyDefinition.MainModule.ImportReferenceThreadSafe(genericRef);
            return genericRef;
        } else {
            var typeRef = ImportType($"System.Func`{signature.parameterCount + 1}");
            var genericRef = new GenericInstanceType(typeRef);

            foreach (var p in signature.GetParameterTypes())
                genericRef.GenericArguments.Add(GetType(p.type));

            genericRef.GenericArguments.Add(GetType(signature.returnType));

            _assemblyDefinition.MainModule.ImportReferenceThreadSafe(Resolve(genericRef));
            // TODO Resolving may be unnecessary here?
            // _assemblyDefinition.MainModule.ImportReferenceThreadSafe(genericRef);
            return genericRef;
        }
    }

    internal MethodReference GetMethod(MethodSymbol method) {
        MethodReference value = null;
        var found = false;

        if (method.originalDefinition is PEMethodSymbol m) {
            value = ResolveMethod(
                (PENamedTypeSymbol)m.containingType,
                m.metadataName,
                m.GetParameters()
                    .Select(p => p.type.ContainsTemplateParameter()
                        ? null
                        : GetType(p.type, p.refKind != RefKind.None).ToString())
                    .ToArray()
            );

            found = true;
        }

        if (!found && method.originalDefinition is FunctionMethodSymbol s) {
            var typeRef = GetFuncType(s);
            var paramTypes = s.parameterCount == 1
                ? ["T"]
                : s.GetParameters().Select(p => $"T{p.ordinal + 1}").ToArray();
            var invoke = ResolveMethod(typeRef.GetElementType().FullName, "Invoke", paramTypes);
            var genericInvoke = new MethodReference(invoke.Name, invoke.ReturnType, typeRef) {
                HasThis = invoke.HasThis,
                ExplicitThis = invoke.ExplicitThis,
                CallingConvention = invoke.CallingConvention
            };

            foreach (var p in invoke.Parameters)
                genericInvoke.Parameters.Add(new Mono.Cecil.ParameterDefinition(p.ParameterType));

            return _assemblyDefinition.MainModule.ImportReferenceThreadSafe(genericInvoke);
        }

        if (!found && _methods.TryGetValue(method.originalDefinition, out var val)) {
            found = true;
            value = (MethodReference)val;
        }

        if (found) {
            var constructedType = GetType(method.containingType);

            if (method.arity > 0) {
                var def = _assemblyDefinition.MainModule.ImportReferenceThreadSafe(Resolve(value));
                // TODO Resolving may be unnecessary here?
                // var def = _assemblyDefinition.MainModule.ImportReferenceThreadSafe(value);
                var generic = new GenericInstanceMethod(def);
                // TODO Not necessary?
                // {
                //     DeclaringType = constructedType
                // };

                foreach (var p in method.templateArguments.Select(t => GetType(t.type.type)).ToArray())
                    generic.GenericArguments.Add(p);

                return generic;
            }

            if (constructedType.IsGenericInstance) {
                var methodRef = new MethodReference(
                    value.Name,
                    value.ReturnType,
                    constructedType) {
                    HasThis = value.HasThis,
                    ExplicitThis = value.ExplicitThis,
                    CallingConvention = value.CallingConvention
                };

                foreach (var param in value.Parameters)
                    methodRef.Parameters.Add(new Mono.Cecil.ParameterDefinition(param.ParameterType));

                return methodRef;
            }

            return value;
        }

        return CheckStandardMap(method);
    }

    internal MethodReference GetNullableCtor(TypeSymbol genericType) {
        var typeReference = new GenericInstanceType(NetTypeReference.Nullable);
        var genericArgumentType = GetType(genericType);
        typeReference.GenericArguments.Add(genericArgumentType);

        var ctorDef = NetMethodReference.Nullable_ctor;

        var ctorRef = _assemblyDefinition.MainModule.ImportReferenceThreadSafe(ctorDef);
        var genericCtor = new MethodReference(ctorRef.Name, ctorRef.ReturnType, typeReference) {
            HasThis = ctorRef.HasThis,
            ExplicitThis = ctorRef.ExplicitThis,
            CallingConvention = ctorRef.CallingConvention,
        };

        foreach (var p in ctorRef.Parameters)
            genericCtor.Parameters.Add(new Mono.Cecil.ParameterDefinition(p.ParameterType));

        return _assemblyDefinition.MainModule.ImportReferenceThreadSafe(genericCtor);
    }

    internal MethodReference GetTupleCtor(NamedTypeSymbol tupleType) {
        var typeReference = new GenericInstanceType(GetTupleTypeRef(tupleType.arity));

        foreach (var argument in tupleType.templateArguments) {
            var genericArgumentType = GetType(argument.type.type);
            typeReference.GenericArguments.Add(genericArgumentType);
        }

        var ctorDef = GetTupleCtorRef(tupleType.arity);

        var ctorRef = _assemblyDefinition.MainModule.ImportReferenceThreadSafe(ctorDef);
        var genericCtor = new MethodReference(ctorRef.Name, ctorRef.ReturnType, typeReference) {
            HasThis = ctorRef.HasThis,
            ExplicitThis = ctorRef.ExplicitThis,
            CallingConvention = ctorRef.CallingConvention,
        };

        foreach (var p in ctorRef.Parameters)
            genericCtor.Parameters.Add(new Mono.Cecil.ParameterDefinition(p.ParameterType));

        return _assemblyDefinition.MainModule.ImportReferenceThreadSafe(genericCtor);
    }

    private static TypeReference GetTupleTypeRef(int arity) {
        return arity switch {
            1 => NetTypeReference.ValueTuple_T1,
            2 => NetTypeReference.ValueTuple_T2,
            3 => NetTypeReference.ValueTuple_T3,
            4 => NetTypeReference.ValueTuple_T4,
            5 => NetTypeReference.ValueTuple_T5,
            6 => NetTypeReference.ValueTuple_T6,
            7 => NetTypeReference.ValueTuple_T7,
            8 => NetTypeReference.ValueTuple_TRest,
            _ => throw ExceptionUtilities.UnexpectedValue(arity)
        };
    }

    private static MethodReference GetTupleCtorRef(int arity) {
        return arity switch {
            1 => NetMethodReference.ValueTuple_T1_ctor,
            2 => NetMethodReference.ValueTuple_T2_ctor,
            3 => NetMethodReference.ValueTuple_T3_ctor,
            4 => NetMethodReference.ValueTuple_T4_ctor,
            5 => NetMethodReference.ValueTuple_T5_ctor,
            6 => NetMethodReference.ValueTuple_T6_ctor,
            7 => NetMethodReference.ValueTuple_T7_ctor,
            8 => NetMethodReference.ValueTuple_TRest_ctor,
            _ => throw ExceptionUtilities.UnexpectedValue(arity)
        };
    }

    internal MethodReference GetFuncCtor(FunctionMethodSymbol signature) {
        var typeRef = GetFuncType(signature);
        var ctorRef = ResolveMethod(typeRef.GetElementType().FullName, ".ctor", ["System.Object", "System.IntPtr"]);
        var genericCtor = new MethodReference(ctorRef.Name, ctorRef.ReturnType, typeRef) {
            HasThis = ctorRef.HasThis,
            ExplicitThis = ctorRef.ExplicitThis,
            CallingConvention = ctorRef.CallingConvention
        };

        foreach (var p in ctorRef.Parameters)
            genericCtor.Parameters.Add(new Mono.Cecil.ParameterDefinition(p.ParameterType));

        return _assemblyDefinition.MainModule.ImportReferenceThreadSafe(genericCtor);
    }

    internal MethodReference GetNullableValue(TypeSymbol genericType) {
        var typeReference = new GenericInstanceType(NetTypeReference.Nullable);
        var genericArgumentType = GetType(genericType);
        typeReference.GenericArguments.Add(genericArgumentType);

        var getValueDef = NetMethodReference.Nullable_Value;
        var getValueRef = _assemblyDefinition.MainModule.ImportReferenceThreadSafe(getValueDef);
        var genericGetValue = new MethodReference(getValueRef.Name, getValueRef.ReturnType, typeReference) {
            HasThis = getValueRef.HasThis,
            ExplicitThis = getValueRef.ExplicitThis,
            CallingConvention = getValueRef.CallingConvention,
        };

        return _assemblyDefinition.MainModule.ImportReferenceThreadSafe(genericGetValue);
    }

    internal MethodReference GetNullableValueOrDefault(TypeSymbol genericType) {
        var typeReference = new GenericInstanceType(NetTypeReference.Nullable);
        var genericArgumentType = GetType(genericType);
        typeReference.GenericArguments.Add(genericArgumentType);

        var getValueDef = NetMethodReference.Nullable_GetValueOrDefault;
        var getValueRef = _assemblyDefinition.MainModule.ImportReferenceThreadSafe(getValueDef);
        var genericGetValue = new MethodReference(getValueRef.Name, getValueRef.ReturnType, typeReference) {
            HasThis = getValueRef.HasThis,
            ExplicitThis = getValueRef.ExplicitThis,
            CallingConvention = getValueRef.CallingConvention,
        };

        return _assemblyDefinition.MainModule.ImportReferenceThreadSafe(genericGetValue);
    }

    internal MethodReference GetNullableValueOrDefaultT(TypeSymbol genericType) {
        var typeReference = new GenericInstanceType(NetTypeReference.Nullable);
        var genericArgumentType = GetType(genericType);
        typeReference.GenericArguments.Add(genericArgumentType);

        var getValueDef = NetMethodReference.Nullable_GetValueOrDefault_T;
        var getValueRef = _assemblyDefinition.MainModule.ImportReferenceThreadSafe(getValueDef);
        var genericGetValue = new MethodReference(getValueRef.Name, getValueRef.ReturnType, typeReference) {
            HasThis = getValueRef.HasThis,
            ExplicitThis = getValueRef.ExplicitThis,
            CallingConvention = getValueRef.CallingConvention,
        };

        return _assemblyDefinition.MainModule.ImportReferenceThreadSafe(genericGetValue);
    }

    internal MethodReference GetSort(TypeSymbol elementType) {
        var genericArgumentType = GetType(elementType);

        var sortRef = new GenericInstanceMethod(NetMethodReference.LowLevel_Sort);
        sortRef.GenericArguments.Add(genericArgumentType);

        return _assemblyDefinition.MainModule.ImportReferenceThreadSafe(sortRef);
    }

    internal MethodReference GetFill(TypeSymbol elementType) {
        var genericArgumentType = GetType(elementType);

        var fillRef = new GenericInstanceMethod(NetMethodReference.Array_Fill);
        fillRef.GenericArguments.Add(genericArgumentType);

        return _assemblyDefinition.MainModule.ImportReferenceThreadSafe(fillRef);
    }

    internal MethodReference GetArrayEmpty(TypeSymbol elementType) {
        var genericArgumentType = GetType(elementType);

        var sortRef = new GenericInstanceMethod(NetMethodReference.Array_Empty);
        sortRef.GenericArguments.Add(genericArgumentType);

        return _assemblyDefinition.MainModule.ImportReferenceThreadSafe(sortRef);
    }

    internal MethodReference GetLength(TypeSymbol elementType) {
        var genericArgumentType = GetType(elementType);

        var lengthRef = new GenericInstanceMethod(NetMethodReference.LowLevel_Length);
        lengthRef.GenericArguments.Add(genericArgumentType);

        return _assemblyDefinition.MainModule.ImportReferenceThreadSafe(lengthRef);
    }

    internal MethodReference GetSizeOf(TypeSymbol elementType) {
        var genericArgumentType = GetType(elementType);

        var sizeOfRef = new GenericInstanceMethod(NetMethodReference.Marshal_SizeOf);
        sizeOfRef.GenericArguments.Add(genericArgumentType);

        return _assemblyDefinition.MainModule.ImportReferenceThreadSafe(sizeOfRef);
    }

    internal MethodReference GetBitCast(TypeSymbol tFrom, TypeSymbol tTo) {
        var genericArgumentType1 = GetType(tFrom);
        var genericArgumentType2 = GetType(tTo);

        var bitCastRef = new GenericInstanceMethod(NetMethodReference.Unsafe_BitCast);
        bitCastRef.GenericArguments.Add(genericArgumentType1);
        bitCastRef.GenericArguments.Add(genericArgumentType2);

        return _assemblyDefinition.MainModule.ImportReferenceThreadSafe(bitCastRef);
    }

    internal MethodReference GetNullableHasValue(TypeSymbol genericType) {
        var typeReference = new GenericInstanceType(NetTypeReference.Nullable);
        var genericArgumentType = GetType(genericType);
        typeReference.GenericArguments.Add(genericArgumentType);

        var getValueDef = NetMethodReference.Nullable_HasValue;
        var getValueRef = _assemblyDefinition.MainModule.ImportReferenceThreadSafe(getValueDef);
        var genericGetValue = new MethodReference(getValueRef.Name, getValueRef.ReturnType, typeReference) {
            HasThis = getValueRef.HasThis,
            ExplicitThis = getValueRef.ExplicitThis,
            CallingConvention = getValueRef.CallingConvention,
        };

        return _assemblyDefinition.MainModule.ImportReferenceThreadSafe(genericGetValue);
    }

    internal MethodReference GetNullAssert(TypeSymbol genericType) {
        var genericMethod = new GenericInstanceMethod(NetMethodReference.AssertNull);
        genericMethod.GenericArguments.Add(GetType(genericType));
        return _assemblyDefinition.MainModule.ImportReferenceThreadSafe(genericMethod);
    }

    internal FieldReference GetField(FieldSymbol field) {
        if (field.originalDefinition is PEFieldSymbol f) {
            var peType = GetType(field.containingType);
            var peField = Resolve(peType).Fields.Single(e => e.Name == f.name);

            // TODO Might not need to always import here
            // var fieldType = peField.ContainsGenericParameter
            //     ? _assemblyDefinition.MainModule.ImportReferenceThreadSafe(peField.FieldType, peType)
            //     : peField.FieldType;

            return new FieldReference(
                peField.Name,
                _assemblyDefinition.MainModule.ImportReferenceThreadSafe(peField.FieldType, peType),
                // fieldType,
                peType
            );
        } else {
            var fieldRef = _fields[field.originalDefinition];
            var constructedType = GetType(GetFieldContainingType(field));

            // TODO Might not need to always import here
            // var fieldType = fieldRef.ContainsGenericParameter
            //     ? _assemblyDefinition.MainModule.ImportReferenceThreadSafe(fieldRef.FieldType, constructedType)
            //     : fieldRef.FieldType;

            return new FieldReference(
                fieldRef.Name,
                _assemblyDefinition.MainModule.ImportReferenceThreadSafe(fieldRef.FieldType, constructedType),
                // fieldType,
                constructedType
            );
        }
    }

    private TypeSymbol GetFieldContainingType(FieldSymbol field) {
        if (field.isAnonymousUnionMember)
            return ((SourceNamedTypeSymbol)field.containingType).anonymousUnionTypes[field];

        return field.containingType;
    }

    internal override NamedTypeSymbol GetFixedImplementationType(SourceFixedFieldSymbol field) {
        return _program.fixedImplementationTypes[field];
    }

    internal override void EmitGlobalsClass() {
        if (_globalsClass is not null)
            return;

        lock (_globalsClassLock) {
            if (_globalsClass is not null)
                return;

            _globalsClass = new TypeDefinition(
                "",
                "<Globals>",
                TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Public,
                _specialTypes[SpecialType.Object]
            );

            randomField = new FieldDefinition(
                "<random>",
                FieldAttributes.Static | FieldAttributes.Public,
                NetTypeReference.Random
            );

            _globalsClass.Fields.Add(randomField);

            var cctor = new MethodDefinition(
                ".cctor",
                MethodAttributes.Static | MethodAttributes.Private |
                MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                _specialTypes[SpecialType.Void]
            );

            cctor.Body.InitLocals = true;
            var cctorILProcessor = cctor.Body.GetILProcessor();
            cctorILProcessor.Emit(OpCodes.Newobj, NetMethodReference.Random_ctor);
            cctorILProcessor.Emit(OpCodes.Stsfld, randomField);
            cctorILProcessor.Emit(OpCodes.Ret);

            _globalsClass.Methods.Insert(0, cctor);

            lock (GlobalCecilLock)
                _assemblyDefinition.MainModule.Types.Add(_globalsClass);
        }
    }

    private void CreateTypeDefinitionAndBases(NamedTypeSymbol type) {
        var baseStack = new Stack<NamedTypeSymbol>();
        var current = type;

        while (current is not null) {
            if (current.specialType is SpecialType.Object)
                break;

            baseStack.Push(current);
            current = current.baseType;
        }

        while (baseStack.Count > 0) {
            var baseType = baseStack.Pop();

            if (_types.ContainsKey(baseType.originalDefinition))
                continue;

            var typeDefinition = CreateNamedTypeDefinition(baseType);

            lock (GlobalCecilLock)
                _assemblyDefinition.MainModule.Types.Add(typeDefinition);
        }
    }

    private void EmitInternal(bool programOnly = false) {
        CompleteWellKnownTypes();

        foreach (var type in _topLevelTypes)
            CreateTypeDefinitionAndBases(type);

        if (_program.compilation.options.concurrentBuild && !programOnly) {
            var maxParallels = _program.compilation.options.maxCoreCount;
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = maxParallels };

            var topLevelTypes = _topLevelTypes;
            Parallel.For(0, topLevelTypes.Length, parallelOptions, i => CreateMemberDefinitions(topLevelTypes[i]));

            var linearTypes = _linearNestedTypes;
            Parallel.For(0, linearTypes.Length, parallelOptions, i => CreateMemberDefinitions(linearTypes[i]));

            Parallel.ForEach(_methods, parallelOptions, method => EmitMethod(method.Value));
        } else {
            foreach (var type in _topLevelTypes) {
                if (!programOnly || type.IsFromCompilation(_program.compilation))
                    CreateMemberDefinitions(type);
            }

            foreach (var type in _linearNestedTypes) {
                if (!programOnly || type.IsFromCompilation(_program.compilation))
                    CreateMemberDefinitions(type);
            }

            foreach (var method in _methods) {
                if (!programOnly || method.Key.IsFromCompilation(_program.compilation))
                    EmitMethod(method.Value);
            }
        }

        var entryPoint = _program.entryPoint;

        if (entryPoint is not null) {
            if (!(entryPoint.returnsVoid || entryPoint.returnType.specialType == SpecialType.Int32)) {
                _diagnostics.Push(Error.IncompatibleEntryPointReturn(entryPoint.location, entryPoint));
            } else {
                var entry = CreateEntryWrapperIfApplicable(entryPoint);

                if (entryPoint.isStatic)
                    _assemblyDefinition.EntryPoint = entry;
                else
                    CreateStaticEntryPoint(entry, entryPoint.containingType.instanceConstructors[0]);
            }
        }

        if (_debugMode) {
            var debuggableAttribute = new CustomAttribute(ResolveMethod("System.Diagnostics.DebuggableAttribute", ".ctor", ["System.Boolean", "System.Boolean"]));
            debuggableAttribute.ConstructorArguments.Add(new CustomAttributeArgument(_specialTypes[SpecialType.Bool], true));
            debuggableAttribute.ConstructorArguments.Add(new CustomAttributeArgument(_specialTypes[SpecialType.Bool], true));

            _assemblyDefinition.CustomAttributes.Add(debuggableAttribute);
        }
    }

    private void CompleteWellKnownTypes() {
        _types.Add(
            CorLibrary.GetWellKnownType(WellKnownType.Exception),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(Exception))));
        _types.Add(
            CorLibrary.GetWellKnownType(WellKnownType.ValueTuple_T1),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<>))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T1_Item1),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<>).GetField("Item1"))));
        _types.Add(
            CorLibrary.GetWellKnownType(WellKnownType.ValueTuple_T2),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,>))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T2_Item1),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,>).GetField("Item1"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T2_Item2),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,>).GetField("Item2"))));
        _types.Add(
            CorLibrary.GetWellKnownType(WellKnownType.ValueTuple_T3),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,>))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T3_Item1),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,>).GetField("Item1"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T3_Item2),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,>).GetField("Item2"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T3_Item3),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,>).GetField("Item3"))));
        _types.Add(
            CorLibrary.GetWellKnownType(WellKnownType.ValueTuple_T4),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,>))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T4_Item1),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,>).GetField("Item1"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T4_Item2),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,>).GetField("Item2"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T4_Item3),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,>).GetField("Item3"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T4_Item4),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,>).GetField("Item4"))));
        _types.Add(
            CorLibrary.GetWellKnownType(WellKnownType.ValueTuple_T5),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,>))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T5_Item1),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,>).GetField("Item1"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T5_Item2),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,>).GetField("Item2"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T5_Item3),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,>).GetField("Item3"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T5_Item4),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,>).GetField("Item4"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T5_Item5),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,>).GetField("Item5"))));
        _types.Add(
            CorLibrary.GetWellKnownType(WellKnownType.ValueTuple_T6),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,>))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T6_Item1),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,>).GetField("Item1"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T6_Item2),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,>).GetField("Item2"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T6_Item3),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,>).GetField("Item3"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T6_Item4),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,>).GetField("Item4"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T6_Item5),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,>).GetField("Item5"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T6_Item6),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,>).GetField("Item6"))));
        _types.Add(
            CorLibrary.GetWellKnownType(WellKnownType.ValueTuple_T7),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,,>))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T7_Item1),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,,>).GetField("Item1"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T7_Item2),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,,>).GetField("Item2"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T7_Item3),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,,>).GetField("Item3"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T7_Item4),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,,>).GetField("Item4"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T7_Item5),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,,>).GetField("Item5"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T7_Item6),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,,>).GetField("Item6"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_T7_Item7),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,,>).GetField("Item7"))));
        _types.Add(
            CorLibrary.GetWellKnownType(WellKnownType.ValueTuple_TRest),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,,,>))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_TRest_Item1),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,,,>).GetField("Item1"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_TRest_Item2),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,,,>).GetField("Item2"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_TRest_Item3),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,,,>).GetField("Item3"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_TRest_Item4),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,,,>).GetField("Item4"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_TRest_Item5),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,,,>).GetField("Item5"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_TRest_Item6),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,,,>).GetField("Item6"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_TRest_Item7),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,,,>).GetField("Item7"))));
        _fields.Add(
            (FieldSymbol)CorLibrary.GetWellKnownMember(WellKnownMember.ValueTuple_TRest_Rest),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(ValueTuple<,,,,,,,>).GetField("Rest"))));
        _types.Add(
            CorLibrary.GetWellKnownType(WellKnownType.Attribute),
            Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(Attribute))));
    }

    private MethodDefinition CreateEntryWrapperIfApplicable(MethodSymbol entrySymbol) {
        var entryDef = _methods[entrySymbol];

        if (entrySymbol.parameterCount != 1 || entrySymbol.GetParameterType(0).specialType == SpecialType.Array)
            return entryDef;

        var isStatic = entrySymbol.isStatic;

        var programType = _types[entrySymbol.containingType];

        var wrapper = new MethodDefinition("Main", entryDef.Attributes, entryDef.ReturnType);

        wrapper.Parameters.Add(new Mono.Cecil.ParameterDefinition(
            "args",
            ParameterAttributes.None,
            _assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(string[]))
        ));

        programType.Methods.Add(wrapper);

        var wrapperBuilder = new CecilILBuilder(null, this, wrapper);
        var il = wrapperBuilder.iLProcessor;

        var arrayTypeSymbol = (NamedTypeSymbol)entrySymbol.GetParameterType(0);

        var ctorSymbol = CorLibrary.GetWellKnownMethod(WellKnownMember.Array_ctor_2).AsMember(arrayTypeSymbol);
        var ctor = GetMethod(ctorSymbol);

        il.Emit(OpCodes.Ldarg_0);

        if (!isStatic)
            il.Emit(OpCodes.Ldarg_1);

        il.Emit(OpCodes.Ldlen);
        il.Emit(OpCodes.Conv_I8);
        il.Emit(isStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1);
        il.Emit(OpCodes.Newobj, ctor);
        il.Emit(OpCodes.Call, entryDef);
        il.Emit(OpCodes.Ret);

        wrapperBuilder.Finish();

        return wrapper;
    }

    private void CreateStaticEntryPoint(MethodDefinition instanceEntry, MethodSymbol programCtor) {
        var hasArgs = instanceEntry.Parameters.Count > 0;

        var staticClass = new TypeDefinition(
            "",
            "<s_Program>",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed,
            _specialTypes[SpecialType.Object]
        );

        var staticEntry = new MethodDefinition(
            "Main",
            MethodAttributes.Static | MethodAttributes.Public,
            instanceEntry.ReturnType
        );

        if (hasArgs) {
            staticEntry.Parameters.Add(new Mono.Cecil.ParameterDefinition(
                "args",
                ParameterAttributes.None,
                instanceEntry.Parameters[0].ParameterType
            ));
        }

        staticClass.Methods.Add(staticEntry);

        var staticBuilder = new CecilILBuilder(null, this, staticEntry);
        var il = staticBuilder.iLProcessor;

        il.Emit(OpCodes.Newobj, GetMethod(programCtor));

        if (hasArgs)
            il.Emit(OpCodes.Ldarg_0);

        il.Emit(OpCodes.Callvirt, instanceEntry);
        il.Emit(OpCodes.Ret);

        staticBuilder.Finish();

        _assemblyDefinition.MainModule.Types.Add(staticClass);
        _assemblyDefinition.EntryPoint = staticEntry;
    }

    private TypeDefinition CreateNamedTypeDefinition(NamedTypeSymbol type, bool isNested = false) {
        TypeDefinition typeDefinition;

        if (type.isInterface) {
            typeDefinition = new TypeDefinition(
                GetNamespaceName(type),
                type.name,
                GetTypeAttributes(type, isNested)
            );
        } else {
            typeDefinition = new TypeDefinition(
                GetNamespaceName(type),
                type.name,
                GetTypeAttributes(type, isNested),
                GetBaseType(type)
            );
        }

        AddInterfaceImplementations(type, typeDefinition);

        if (type.explicitAlignment is not null)
            typeDefinition.PackingSize = (short)type.explicitAlignment;

        if (type.enumFlagsAttribute) {
            var flagsCtor = _assemblyDefinition.MainModule.ImportReferenceThreadSafe(
                typeof(FlagsAttribute).GetConstructor(Type.EmptyTypes)
            );

            var flagsAttr = new CustomAttribute(flagsCtor);
            typeDefinition.CustomAttributes.Add(flagsAttr);
        }

        GenericParameter[] workingParams = [];

        if (type.arity > 0) {
            workingParams = type.templateParameters.Select(t => new GenericParameter(t.name, typeDefinition)).ToArray();

            foreach (var generic in workingParams)
                typeDefinition.GenericParameters.Add(generic);
        }

        _types.Add(type.originalDefinition, typeDefinition);

        // ? CreateNestedTypes calls CreateNestedTypes directly
        if (!isNested)
            CreateNestedTypes(type, typeDefinition, workingParams);

        return typeDefinition;
    }

    private void AddInterfaceImplementations(NamedTypeSymbol type, TypeDefinition typeDefinition) {
        foreach (var @interface in type.Interfaces())
            typeDefinition.Interfaces.Add(new InterfaceImplementation(GetType(@interface)));
    }

    private TypeReference GetBaseType(NamedTypeSymbol type) {
        if (type.IsStructType())
            return NetTypeReference.ValueType;

        if (type.IsEnumType())
            return NetTypeReference.Enum;

        Debug.Assert(type.baseType is not null);
        return GetType(type.baseType);
    }

    private void CreateNestedTypes(
        NamedTypeSymbol type,
        TypeDefinition typeDefinition,
        GenericParameter[] workingParams) {
        foreach (var member in type.GetTypeMembers())
            CreateNestedType(member, workingParams);

        if (_program.nestedTypes.ContainsKey(type)) {
            foreach (var nestedType in _program.nestedTypes[type])
                CreateNestedType(nestedType, workingParams);
        }

        void CreateNestedType(NamedTypeSymbol nestedType, GenericParameter[] workingParams) {
            var nestedDefinition = CreateNamedTypeDefinition(nestedType, isNested: true);

            workingParams = workingParams.Concat(
                nestedType.templateParameters.Select(t => new GenericParameter(t.name, nestedDefinition))).ToArray();

            foreach (var generic in workingParams)
                nestedDefinition.GenericParameters.Add(new GenericParameter(generic.Name, nestedDefinition));

            CreateNestedTypes(nestedType, nestedDefinition, workingParams);
            typeDefinition.NestedTypes.Add(nestedDefinition);
        }
    }

    private string GetNamespaceName(Symbol symbol) {
        var namespaceName = symbol.containingNamespace?.isGlobalNamespace == true ? "" : symbol.containingNamespace?.name ?? "";

        if (symbol.IsFromCompilation(_program.compilation))
            return namespaceName;

        if (namespaceName == "")
            return "Belte";

        return $"Belte.{namespaceName}";
    }

    private void CreateEnumMemberDefinitions(NamedTypeSymbol type, TypeDefinition typeDefinition) {
        var underlyingType = type.GetEnumUnderlyingType().StrippedType();
        var underlyingTypeRef = GetType(underlyingType);
        var underlyingField = (type as SourceNamedTypeSymbol).enumValueField;

        var underlyingFieldDef = new FieldDefinition(
            underlyingField.name,
            FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName,
            underlyingTypeRef
        );

        typeDefinition.Fields.Add(underlyingFieldDef);
        _fields.Add(underlyingField, underlyingFieldDef);

        foreach (var member in type.GetMembers()) {
            if (member is not FieldSymbol f)
                continue;

            var fieldDef = new FieldDefinition(
                f.name,
                FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal,
                typeDefinition
            ) {
                Constant = f.constantValue
            };

            typeDefinition.Fields.Add(fieldDef);
            _fields.Add(f, fieldDef);
        }
    }

    private void CreateMemberDefinitions(NamedTypeSymbol type) {
        var typeDefinition = _types[type.originalDefinition];

        if (type.IsEnumType()) {
            CreateEnumMemberDefinitions(type, typeDefinition);
            return;
        }

        var isAnonymousUnion = type is AnonymousUnionType;
        var seenGroupIds = new HashSet<int>();

        foreach (var member in type.GetMembers()) {
            if (member is FieldSymbol f) {
                if (!isAnonymousUnion && f.isAnonymousUnionMember) {
                    if (seenGroupIds.Add(f.unionGroupId))
                        CreateAnonymousUnionField(f, typeDefinition, (SourceNamedTypeSymbol)type);

                    continue;
                }

                if (f.isFixedSizeBuffer) {
                    CreateFixedSizeBufferField(f as SourceFixedFieldSymbol, typeDefinition, type);
                    continue;
                }

                var fieldDefinition = new FieldDefinition(
                    f.name,
                    GetFieldAttributes(f),
                    (f.type.typeKind == TypeKind.FunctionPointer)
                        ? _specialTypes[SpecialType.IntPtr]
                        : GetType(f.type, f.refKind != RefKind.None)
                );

                if (type.IsStructType() && f.type.specialType == SpecialType.Bool)
                    fieldDefinition.MarshalInfo = new MarshalInfo(NativeType.I1);

                if (type.isUnionStruct)
                    fieldDefinition.Offset = 0;

                _fields.Add(f, fieldDefinition);
                typeDefinition.Fields.Add(fieldDefinition);
            } else if (member is NamedTypeSymbol t) {
                CreateMemberDefinitions(t);
            } else if (member is MethodSymbol m && m.isAbstract) {
                CreateMethodDefinition(m, null, typeDefinition);
            }
        }

        // Checking program map for methods to make sure synthesized ones are included (such as closure methods)
        foreach (var pair in _methodBodies) {
            if (pair.Item1.containingType.Equals(type)) {
                CreateMethodDefinition(pair.Item1, pair.Item2, typeDefinition);

                if (pair.Item1 == _program.entryPoint)
                    CreateAssemblyResolverDefinition(typeDefinition);
            }
        }
    }

    private void CreateFixedSizeBufferField(
        SourceFixedFieldSymbol field,
        TypeDefinition typeDefinition,
        NamedTypeSymbol parent) {
        var fixedImpl = GetFixedImplementationType(field);

        var elementType = ((PointerTypeSymbol)field.type).pointedAtType;
        var elementSize = elementType.FixedBufferElementSizeInBytes();

        var nestedType = new TypeDefinition(
            typeDefinition.Namespace,
            fixedImpl.name,
            GetTypeAttributes(fixedImpl, true),
            GetBaseType(fixedImpl)
        ) {
            PackingSize = 0,
            ClassSize = field.fixedSize * elementSize
        };

        typeDefinition.NestedTypes.Add(nestedType);

        var nestedBufferField = fixedImpl.fixedElementField;

        var nestedBufferFieldDef = new FieldDefinition(
            nestedBufferField.name,
            GetFieldAttributes(nestedBufferField),
            GetType(nestedBufferField.type)
        );

        nestedType.Fields.Add(nestedBufferFieldDef);

        var adaptedFieldDef = new FieldDefinition(
            field.name,
            GetFieldAttributes(field),
            nestedType
        );

        if (parent.isUnionStruct)
            adaptedFieldDef.Offset = 0;

        typeDefinition.Fields.Add(adaptedFieldDef);

        _fields.Add(field, adaptedFieldDef);
        _fields.Add(nestedBufferField, nestedBufferFieldDef);

        lock (_types)
            _types.Add(fixedImpl, nestedType);
    }

    private void CreateAnonymousUnionField(
        FieldSymbol field,
        TypeDefinition typeDefinition,
        SourceNamedTypeSymbol parent) {
        var union = parent.anonymousUnionTypes[field];
        var unionField = parent.anonymousUnionFields[union];

        var unionFieldDef = new FieldDefinition(
            unionField.name,
            GetFieldAttributes(unionField),
            GetType(union)
        );

        if (parent.isUnionStruct)
            unionFieldDef.Offset = 0;

        typeDefinition.Fields.Add(unionFieldDef);
        _fields.Add(unionField, unionFieldDef);
    }

    private MethodDefinition CreateMethodDefinition(
        MethodSymbol method,
        BoundBlockStatement body,
        TypeDefinition containingType) {
        if (method.isExtern)
            return CreatePInvokeMethodDefinition(method, containingType);
        else
            return CreateNormalMethodDefinition(method, body, containingType);
    }

    private MethodDefinition CreateNormalMethodDefinition(
        MethodSymbol method,
        BoundBlockStatement body,
        TypeDefinition containingType) {
        var methodDefinition = new MethodDefinition(
            method.name,
            GetMethodAttributes(method),
            GetTypeOrIntPtr(method.returnType, method.returnsByRef)
        );

        foreach (var parameter in method.parameters) {
            var parameterDefinition = new Mono.Cecil.ParameterDefinition(
                parameter.name,
                ParameterAttributes.None,
                GetTypeOrIntPtr(parameter.type, parameter.refKind != RefKind.None)
            );

            methodDefinition.Parameters.Add(parameterDefinition);
        }

        if (method.arity > 0) {
            var genericBuilder = ArrayBuilder<GenericParameter>.GetInstance();

            foreach (var templateParameter in method.templateParameters) {
                var genericParameter = new GenericParameter(templateParameter.name, methodDefinition);
                methodDefinition.GenericParameters.Add(genericParameter);
                genericBuilder.Add(genericParameter);
            }

            _methodTypeParameters.Add(method, genericBuilder.ToArrayAndFree());
        }

        SetCustomAttributes(method, methodDefinition);

        _methods.Add(method, methodDefinition);

        if (body is not null)
            _methodBodyMap.Add(methodDefinition, (method, body));

        containingType.Methods.Add(methodDefinition);

        if (method.methodKind == MethodKind.Finalizer) {
            var baseFinalize = GetMethod(MethodCompiler.GetBaseTypeFinalizeMethod(method));
            methodDefinition.Overrides.Add(baseFinalize);
        }

        return methodDefinition;
    }

    private void SetCustomAttributes(MethodSymbol method, MethodDefinition methodDefinition) {
        var unmanagedAttribute = method.GetUnmanagedCallersOnlyAttributeData(true);

        if (unmanagedAttribute is not null && unmanagedAttribute != UnmanagedCallersOnlyAttributeData.Uninitialized) {
            var unmanagedAttr = _assemblyDefinition.MainModule
                .ImportReferenceThreadSafe(typeof(System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute));
            var callConvCdecl = _assemblyDefinition.MainModule
                .ImportReferenceThreadSafe(typeof(System.Runtime.CompilerServices.CallConvCdecl));

            var attrCtor = _assemblyDefinition.MainModule.ImportReferenceThreadSafe(
                typeof(System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute)
                    .GetConstructor(Type.EmptyTypes)
            );

            var attr = new CustomAttribute(attrCtor);

            var typeArray = new CustomAttributeArgument(
                _assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(Type[])),
                new CustomAttributeArgument[] {
                new CustomAttributeArgument(
                    _assemblyDefinition.MainModule.ImportReferenceThreadSafe(typeof(Type)),
                    callConvCdecl)
                }
            );

            var callConvsField = typeof(System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute)
                .GetField(nameof(System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute.CallConvs));

            // var propRef = new PropertyDefinition(
            //     callConvsField.Name,
            //     PropertyAttributes.None,
            //     _assemblyDefinition.MainModule.ImportReference(typeof(Type[]))
            // );

            attr.Fields.Add(new CustomAttributeNamedArgument(
                callConvsField.Name,
                typeArray
            ));

            methodDefinition.CustomAttributes.Add(attr);
        }
    }

    private TypeReference GetTypeOrIntPtr(TypeSymbol type, bool byRef) {
        if (type.typeKind == TypeKind.FunctionPointer)
            return _specialTypes[SpecialType.IntPtr];

        return GetType(type, byRef);
    }

    private MethodDefinition CreatePInvokeMethodDefinition(MethodSymbol method, TypeDefinition containingType) {
        var dllImportData = method.GetDllImportData();
        var returnType = GetTypeOrIntPtr(method.returnType, method.returnsByRef);
        var methodDefinition = new MethodDefinition(
            method.name,
            GetMethodAttributes(method),
            returnType
        );

        var moduleReference = new ModuleReference(dllImportData.moduleName);

        var pInvoke = new PInvokeInfo(
            GetCallingConvention(dllImportData.callingConvention) | GetCharSet(dllImportData.characterSet),
            method.name,
            moduleReference
        );

        methodDefinition.PInvokeInfo = pInvoke;
        methodDefinition.IsPreserveSig = true;

        foreach (var parameter in method.parameters) {
            var parameterDefinition = new Mono.Cecil.ParameterDefinition(
                parameter.name,
                ParameterAttributes.None,
                GetTypeOrIntPtr(parameter.type, parameter.refKind != RefKind.None)
            );

            if (parameter.type.specialType == SpecialType.Bool)
                parameterDefinition.MarshalInfo = new MarshalInfo(NativeType.I1);

            methodDefinition.Parameters.Add(parameterDefinition);
        }

        if (method.returnType.specialType == SpecialType.Bool)
            methodDefinition.MethodReturnType.MarshalInfo = new MarshalInfo(NativeType.I1);

        SetCustomAttributes(method, methodDefinition);

        _methods.Add(method, methodDefinition);

        lock (GlobalCecilLock) {
            containingType.Methods.Add(methodDefinition);
            _assemblyDefinition.MainModule.ModuleReferences.Add(moduleReference);
        }

        return methodDefinition;

        PInvokeAttributes GetCallingConvention(CallingConvention callingConvention) {
            return callingConvention switch {
                CallingConvention.Winapi => PInvokeAttributes.CallConvWinapi,
                CallingConvention.FastCall => PInvokeAttributes.CallConvFastcall,
                CallingConvention.Cdecl => PInvokeAttributes.CallConvCdecl,
                CallingConvention.StdCall => PInvokeAttributes.CallConvStdCall,
                CallingConvention.ThisCall => PInvokeAttributes.CallConvThiscall,
                _ => throw ExceptionUtilities.UnexpectedValue(callingConvention)
            };
        }

        PInvokeAttributes GetCharSet(System.Runtime.InteropServices.CharSet charSet) {
            return charSet switch {
                System.Runtime.InteropServices.CharSet.Ansi => PInvokeAttributes.CharSetAnsi,
                System.Runtime.InteropServices.CharSet.Auto => PInvokeAttributes.CharSetAuto,
                System.Runtime.InteropServices.CharSet.None => PInvokeAttributes.CharSetNotSpec,
                System.Runtime.InteropServices.CharSet.Unicode => PInvokeAttributes.CharSetUnicode,
                _ => throw ExceptionUtilities.UnexpectedValue(charSet),
            };
        }
    }

    private static TypeAttributes GetTypeAttributes(NamedTypeSymbol type, bool isNested) {
        // Structs use TypeAttributes.Class
        var attributes = type.isInterface ? TypeAttributes.Interface : TypeAttributes.Class;

        if (type.isStatic)
            attributes |= TypeAttributes.Abstract | TypeAttributes.Sealed;
        if (type.isAbstract)
            attributes |= TypeAttributes.Abstract;
        if (type.isSealed)
            attributes |= TypeAttributes.Sealed;

        if (type.IsStructType())
            attributes |= type.isUnionStruct ? TypeAttributes.ExplicitLayout : TypeAttributes.SequentialLayout;

        attributes |= type.declaredAccessibility switch {
            Accessibility.Private => TypeAttributes.NestedPrivate,
            Accessibility.Public when isNested => TypeAttributes.NestedPublic,
            Accessibility.Public => TypeAttributes.Public,
            Accessibility.Protected => TypeAttributes.NestedFamily,
            _ => 0
        };

        return attributes;
    }

    private static FieldAttributes GetFieldAttributes(FieldSymbol field) {
        FieldAttributes attributes = field.declaredAccessibility switch {
            Accessibility.Private => FieldAttributes.Private,
            Accessibility.Public => FieldAttributes.Public,
            Accessibility.Protected => FieldAttributes.Family,
            _ => 0
        };

        if (field.isStatic)
            attributes |= FieldAttributes.Static;

        return attributes;
    }

    private static MethodAttributes GetMethodAttributes(MethodSymbol method) {
        var attributes = method.declaredAccessibility switch {
            Accessibility.Private => MethodAttributes.Private,
            Accessibility.Public => MethodAttributes.Public,
            Accessibility.Protected => MethodAttributes.Family,
            _ => 0
        } | MethodAttributes.HideBySig;

        if (method.isStatic)
            attributes |= MethodAttributes.Static;
        if (method.isAbstract)
            attributes |= MethodAttributes.Abstract | MethodAttributes.Virtual;
        if (method.IsMetadataVirtual())
            attributes |= MethodAttributes.Virtual;
        if (method.isOverride)
            attributes |= MethodAttributes.Virtual;

        switch (method.methodKind) {
            case MethodKind.Constructor:
            case MethodKind.StaticConstructor:
                attributes |= MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
                break;
        }

        return attributes;
    }

    private void EmitMethod(MethodDefinition methodDefinition) {
        if (methodDefinition.IsAbstract || (methodDefinition.Attributes & MethodAttributes.PInvokeImpl) != 0)
            return;

        var (method, body) = _methodBodyMap[methodDefinition];
        var ilBuilder = new CecilILBuilder(method, this, methodDefinition);
        var codeGen = new CodeGenerator(this, method, body, ilBuilder, _debugMode, _diagnostics);

        if (_program.entryPoint == method)
            EmitAssemblyResolver(methodDefinition);

        codeGen.Generate();

        if (!_debugMode)
            methodDefinition.Body.Optimize();

        if (_debugMode) {
            methodDefinition.DebugInformation.Scope = new ScopeDebugInformation(
                methodDefinition.Body.Instructions.First(),
                methodDefinition.Body.Instructions.Last()
            );

            foreach (CecilVariableDefinition local in ilBuilder.localSlotManager.LocalsInOrder()) {
                if (local.synthesizedKind == SynthesizedLocalKind.UserDefined) {
                    methodDefinition.DebugInformation.Scope.Variables.Add(
                        new VariableDebugInformation(local.variableDefinition, local.name)
                    );
                }
            }
        }
    }

    private void CreateAssemblyResolverDefinition(TypeDefinition mainType) {
        var cDefinition = new TypeDefinition(
            "",
            "<>AssemblyResolverClass",
            TypeAttributes.NestedPrivate |
            TypeAttributes.Sealed |
            TypeAttributes.BeforeFieldInit,
            _specialTypes[SpecialType.Object]
        );

        cDefinition.CustomAttributes.Add(new CustomAttribute(_belteCompilerGeneratedAttributeCtor));

        _c9 = new FieldDefinition(
            "<>9",
            FieldAttributes.InitOnly | FieldAttributes.Static | FieldAttributes.Public,
            cDefinition
        );

        _c9__0_0 = new FieldDefinition(
            "<>9__0_0",
            FieldAttributes.InitOnly | FieldAttributes.Static | FieldAttributes.Public,
            ImportType("System.ResolveEventHandler")
        );

        cDefinition.Fields.Add(_c9);
        cDefinition.Fields.Add(_c9__0_0);

        var cctor = new MethodDefinition(
            ".cctor",
            MethodAttributes.Static | MethodAttributes.Private |
            MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            _specialTypes[SpecialType.Void]
        );

        cctor.CustomAttributes.Add(new CustomAttribute(_belteCompilerGeneratedAttributeCtor));

        var ctor = new MethodDefinition(
            ".ctor",
            MethodAttributes.Public |
            MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            _specialTypes[SpecialType.Void]
        );

        ctor.CustomAttributes.Add(new CustomAttribute(_belteCompilerGeneratedAttributeCtor));

        var methodDefinition = new MethodDefinition(
            "<Main>AssemblyResolver",
            MethodAttributes.HideBySig | MethodAttributes.Assembly,
            ImportType("System.Reflection.Assembly")
        );

        methodDefinition.CustomAttributes.Add(new CustomAttribute(_belteCompilerGeneratedAttributeCtor));

        methodDefinition.Parameters.Add(
            new Mono.Cecil.ParameterDefinition(
                "s",
                ParameterAttributes.None,
                _specialTypes[SpecialType.Object]
            )
        );

        methodDefinition.Parameters.Add(
            new Mono.Cecil.ParameterDefinition(
                "e",
                ParameterAttributes.None,
                ImportType("System.ResolveEventArgs")
            )
        );

        _cInit = methodDefinition;
        _cctor = cctor;
        _ctor = ctor;
        cDefinition.Methods.Add(cctor);
        cDefinition.Methods.Add(ctor);
        cDefinition.Methods.Add(_cInit);

        mainType.NestedTypes.Add(cDefinition);

        var moduleInitializerAttributeCtor = ResolveMethod("System.Runtime.CompilerServices.ModuleInitializerAttribute", ".ctor", []);
        var attr = new CustomAttribute(moduleInitializerAttributeCtor);

        _init = new MethodDefinition(
            "<>Init",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static,
            _specialTypes[SpecialType.Void]
        );

        _init.CustomAttributes.Add(new CustomAttribute(_belteCompilerGeneratedAttributeCtor));
        _init.CustomAttributes.Add(attr);

        mainType.Methods.Add(_init);
    }

    private void EmitAssemblyResolver(MethodDefinition mainMethod) {
        // TODO Maybe instead of finding dlls from compiler, embed and extract them?
        var cctorBuilder = new CecilILBuilder(null, this, _cctor);
        var cctorIL = cctorBuilder.iLProcessor;

        cctorIL.Emit(OpCodes.Newobj, _ctor);
        cctorIL.Emit(OpCodes.Stsfld, _c9);
        cctorIL.Emit(OpCodes.Ret);

        cctorBuilder.Finish();

        var ctorBuilder = new CecilILBuilder(null, this, _ctor);
        var ctorIL = ctorBuilder.iLProcessor;

        ctorIL.Emit(OpCodes.Ldarg_0);
        ctorIL.Emit(OpCodes.Call, ResolveMethod("System.Object", ".ctor", []));
        ctorIL.Emit(OpCodes.Ret);

        ctorBuilder.Finish();

        var cBuilder = new CecilILBuilder(null, this, _cInit);

        cBuilder.AllocateSlot(_specialTypes[SpecialType.String], LocalSlotConstraints.None);
        cBuilder.AllocateSlot(_specialTypes[SpecialType.String], LocalSlotConstraints.None);

        var cIL = cBuilder.iLProcessor;
        var ret = new object();

        cIL.Emit(OpCodes.Ldarg_2);
        cIL.Emit(OpCodes.Callvirt, ResolveMethod("System.ResolveEventArgs", "get_Name", []));
        cIL.Emit(OpCodes.Newobj, ResolveMethod("System.Reflection.AssemblyName", ".ctor", ["System.String"]));
        cIL.Emit(OpCodes.Call, ResolveMethod("System.Reflection.AssemblyName", "get_Name", []));
        cIL.Emit(OpCodes.Ldstr, ".dll");
        cIL.Emit(OpCodes.Call, ResolveMethod("System.String", "Concat", ["System.String", "System.String"]));
        cIL.Emit(OpCodes.Stloc_0);
        cIL.Emit(OpCodes.Ldstr, AppContext.BaseDirectory);
        cIL.Emit(OpCodes.Ldloc_0);
        cIL.Emit(OpCodes.Call, ResolveMethod("System.IO.Path", "Combine", ["System.String", "System.String"]));
        cIL.Emit(OpCodes.Stloc_1);
        cIL.Emit(OpCodes.Ldloc_1);
        cIL.Emit(OpCodes.Call, ResolveMethod("System.IO.File", "Exists", ["System.String"]));
        cBuilder.EmitBranch(CodeGeneration.OpCode.Brtrue_S, ret);
        cIL.Emit(OpCodes.Ldnull);
        cIL.Emit(OpCodes.Ret);
        cBuilder.MarkLabel(ret);
        cIL.Emit(OpCodes.Ldloc_1);
        cIL.Emit(OpCodes.Call, ResolveMethod("System.Reflection.Assembly", "LoadFrom", ["System.String"]));
        cIL.Emit(OpCodes.Ret);

        cBuilder.Finish();

        var amBuilder = new CecilILBuilder(null, this, _init);
        var amIL = amBuilder.iLProcessor;

        var endAM = new object();

        amIL.Emit(OpCodes.Call, ResolveMethod("System.AppDomain", "get_CurrentDomain", []));
        amIL.Emit(OpCodes.Ldsfld, _c9__0_0);
        amIL.Emit(OpCodes.Dup);
        amBuilder.EmitBranch(CodeGeneration.OpCode.Brtrue_S, endAM);
        amIL.Emit(OpCodes.Pop);
        amIL.Emit(OpCodes.Ldsfld, _c9);
        amIL.Emit(OpCodes.Ldftn, _cInit);
        amIL.Emit(OpCodes.Newobj, ResolveMethod("System.ResolveEventHandler", ".ctor", ["System.Object", "System.IntPtr"]));
        amIL.Emit(OpCodes.Dup);
        amIL.Emit(OpCodes.Stsfld, _c9__0_0);
        amBuilder.MarkLabel(endAM);
        amIL.Emit(OpCodes.Callvirt, ResolveMethod("System.AppDomain", "add_AssemblyResolve", ["System.ResolveEventHandler"]));
        amIL.Emit(OpCodes.Ret);

        amBuilder.Finish();

        lock (GlobalCecilLock) {
            var moduleType = _assemblyDefinition.MainModule.Types.First(t => t.Name == "<Module>");

            if (moduleType.Methods.Any(m => m.Name == ".cctor")) {
                var moduleCCtor = moduleType.Methods.First(m => m.Name == ".cctor");
                var il = moduleCCtor.Body.GetILProcessor();
                var finalRet = moduleCCtor.Body.Instructions.Last(i => i.OpCode == OpCodes.Ret);

                il.InsertBefore(finalRet, il.Create(OpCodes.Call, _init));
            } else {
                var cctor = new MethodDefinition(
                    ".cctor",
                    MethodAttributes.Private |
                    MethodAttributes.Static |
                    MethodAttributes.SpecialName |
                    MethodAttributes.RTSpecialName,
                    _specialTypes[SpecialType.Void]
                );

                moduleType.Methods.Add(cctor);

                var mBuilder = new CecilILBuilder(null, this, cctor);
                var mIL = mBuilder.iLProcessor;

                mIL.Emit(OpCodes.Call, _init);
                mIL.Emit(OpCodes.Ret);

                mBuilder.Finish();
            }
        }
    }

    private TypeDefinition ResolveType(NamedTypeSymbol peTypeSymbol) {
        var stack = new Stack<NamedTypeSymbol>();
        var current = peTypeSymbol;

        while (current is not null) {
            stack.Push(current);
            current = (NamedTypeSymbol)(current.containingType as PENamedTypeSymbol) ??
                (current.containingType as MissingMetadataTypeSymbol);
        }

        var topType = stack.Pop();
        var displayName = topType.ToDisplayString(SymbolDisplayFormat.NetNamespaceQualifiedNameFormat);
        var currentFoundType = ResolveType(null, displayName);
        // _assemblyDefinition.MainModule.ImportReferenceThreadSafe(Resolve(currentFoundType));
        // TODO Resolving may be unnecessary here?
        // _assemblyDefinition.MainModule.ImportReferenceThreadSafe(currentFoundType);

        while (stack.Count > 0) {
            var nestedType = stack.Pop();
            currentFoundType = Resolve(currentFoundType).NestedTypes.First(t => t.Name == nestedType.name);
            // _assemblyDefinition.MainModule.ImportReferenceThreadSafe(Resolve(currentFoundType));
            // TODO Resolving may be unnecessary here?
            // _assemblyDefinition.MainModule.ImportReferenceThreadSafe(currentFoundType);
        }

        return currentFoundType;
    }

    private TypeReference ImportType(string metadataName) {
        return ImportType(displayName: null, metadataName);
    }

    private TypeReference ImportType(string displayName, string metadataName) {
        return _assemblyDefinition.MainModule.ImportReferenceThreadSafe(ResolveType(displayName, metadataName));
    }

    private TypeDefinition ResolveType(string name, string metadataName) {
        var foundTypes = new List<TypeDefinition>(1);

        for (var i = 0; i < _assemblies.Count; i++) {
            var modules = _assemblies[i].Modules;

            for (var j = 0; j < modules.Count; j++) {
                var types = modules[j].Types;

                for (var k = 0; k < types.Count; k++) {
                    var type = types[k];

                    if (type.FullName == metadataName)
                        foundTypes.Add(type);
                }
            }
        }

        // TODO Do we actually care about ambiguity
        if (foundTypes.Count >= 1) {
            // if (import)
            //     return Resolve(_assemblyDefinition.MainModule.ImportReferenceThreadSafe(foundTypes[0]));

            return foundTypes[0];
        } else if (foundTypes.Count == 0) {
            throw new BelteInternalException($"Required type not found: {name} ({metadataName})");
        } else {
            throw new BelteInternalException(
                $"Required type ambiguous: {name} ({metadataName}); found {foundTypes.Count} candidates"
            );
        }
    }

    private MethodReference ResolveMethod(PENamedTypeSymbol type, string methodName, string[] parameterTypeNames) {
        var foundType = ResolveType(type);

        if (TryResolveMethodCore([Resolve(foundType)], methodName, parameterTypeNames, out var methodRef1))
            return methodRef1;

        throw new BelteInternalException($"Required method not found: {foundType.Name} {methodName} {parameterTypeNames.Length}");
    }

    private MethodReference ResolveMethod(string typeName, string methodName, string[] parameterTypeNames) {
        var foundTypes = _assemblies
            .SelectMany(a => a.Modules)
            .SelectMany(m => m.Types)
            .Where(t => t.FullName == typeName)
            .ToArray();

        if (foundTypes.Length >= 1) {
            if (TryResolveMethodCore(foundTypes, methodName, parameterTypeNames, out var methodRef1))
                return methodRef1;

            throw new BelteInternalException($"Required method not found: {typeName} {methodName} {parameterTypeNames.Length}");
        }

        var foundType = _backupAssemblies
            .SelectMany(a => a.Modules)
            .SelectMany(m => m.Types)
            .Where(t => t.FullName == typeName)
            .ToArray()
            .FirstOrDefault()
                ?? throw new BelteInternalException($"Required type not found: {typeName}");

        if (TryResolveMethodCore([foundType], methodName, parameterTypeNames, out var methodRef2))
            return methodRef2;
        else
            throw new BelteInternalException($"Required method not found: {typeName} {methodName} {parameterTypeNames.Length}");
    }

    private bool TryResolveMethodCore(
        TypeDefinition[] foundTypes,
        string methodName,
        string[] parameterTypeNames,
        out MethodReference methodDefinition) {
        var foundType = foundTypes[0];
        var methods = foundType.Methods.Where(m => m.Name == methodName);

        foreach (var method in methods) {
            if (method.Parameters.Count != parameterTypeNames.Length)
                continue;

            var allParametersMatch = true;

            for (var i = 0; i < parameterTypeNames.Length; i++) {
                // ? We treat null as we don't want to check this one (used for unresolved type arguments)
                if (parameterTypeNames[i] is null)
                    continue;

                if (method.Parameters[i].ParameterType.FullName != parameterTypeNames[i]) {
                    allParametersMatch = false;
                    break;
                }
            }

            if (!allParametersMatch)
                continue;

            methodDefinition = _assemblyDefinition.MainModule.ImportReferenceThreadSafe(method);
            return true;
        }

        methodDefinition = null;
        return false;
    }

    private void ResolveTypes() {
        var builtInTypes = new List<(SpecialType type, string metadataName)>() {
            (SpecialType.Object, "System.Object"),
            (SpecialType.Any, "System.Object"),
            (SpecialType.Bool, "System.Boolean"),
            (SpecialType.WinBool, "System.Int32"),
            (SpecialType.Int, "System.Int64"),
            (SpecialType.String, "System.String"),
            (SpecialType.Decimal, "System.Double"),
            (SpecialType.Nullable, "System.Nullable`1"),
            (SpecialType.Void, "System.Void"),
            (SpecialType.Type, "System.Type"),
            (SpecialType.Char, "System.Char"),
            (SpecialType.Int8, "System.SByte"),
            (SpecialType.UInt8, "System.Byte"),
            (SpecialType.Int16, "System.Int16"),
            (SpecialType.UInt16, "System.UInt16"),
            (SpecialType.Int32, "System.Int32"),
            (SpecialType.UInt32, "System.UInt32"),
            (SpecialType.Int64, "System.Int64"),
            (SpecialType.UInt64, "System.UInt64"),
            (SpecialType.Float32, "System.Single"),
            (SpecialType.Float64, "System.Double"),
            (SpecialType.IntPtr, "System.IntPtr"),
            (SpecialType.UIntPtr, "System.UIntPtr"),
        };

        foreach (var (type, metadataName) in builtInTypes) {
            var typeReference = ImportType(CorLibrary.GetSpecialType(type).name, metadataName);
            _specialTypes.Add(type, typeReference);
        }

        NetTypeReference.Random = ImportType("System.Random");
        NetTypeReference.Nullable = ImportType("System.Nullable`1");
        NetTypeReference.ValueType = ImportType("System.ValueType");
        NetTypeReference.Enum = ImportType("System.Enum");

        NetTypeReference.ValueTuple_T1 = ImportType("System.ValueTuple`1");
        NetTypeReference.ValueTuple_T2 = ImportType("System.ValueTuple`2");
        NetTypeReference.ValueTuple_T3 = ImportType("System.ValueTuple`3");
        NetTypeReference.ValueTuple_T4 = ImportType("System.ValueTuple`4");
        NetTypeReference.ValueTuple_T5 = ImportType("System.ValueTuple`5");
        NetTypeReference.ValueTuple_T6 = ImportType("System.ValueTuple`6");
        NetTypeReference.ValueTuple_T7 = ImportType("System.ValueTuple`7");
        NetTypeReference.ValueTuple_TRest = ImportType("System.ValueTuple`8");
    }

    private MethodReference CheckStandardMap(MethodSymbol method) {
        var mapKey = LibraryHelpers.BuildMapKey(method);

        if (mapKey.StartsWith("ValueTuple_.ctor_"))
            return GetTupleCtor(method.containingType);

        return mapKey switch {
            "Nullable<>_.ctor_T" => GetNullableCtor(method.containingType.templateArguments[0].type.type),
            "Nullable<>_get_Value" => GetNullableValue(method.containingType.templateArguments[0].type.type),
            "Nullable<>_get_HasValue" => GetNullableHasValue(method.containingType.templateArguments[0].type.type),
            "Nullable<>_GetValueOrDefault" => GetNullableValueOrDefault(method.containingType.templateArguments[0].type.type),
            "Nullable<>_GetValueOrDefault_T" => GetNullableValueOrDefaultT(method.containingType.templateArguments[0].type.type),
            _ => _stlMap[mapKey],
        };
    }

    private void ResolveMethods() {
        NetMethodReference.Object_Equals_OO = ResolveMethod("System.Object", "Equals", ["System.Object", "System.Object"]);
        NetMethodReference.Object_ToString = ResolveMethod("System.Object", "ToString", []);
        NetMethodReference.Enum_ToString = ResolveMethod("System.Enum", "ToString", []);
        NetMethodReference.String_Concat_SS = ResolveMethod("System.String", "Concat", ["System.String", "System.String"]);
        NetMethodReference.String_Concat_SSS = ResolveMethod("System.String", "Concat", ["System.String", "System.String", "System.String"]);
        NetMethodReference.String_Concat_SSSS = ResolveMethod("System.String", "Concat", ["System.String", "System.String", "System.String", "System.String"]);
        NetMethodReference.String_Concat_A = ResolveMethod("System.String", "Concat", ["System.String[]"]);
        NetMethodReference.String_Equality_SS = ResolveMethod("System.String", "op_Equality", ["System.String", "System.String"]);
        NetMethodReference.String_get_Chars_I = ResolveMethod("System.String", "get_Chars", ["System.Int32"]);
        NetMethodReference.Convert_ToBoolean_S = ResolveMethod("System.Convert", "ToBoolean", ["System.String"]);
        NetMethodReference.Convert_ToBoolean_I32 = ResolveMethod("System.Convert", "ToBoolean", ["System.Int32"]);
        NetMethodReference.Convert_ToInt64_S = ResolveMethod("System.Convert", "ToInt64", ["System.String"]);
        NetMethodReference.Convert_ToInt64_D = ResolveMethod("System.Convert", "ToInt64", ["System.Double"]);
        NetMethodReference.Convert_ToDouble_S = ResolveMethod("System.Convert", "ToDouble", ["System.String"]);
        NetMethodReference.Convert_ToDouble_I = ResolveMethod("System.Convert", "ToDouble", ["System.Int64"]);
        NetMethodReference.Convert_ToString_I = ResolveMethod("System.Convert", "ToString", ["System.Int64"]);
        NetMethodReference.Convert_ToString_D = ResolveMethod("System.Convert", "ToString", ["System.Double"]);
        NetMethodReference.Convert_ToInt32_S = ResolveMethod("System.Convert", "ToInt32", ["System.String"]);
        NetMethodReference.Convert_ToInt32_B = ResolveMethod("System.Convert", "ToInt32", ["System.Boolean"]);
        NetMethodReference.Convert_ToChar_S = ResolveMethod("System.Convert", "ToChar", ["System.String"]);
        NetMethodReference.Convert_ToByte_S = ResolveMethod("System.Convert", "ToByte", ["System.String"]);
        NetMethodReference.Convert_ToUInt16_S = ResolveMethod("System.Convert", "ToUInt16", ["System.String"]);
        NetMethodReference.Convert_ToUInt32_S = ResolveMethod("System.Convert", "ToUInt32", ["System.String"]);
        NetMethodReference.Convert_ToUInt64_S = ResolveMethod("System.Convert", "ToUInt64", ["System.String"]);
        NetMethodReference.Convert_ToSByte_S = ResolveMethod("System.Convert", "ToSByte", ["System.String"]);
        NetMethodReference.Convert_ToInt16_S = ResolveMethod("System.Convert", "ToInt16", ["System.String"]);
        NetMethodReference.Convert_ToSingle_S = ResolveMethod("System.Convert", "ToSingle", ["System.String"]);
        NetMethodReference.Convert_ToString_B = ResolveMethod("System.Convert", "ToString", ["System.Boolean"]);
        NetMethodReference.Convert_ToString_C = ResolveMethod("System.Convert", "ToString", ["System.Char"]);
        NetMethodReference.Convert_ToString_UI8 = ResolveMethod("System.Convert", "ToString", ["System.Byte"]);
        NetMethodReference.Convert_ToString_UI16 = ResolveMethod("System.Convert", "ToString", ["System.UInt16"]);
        NetMethodReference.Convert_ToString_UI32 = ResolveMethod("System.Convert", "ToString", ["System.UInt32"]);
        NetMethodReference.Convert_ToString_UI64 = ResolveMethod("System.Convert", "ToString", ["System.UInt64"]);
        NetMethodReference.Convert_ToString_I8 = ResolveMethod("System.Convert", "ToString", ["System.SByte"]);
        NetMethodReference.Convert_ToString_I16 = ResolveMethod("System.Convert", "ToString", ["System.Int16"]);
        NetMethodReference.Convert_ToString_I32 = ResolveMethod("System.Convert", "ToString", ["System.Int32"]);
        NetMethodReference.Convert_ToString_I64 = ResolveMethod("System.Convert", "ToString", ["System.Int64"]);
        NetMethodReference.Convert_ToString_F32 = ResolveMethod("System.Convert", "ToString", ["System.Single"]);
        NetMethodReference.Convert_ToString_F64 = ResolveMethod("System.Convert", "ToString", ["System.Double"]);
        NetMethodReference.Random_ctor = ResolveMethod("System.Random", ".ctor", []);
        NetMethodReference.Random_NextInt64_I = ResolveMethod("System.Random", "NextInt64", ["System.Int64"]);
        NetMethodReference.Random_NextDouble = ResolveMethod("System.Random", "NextDouble", []);
        NetMethodReference.Nullable_ctor = ResolveMethod("System.Nullable`1", ".ctor", ["T"]);
        NetMethodReference.Nullable_Value = ResolveMethod("System.Nullable`1", "get_Value", []);
        NetMethodReference.Nullable_HasValue = ResolveMethod("System.Nullable`1", "get_HasValue", []);
        NetMethodReference.Nullable_GetValueOrDefault = ResolveMethod("System.Nullable`1", "GetValueOrDefault", []);
        NetMethodReference.Nullable_GetValueOrDefault_T = ResolveMethod("System.Nullable`1", "GetValueOrDefault", ["T"]);
        NetMethodReference.Type_GetTypeFromHandle = ResolveMethod("System.Type", "GetTypeFromHandle", ["System.RuntimeTypeHandle"]);
        NetMethodReference.NullReferenceException_ctor = ResolveMethod("System.NullReferenceException", ".ctor", []);
        NetMethodReference.NullConditionException_ctor = ResolveMethod("Belte.Runtime.NullConditionException", ".ctor", []);
        NetMethodReference.UnreachableException_ctor = ResolveMethod("System.Diagnostics.UnreachableException", ".ctor", []);
        NetMethodReference.LowLevel_Sort = ResolveMethod("Belte.Runtime.Utilities", "Sort", ["T[]"]);
        NetMethodReference.LowLevel_Length = ResolveMethod("Belte.Runtime.Utilities", "Length", ["T[]"]);
        NetMethodReference.AssertNull = ResolveMethod("Belte.Runtime.Utilities", "AssertNull", ["T"]);
        NetMethodReference.Marshal_SizeOf = ResolveMethod("System.Runtime.InteropServices.Marshal", "SizeOf", []);
        NetMethodReference.Unsafe_BitCast = ResolveMethod("System.Runtime.CompilerServices.Unsafe", "BitCast", ["TFrom"]);
        NetMethodReference.ValueTuple_T1_ctor = ResolveMethod("System.ValueTuple`1", ".ctor", ["T1"]);
        NetMethodReference.ValueTuple_T2_ctor = ResolveMethod("System.ValueTuple`2", ".ctor", ["T1", "T2"]);
        NetMethodReference.ValueTuple_T3_ctor = ResolveMethod("System.ValueTuple`3", ".ctor", ["T1", "T2", "T3"]);
        NetMethodReference.ValueTuple_T4_ctor = ResolveMethod("System.ValueTuple`4", ".ctor", ["T1", "T2", "T3", "T4"]);
        NetMethodReference.ValueTuple_T5_ctor = ResolveMethod("System.ValueTuple`5", ".ctor", ["T1", "T2", "T3", "T4", "T5"]);
        NetMethodReference.ValueTuple_T6_ctor = ResolveMethod("System.ValueTuple`6", ".ctor", ["T1", "T2", "T3", "T4", "T5", "T6"]);
        NetMethodReference.ValueTuple_T7_ctor = ResolveMethod("System.ValueTuple`7", ".ctor", ["T1", "T2", "T3", "T4", "T5", "T6", "T7"]);
        NetMethodReference.ValueTuple_TRest_ctor = ResolveMethod("System.ValueTuple`8", ".ctor", ["T1", "T2", "T3", "T4", "T5", "T6", "T7", "TRest"]);
        NetMethodReference.Array_Empty = ResolveMethod("System.Array", "Empty", []);
        NetMethodReference.Array_Fill = ResolveMethod("System.Array", "Fill", ["T[]", "T"]);
    }

    private void GenerateSTLMap() {
        if (_program.compilation.options.noStdLib) {
            _stlMap = new Dictionary<string, MethodReference>() {
                { "Object<>_.ctor", ResolveMethod("System.Object", ".ctor", []) },
                { "Object<>_ToString", ResolveMethod("System.Object", "ToString", []) },
                { "Object<>_Equals_O?", ResolveMethod("System.Object", "Equals", ["System.Object"]) },
                { "Object<>_GetHashCode", ResolveMethod("System.Object", "GetHashCode", []) },
                { "Object<>_Finalize", ResolveMethod("System.Object", "Finalize", []) },
                { "Exception_.ctor", ResolveMethod("System.Exception", ".ctor", []) },
                { "Exception_.ctor_S?", ResolveMethod("System.Exception", ".ctor", ["System.String"]) },
                { "LowLevel_GetHashCode_O", ResolveMethod("Belte.Runtime.Utilities", "GetHashCode", ["System.Object"]) },
                { "LowLevel_CombineHashCode_I4I4", ResolveMethod("Belte.Runtime.Utilities", "CombineHashCode", ["System.Int32", "System.Int32"]) },
                { "LowLevel_GetTypeName_O", ResolveMethod("Belte.Runtime.Utilities", "GetTypeName", ["System.Object"]) },
                { "LowLevel_ThrowNullConditionException", ResolveMethod("Belte.Runtime.ThrowHelper", "ThrowNullConditionException", []) },
                { "LowLevel_CreateLPCSTR_S", ResolveMethod("Belte.Runtime.Utilities", "CreateLPCSTR", ["System.String"]) },
                { "LowLevel_CreateLPCSTR_UTF_S", ResolveMethod("Belte.Runtime.Utilities", "CreateLPCSTR_UTF", ["System.String"]) },
                { "LowLevel_CreateLPCWSTR_S", ResolveMethod("Belte.Runtime.Utilities", "CreateLPCWSTR", ["System.String"]) },
                { "LowLevel_FreeLPCSTR_U*", ResolveMethod("Belte.Runtime.Utilities", "FreeLPCSTR", ["System.Byte*"]) },
                { "LowLevel_FreeLPCWSTR_C*", ResolveMethod("Belte.Runtime.Utilities", "FreeLPCWSTR", ["System.Char*"]) },
                { "LowLevel_ReadLPCSTR_U*", ResolveMethod("Belte.Runtime.Utilities", "ReadLPCSTR", ["System.Byte*"]) },
                { "LowLevel_ReadLPCWSTR_C*", ResolveMethod("Belte.Runtime.Utilities", "ReadLPCWSTR", ["System.Char*"]) },
                { "LowLevel_GetGCPtr_O", ResolveMethod("Belte.Runtime.Utilities", "GetGCPtr", ["System.Object"]) },
                { "LowLevel_FreeGCHandle_V*", ResolveMethod("Belte.Runtime.Utilities", "FreeGCHandle", ["System.Void*"]) },
                { "LowLevel_GetObject_V*", ResolveMethod("Belte.Runtime.Utilities", "GetObject", ["System.Void*"]) },
                { "LowLevel_IsLittleEndian", ResolveMethod("Belte.Runtime.Utilities", "IsLittleEndian", []) },
                { "LowLevel_ReverseEndianness_I4", ResolveMethod("System.Buffers.Binary.BinaryPrimitives", "ReverseEndianness", ["System.Int32"]) },
            };
        } else {
            _stlMap = new Dictionary<string, MethodReference>() {
                { "Object<>_.ctor", ResolveMethod("System.Object", ".ctor", []) },
                { "Object<>_ToString", ResolveMethod("System.Object", "ToString", []) },
                { "Object<>_Equals_O?", ResolveMethod("System.Object", "Equals", ["System.Object"]) },
                { "Object<>_GetHashCode", ResolveMethod("System.Object", "GetHashCode", []) },
                { "Object<>_Finalize", ResolveMethod("System.Object", "Finalize", []) },
                { "Exception_.ctor", ResolveMethod("System.Exception", ".ctor", []) },
                { "Exception_.ctor_S?", ResolveMethod("System.Exception", ".ctor", ["System.String"]) },
                { "Console_Clear", ResolveMethod("System.Console", "Clear", []) },
                { "Console_GetWidth", ResolveMethod("Belte.Runtime.Console", "GetWidth", []) },
                { "Console_GetHeight", ResolveMethod("Belte.Runtime.Console", "GetHeight", []) },
                { "Console_Print_S?", ResolveMethod("System.Console", "Write", ["System.String"]) },
                { "Console_Print_A?", ResolveMethod("System.Console", "Write", ["System.Object"]) },
                { "Console_Print_[?", ResolveMethod("System.Console", "Write", ["System.Char[]"]) },
                { "Console_PrintLine", ResolveMethod("System.Console", "WriteLine", []) },
                { "Console_PrintLine_S?", ResolveMethod("System.Console", "WriteLine", ["System.String"]) },
                { "Console_PrintLine_A?", ResolveMethod("System.Console", "WriteLine", ["System.Object"]) },
                { "Console_PrintLine_[?", ResolveMethod("System.Console", "WriteLine", ["System.Char[]"]) },
                { "Console_Input", ResolveMethod("System.Console", "ReadLine", []) },
                { "Console_ResetColor", ResolveMethod("System.Console", "ResetColor", []) },
                { "Console_SetForegroundColor_I", ResolveMethod("Belte.Runtime.Console", "SetForegroundColor", ["System.Int64"]) },
                { "Console_SetBackgroundColor_I", ResolveMethod("Belte.Runtime.Console", "SetBackgroundColor", ["System.Int64"]) },
                { "Console_SetCursorPosition_I?I?", ResolveMethod("Belte.Runtime.Console", "SetCursorPosition", ["System.Nullable`1<System.Int64>", "System.Nullable`1<System.Int64>"]) },
                { "Console_SetCursorVisibility_B", ResolveMethod("Belte.Runtime.Console", "SetCursorVisibility", ["System.Boolean"]) },
                { "Directory_Create_S", ResolveMethod("Belte.Runtime.Utilities", "CreateDirectory", ["System.String"]) },
                { "Directory_Delete_S", ResolveMethod("Belte.Runtime.Utilities", "DeleteDirectory", ["System.String"]) },
                { "Directory_Exists_S", ResolveMethod("System.IO.Directory", "Exists", ["System.String"]) },
                { "Directory_GetCurrentDirectory", ResolveMethod("System.IO.Directory", "GetCurrentDirectory", []) },
                { "File_AppendText_SS", ResolveMethod("System.IO.File", "AppendAllText", ["System.String", "System.String"]) },
                { "File_Create_S", ResolveMethod("System.IO.File", "Create", ["System.String"]) },
                { "File_Copy_S", ResolveMethod("System.IO.File", "Copy", ["System.String", "System.String"]) },
                { "File_Delete_S", ResolveMethod("System.IO.File", "Delete", ["System.String"]) },
                { "File_Exists_S", ResolveMethod("System.IO.File", "Exists", ["System.String"]) },
                { "File_ReadText_S", ResolveMethod("System.IO.File", "ReadAllText", ["System.String"]) },
                { "File_WriteText_SS", ResolveMethod("System.IO.File", "WriteAllText", ["System.String", "System.String"]) },
                { "String_Ascii_S", ResolveMethod("Belte.Runtime.Utilities", "Ascii", ["System.String"]) },
                { "String_Char_I", ResolveMethod("Belte.Runtime.Utilities", "Char", ["System.Int64"]) },
                { "String_Split_SS", ResolveMethod("Belte.Runtime.Utilities", "Split", ["System.String", "System.String"]) },
                { "String_Length_S", ResolveMethod("Belte.Runtime.Utilities", "StringLength", ["System.String"]) },
                { "String_IndexOf_SC", ResolveMethod("Belte.Runtime.Utilities", "StringIndexOf", ["System.String", "System.Char"]) },
                { "String_IsNullOrWhiteSpace_S?", ResolveMethod("System.String", "IsNullOrWhiteSpace", ["System.String"]) },
                { "String_IsNullOrWhiteSpace_C?", ResolveMethod("Belte.Runtime.Utilities", "IsNullOrWhiteSpace", ["System.Nullable`1<System.Char>"]) },
                { "String_IsDigit_C?", ResolveMethod("Belte.Runtime.Utilities", "IsDigit", ["System.Nullable`1<System.Char>"]) },
                { "String_Substring_SI?I?", ResolveMethod("Belte.Runtime.Utilities", "Substring", ["System.String", "System.Nullable`1<System.Int64>", "System.Nullable`1<System.Int64>"]) },
                { "String_PadLeft_SCI", ResolveMethod("Belte.Runtime.Utilities", "StringPadLeft", ["System.String", "System.Char", "System.Int64"]) },
                { "String_PadRight_SCI", ResolveMethod("Belte.Runtime.Utilities", "StringPadRight", ["System.String", "System.Char", "System.Int64"]) },
                { "String_Replace_SSS", ResolveMethod("Belte.Runtime.Utilities", "StringReplace", ["System.String", "System.String", "System.String"]) },
                { "String_Trim_S", ResolveMethod("Belte.Runtime.Utilities", "StringTrim", ["System.String"]) },
                { "String_Trim_S[", ResolveMethod("Belte.Runtime.Utilities", "StringTrim", ["System.String", "System.Char[]"]) },
                { "String_TrimStart_S", ResolveMethod("Belte.Runtime.Utilities", "StringTrimStart", ["System.String"]) },
                { "String_TrimStart_S[", ResolveMethod("Belte.Runtime.Utilities", "StringTrimStart", ["System.String", "System.Char[]"]) },
                { "String_TrimEnd_S", ResolveMethod("Belte.Runtime.Utilities", "StringTrimEnd", ["System.String"]) },
                { "String_TrimEnd_S[", ResolveMethod("Belte.Runtime.Utilities", "StringTrimEnd", ["System.String", "System.Char[]"]) },
                { "String_Contains_SS", ResolveMethod("Belte.Runtime.Utilities", "StringContains", ["System.String", "System.String"]) },
                { "Int_Parse_S?", ResolveMethod("Belte.Runtime.Utilities", "IntParse", ["System.String"]) },
                { "Int_ToString_IS", ResolveMethod("Belte.Runtime.Utilities", "IntToString", ["System.Int64", "System.String"]) },
                { "Decimal_IsNaN_F4", ResolveMethod("System.Single", "IsNaN", ["System.Single"]) },
                { "Decimal_IsPosInfinity_F4", ResolveMethod("System.Single", "IsPositiveInfinity", ["System.Single"]) },
                { "Decimal_IsNegInfinity_F4", ResolveMethod("System.Single", "IsNegativeInfinity", ["System.Single"]) },
                { "Decimal_IsInfinity_F4", ResolveMethod("System.Single", "IsInfinity", ["System.Single"]) },
                { "Decimal_IsNaN_F8", ResolveMethod("System.Double", "IsNaN", ["System.Double"]) },
                { "Decimal_IsPosInfinity_F8", ResolveMethod("System.Double", "IsPositiveInfinity", ["System.Double"]) },
                { "Decimal_IsNegInfinity_F8", ResolveMethod("System.Double", "IsNegativeInfinity", ["System.Double"]) },
                { "Decimal_IsInfinity_F8", ResolveMethod("System.Double", "IsInfinity", ["System.Double"]) },
                { "Decimal_Parse_S?", ResolveMethod("Belte.Runtime.Utilities", "DecimalParse", ["System.String"]) },
                { "Decimal_ToString_DS", ResolveMethod("Belte.Runtime.Utilities", "DecimalToString", ["System.Double", "System.String"]) },
                { "LowLevel_GetHashCode_O", ResolveMethod("Belte.Runtime.Utilities", "GetHashCode", ["System.Object"]) },
                { "LowLevel_GetTypeName_O", ResolveMethod("Belte.Runtime.Utilities", "GetTypeName", ["System.Object"]) },
                { "LowLevel_ThrowNullConditionException", ResolveMethod("Belte.Runtime.ThrowHelper", "ThrowNullConditionException", []) },
                { "LowLevel_CreateLPCSTR_S", ResolveMethod("Belte.Runtime.Utilities", "CreateLPCSTR", ["System.String"]) },
                { "LowLevel_CreateLPCSTR_UTF_S", ResolveMethod("Belte.Runtime.Utilities", "CreateLPCSTR_UTF", ["System.String"]) },
                { "LowLevel_CreateLPCWSTR_S", ResolveMethod("Belte.Runtime.Utilities", "CreateLPCWSTR", ["System.String"]) },
                { "LowLevel_FreeLPCSTR_U*", ResolveMethod("Belte.Runtime.Utilities", "FreeLPCSTR", ["System.Byte*"]) },
                { "LowLevel_FreeLPCWSTR_C*", ResolveMethod("Belte.Runtime.Utilities", "FreeLPCWSTR", ["System.Char*"]) },
                { "LowLevel_ReadLPCSTR_U*", ResolveMethod("Belte.Runtime.Utilities", "ReadLPCSTR", ["System.Byte*"]) },
                { "LowLevel_ReadLPCWSTR_C*", ResolveMethod("Belte.Runtime.Utilities", "ReadLPCWSTR", ["System.Char*"]) },
                { "LowLevel_GetGCPtr_O", ResolveMethod("Belte.Runtime.Utilities", "GetGCPtr", ["System.Object"]) },
                { "LowLevel_FreeGCHandle_V*", ResolveMethod("Belte.Runtime.Utilities", "FreeGCHandle", ["System.Void*"]) },
                { "LowLevel_GetObject_V*", ResolveMethod("Belte.Runtime.Utilities", "GetObject", ["System.Void*"]) },
                { "LowLevel_IsLittleEndian", ResolveMethod("Belte.Runtime.Utilities", "IsLittleEndian", []) },
                { "LowLevel_ReverseEndianness_I4", ResolveMethod("System.Buffers.Binary.BinaryPrimitives", "ReverseEndianness", ["System.Int32"]) },
                { "HashCode_Combine_I4I4", ResolveMethod("Belte.Runtime.Utilities", "HashCodeCombine", ["System.Int32", "System.Int32"]) },
                { "HashCode_Combine_I4I4I4", ResolveMethod("Belte.Runtime.Utilities", "HashCodeCombine", ["System.Int32", "System.Int32", "System.Int32"]) },
                { "HashCode_Combine_I4I4I4I4", ResolveMethod("Belte.Runtime.Utilities", "HashCodeCombine", ["System.Int32", "System.Int32", "System.Int32", "System.Int32"]) },
                { "HashCode_Combine_I4I4I4I4I4", ResolveMethod("Belte.Runtime.Utilities", "HashCodeCombine", ["System.Int32", "System.Int32", "System.Int32", "System.Int32", "System.Int32"]) },
                { "HashCode_Combine_I4I4I4I4I4I4", ResolveMethod("Belte.Runtime.Utilities", "HashCodeCombine", ["System.Int32", "System.Int32", "System.Int32", "System.Int32", "System.Int32", "System.Int32"]) },
                { "HashCode_Combine_I4I4I4I4I4I4I4", ResolveMethod("Belte.Runtime.Utilities", "HashCodeCombine", ["System.Int32", "System.Int32", "System.Int32", "System.Int32", "System.Int32", "System.Int32", "System.Int32"]) },
                { "HashCode_Combine_I4I4I4I4I4I4I4I4", ResolveMethod("Belte.Runtime.Utilities", "HashCodeCombine", ["System.Int32", "System.Int32", "System.Int32", "System.Int32", "System.Int32", "System.Int32", "System.Int32", "System.Int32"]) },
                { "Time_Now", ResolveMethod("Belte.Runtime.Utilities", "TimeNow", []) },
                { "Time_Sleep_I", ResolveMethod("Belte.Runtime.Utilities", "TimeSleep", ["System.Int64"]) },
                { "Math_Abs_D?", ResolveMethod("Belte.Runtime.Math", "Abs", ["System.Nullable`1<System.Double>"]) },
                { "Math_Abs_D", ResolveMethod("System.Math", "Abs", ["System.Double"]) },
                { "Math_Abs_I?", ResolveMethod("Belte.Runtime.Math", "Abs", ["System.Nullable`1<System.Int64>"]) },
                { "Math_Abs_I", ResolveMethod("System.Math", "Abs", ["System.Int64"]) },
                { "Math_Acos_D?", ResolveMethod("Belte.Runtime.Math", "Acos", ["System.Nullable`1<System.Double>"]) },
                { "Math_Acos_D", ResolveMethod("System.Math", "Acos", ["System.Double"]) },
                { "Math_Acosh_D?", ResolveMethod("Belte.Runtime.Math", "Acosh", ["System.Nullable`1<System.Double>"]) },
                { "Math_Acosh_D", ResolveMethod("System.Math", "Acosh", ["System.Double"]) },
                { "Math_Asin_D?", ResolveMethod("Belte.Runtime.Math", "Asin", ["System.Nullable`1<System.Double>"]) },
                { "Math_Asin_D", ResolveMethod("System.Math", "Asin", ["System.Double"]) },
                { "Math_Asinh_D?", ResolveMethod("Belte.Runtime.Math", "Asinh", ["System.Nullable`1<System.Double>"]) },
                { "Math_Asinh_D", ResolveMethod("System.Math", "Asinh", ["System.Double"]) },
                { "Math_Atan_D?", ResolveMethod("Belte.Runtime.Math", "Atan", ["System.Nullable`1<System.Double>"]) },
                { "Math_Atan_D", ResolveMethod("System.Math", "Atan", ["System.Double"]) },
                { "Math_Atanh_D?", ResolveMethod("Belte.Runtime.Math", "Atanh", ["System.Nullable`1<System.Double>"]) },
                { "Math_Atanh_D", ResolveMethod("System.Math", "Atanh", ["System.Double"]) },
                { "Math_Ceiling_D?", ResolveMethod("Belte.Runtime.Math", "Ceiling", ["System.Nullable`1<System.Double>"]) },
                { "Math_Ceiling_D", ResolveMethod("System.Math", "Ceiling", ["System.Double"]) },
                { "Math_Clamp_D?D?D?", ResolveMethod("Belte.Runtime.Math", "Clamp", ["System.Nullable`1<System.Double>", "System.Nullable`1<System.Double>", "System.Nullable`1<System.Double>"]) },
                { "Math_Clamp_DDD", ResolveMethod("System.Math", "Clamp", ["System.Double","System.Double", "System.Double"]) },
                { "Math_Clamp_F4?F4?F4?", ResolveMethod("Belte.Runtime.Math", "Clamp", ["System.Nullable`1<System.Single>", "System.Nullable`1<System.Single>", "System.Nullable`1<System.Single>"]) },
                { "Math_Clamp_F4F4F4", ResolveMethod("System.Math", "Clamp", ["System.Single","System.Single", "System.Single"]) },
                { "Math_Clamp_I?I?I?", ResolveMethod("Belte.Runtime.Math", "Clamp", ["System.Nullable`1<System.Int64>", "System.Nullable`1<System.Int64>", "System.Nullable`1<System.Int64>"]) },
                { "Math_Clamp_III", ResolveMethod("System.Math", "Clamp", ["System.Int64","System.Int64", "System.Int64"]) },
                { "Math_Clamp_U8?U8?U8?", ResolveMethod("Belte.Runtime.Math", "Clamp", ["System.Nullable`1<System.UInt64>", "System.Nullable`1<System.UInt64>", "System.Nullable`1<System.UInt64>"]) },
                { "Math_Clamp_U8U8U8", ResolveMethod("System.Math", "Clamp", ["System.UInt64","System.UInt64", "System.UInt64"]) },
                { "Math_Clamp_I4?I4?I4?", ResolveMethod("Belte.Runtime.Math", "Clamp", ["System.Nullable`1<System.Int32>", "System.Nullable`1<System.Int32>", "System.Nullable`1<System.Int32>"]) },
                { "Math_Clamp_I4I4I4", ResolveMethod("System.Math", "Clamp", ["System.Int32","System.Int32", "System.Int32"]) },
                { "Math_Clamp_U4?U4?U4?", ResolveMethod("Belte.Runtime.Math", "Clamp", ["System.Nullable`1<System.UInt32>", "System.Nullable`1<System.UInt32>", "System.Nullable`1<System.UInt32>"]) },
                { "Math_Clamp_U4U4U4", ResolveMethod("System.Math", "Clamp", ["System.UInt32","System.UInt32", "System.UInt32"]) },
                { "Math_Clamp_I2?I2?I2?", ResolveMethod("Belte.Runtime.Math", "Clamp", ["System.Nullable`1<System.Int16>", "System.Nullable`1<System.Int16>", "System.Nullable`1<System.Int16>"]) },
                { "Math_Clamp_I2I2I2", ResolveMethod("System.Math", "Clamp", ["System.Int16","System.Int16", "System.Int16"]) },
                { "Math_Clamp_U2?U2?U2?", ResolveMethod("Belte.Runtime.Math", "Clamp", ["System.Nullable`1<System.UInt16>", "System.Nullable`1<System.UInt16>", "System.Nullable`1<System.UInt16>"]) },
                { "Math_Clamp_U2U2U2", ResolveMethod("System.Math", "Clamp", ["System.UInt16","System.UInt16", "System.UInt16"]) },
                { "Math_Clamp_I1?I1?I1?", ResolveMethod("Belte.Runtime.Math", "Clamp", ["System.Nullable`1<System.SByte>", "System.Nullable`1<System.SByte>", "System.Nullable`1<System.SByte>"]) },
                { "Math_Clamp_I1I1I1", ResolveMethod("System.Math", "Clamp", ["System.SByte","System.SByte", "System.SByte"]) },
                { "Math_Clamp_U1?U1?U1?", ResolveMethod("Belte.Runtime.Math", "Clamp", ["System.Nullable`1<System.Byte>", "System.Nullable`1<System.Byte>", "System.Nullable`1<System.Byte>"]) },
                { "Math_Clamp_U1U1U1", ResolveMethod("System.Math", "Clamp", ["System.Byte","System.Byte", "System.Byte"]) },
                { "Math_Clamp_C?C?C?", ResolveMethod("Belte.Runtime.Math", "Clamp", ["System.Nullable`1<System.Char>", "System.Nullable`1<System.Char>", "System.Nullable`1<System.Char>"]) },
                { "Math_Clamp_CCC", ResolveMethod("Belte.Runtime.Math", "Clamp", ["System.Char","System.Char", "System.Char"]) },
                { "Math_Cos_D?", ResolveMethod("Belte.Runtime.Math", "Cos", ["System.Nullable`1<System.Double>"]) },
                { "Math_Cos_D", ResolveMethod("System.Math", "Cos", ["System.Double"]) },
                { "Math_Cosh_D?", ResolveMethod("Belte.Runtime.Math", "Cosh", ["System.Nullable`1<System.Double>"]) },
                { "Math_Cosh_D", ResolveMethod("System.Math", "Cosh", ["System.Double"]) },
                { "Math_Exp_D?", ResolveMethod("Belte.Runtime.Math", "Exp", ["System.Nullable`1<System.Double>"]) },
                { "Math_Exp_D", ResolveMethod("System.Math", "Exp", ["System.Double"]) },
                { "Math_Floor_D?", ResolveMethod("Belte.Runtime.Math", "Floor", ["System.Nullable`1<System.Double>"]) },
                { "Math_Floor_D", ResolveMethod("System.Math", "Floor", ["System.Double"]) },
                { "Math_Lerp_D?D?D?", ResolveMethod("Belte.Runtime.Math", "Lerp", ["System.Nullable`1<System.Double>", "System.Nullable`1<System.Double>", "System.Nullable`1<System.Double>"]) },
                { "Math_Lerp_DDD", ResolveMethod("Belte.Runtime.Math", "Lerp", ["System.Double", "System.Double", "System.Double"]) },
                { "Math_Log_D?D?", ResolveMethod("Belte.Runtime.Math", "Log", ["System.Nullable`1<System.Double>", "System.Nullable`1<System.Double>"]) },
                { "Math_Log_DD", ResolveMethod("System.Math", "Log", ["System.Double", "System.Double"]) },
                { "Math_Log_D?", ResolveMethod("Belte.Runtime.Math", "Log", ["System.Nullable`1<System.Double>"]) },
                { "Math_Log_D", ResolveMethod("System.Math", "Log", ["System.Double"]) },
                { "Math_Max_D?D?", ResolveMethod("Belte.Runtime.Math", "Max", ["System.Nullable`1<System.Double>", "System.Nullable`1<System.Double>"]) },
                { "Math_Max_DD", ResolveMethod("System.Math", "Max", ["System.Double", "System.Double"]) },
                { "Math_Max_F4?F4?", ResolveMethod("Belte.Runtime.Math", "Max", ["System.Nullable`1<System.Single>", "System.Nullable`1<System.Single>"]) },
                { "Math_Max_F4F4", ResolveMethod("System.Math", "Max", ["System.Single", "System.Single"]) },
                { "Math_Max_I?I?", ResolveMethod("Belte.Runtime.Math", "Max", ["System.Nullable`1<System.Int64>", "System.Nullable`1<System.Int64>"]) },
                { "Math_Max_II", ResolveMethod("System.Math", "Max", ["System.Int64", "System.Int64"]) },
                { "Math_Max_I4?I4?", ResolveMethod("Belte.Runtime.Math", "Max", ["System.Nullable`1<System.Int32>", "System.Nullable`1<System.Int32>"]) },
                { "Math_Max_I4I4", ResolveMethod("System.Math", "Max", ["System.Int32", "System.Int32"]) },
                { "Math_Max_U8?U8?", ResolveMethod("Belte.Runtime.Math", "Max", ["System.Nullable`1<System.UInt64>", "System.Nullable`1<System.UInt64>"]) },
                { "Math_Max_U8U8", ResolveMethod("System.Math", "Max", ["System.UInt64", "System.UInt64"]) },
                { "Math_Max_U4?U4?", ResolveMethod("Belte.Runtime.Math", "Max", ["System.Nullable`1<System.UInt32>", "System.Nullable`1<System.UInt32>"]) },
                { "Math_Max_U4U4", ResolveMethod("System.Math", "Max", ["System.UInt32", "System.UInt32"]) },
                { "Math_Min_D?D?", ResolveMethod("Belte.Runtime.Math", "Min", ["System.Nullable`1<System.Double>", "System.Nullable`1<System.Double>"]) },
                { "Math_Min_DD", ResolveMethod("System.Math", "Min", ["System.Double", "System.Double"]) },
                { "Math_Min_F4?F4?", ResolveMethod("Belte.Runtime.Math", "Min", ["System.Nullable`1<System.Single>", "System.Nullable`1<System.Single>"]) },
                { "Math_Min_F4F4", ResolveMethod("System.Math", "Min", ["System.Single", "System.Single"]) },
                { "Math_Min_I?I?", ResolveMethod("Belte.Runtime.Math", "Min", ["System.Nullable`1<System.Int64>", "System.Nullable`1<System.Int64>"]) },
                { "Math_Min_II", ResolveMethod("System.Math", "Min", ["System.Int64", "System.Int64"]) },
                { "Math_Min_I4?I4?", ResolveMethod("Belte.Runtime.Math", "Min", ["System.Nullable`1<System.Int32>", "System.Nullable`1<System.Int32>"]) },
                { "Math_Min_I4I4", ResolveMethod("System.Math", "Min", ["System.Int32", "System.Int32"]) },
                { "Math_Min_U8?U8?", ResolveMethod("Belte.Runtime.Math", "Min", ["System.Nullable`1<System.UInt64>", "System.Nullable`1<System.UInt64>"]) },
                { "Math_Min_U8U8", ResolveMethod("System.Math", "Min", ["System.UInt64", "System.UInt64"]) },
                { "Math_Min_U4?U4?", ResolveMethod("Belte.Runtime.Math", "Min", ["System.Nullable`1<System.UInt32>", "System.Nullable`1<System.UInt32>"]) },
                { "Math_Min_U4U4", ResolveMethod("System.Math", "Min", ["System.UInt32", "System.UInt32"]) },
                { "Math_Pow_DD", ResolveMethod("System.Math", "Pow", ["System.Double", "System.Double"]) },
                { "Math_Pow_D?D?", ResolveMethod("Belte.Runtime.Math", "Pow", ["System.Nullable`1<System.Double>", "System.Nullable`1<System.Double>"]) },
                { "Math_Pow_II", ResolveMethod("Belte.Runtime.Math", "Pow", ["System.Int64", "System.Int64"]) },
                { "Math_Pow_I?I?", ResolveMethod("Belte.Runtime.Math", "Pow", ["System.Nullable`1<System.Int64>", "System.Nullable`1<System.Int64>"]) },
                { "Math_Round_D?", ResolveMethod("Belte.Runtime.Math", "Round", ["System.Nullable`1<System.Double>"]) },
                { "Math_Round_D", ResolveMethod("System.Math", "Round", ["System.Double"]) },
                { "Math_Sign_D?", ResolveMethod("Belte.Runtime.Math", "Sign", ["System.Nullable`1<System.Double>"]) },
                { "Math_Sign_D", ResolveMethod("System.Math", "Sign", ["System.Double"]) },
                { "Math_Sign_I?", ResolveMethod("Belte.Runtime.Math", "Sign", ["System.Nullable`1<System.Int64>"]) },
                { "Math_Sign_I", ResolveMethod("System.Math", "Sign", ["System.Int64"]) },
                { "Math_Sin_D?", ResolveMethod("Belte.Runtime.Math", "Sin", ["System.Nullable`1<System.Double>"]) },
                { "Math_Sin_D", ResolveMethod("System.Math", "Sin", ["System.Double"]) },
                { "Math_Sinh_D?", ResolveMethod("Belte.Runtime.Math", "Sinh", ["System.Nullable`1<System.Double>"]) },
                { "Math_Sinh_D", ResolveMethod("System.Math", "Sinh", ["System.Double"]) },
                { "Math_Sqrt_D?", ResolveMethod("Belte.Runtime.Math", "Sqrt", ["System.Nullable`1<System.Double>"]) },
                { "Math_Sqrt_D", ResolveMethod("System.Math", "Sqrt", ["System.Double"]) },
                { "Math_Tan_D?", ResolveMethod("Belte.Runtime.Math", "Tan", ["System.Nullable`1<System.Double>"]) },
                { "Math_Tan_D", ResolveMethod("System.Math", "Tan", ["System.Double"]) },
                { "Math_Tanh_D?", ResolveMethod("Belte.Runtime.Math", "Tanh", ["System.Nullable`1<System.Double>"]) },
                { "Math_Tanh_D", ResolveMethod("System.Math", "Tanh", ["System.Double"]) },
                { "Math_Truncate_D?", ResolveMethod("Belte.Runtime.Math", "Truncate", ["System.Nullable`1<System.Double>"]) },
                { "Math_Truncate_D", ResolveMethod("System.Math", "Truncate", ["System.Double"]) },
                { "Math_DegToRad_D?", ResolveMethod("Belte.Runtime.Math", "DegToRad", ["System.Nullable`1<System.Double>"]) },
                { "Math_DegToRad_D", ResolveMethod("System.Double", "DegreesToRadians", ["System.Double"]) },
                { "Math_RadToDeg_D?", ResolveMethod("Belte.Runtime.Math", "RadToDeg", ["System.Nullable`1<System.Double>"]) },
                { "Math_RadToDeg_D", ResolveMethod("System.Double", "RadiansToDegrees", ["System.Double"]) },
            };
        }
    }
}

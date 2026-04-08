using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
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
    private readonly BelteDiagnosticQueue _diagnostics;
    private readonly AssemblyDefinition _assemblyDefinition;
    private readonly List<AssemblyDefinition> _assemblies;
    private readonly List<AssemblyDefinition> _backupAssemblies;
    private readonly BoundProgram _program;
    private readonly ImmutableArray<NamedTypeSymbol> _topLevelTypes;
    private readonly ImmutableArray<NamedTypeSymbol> _linearNestedTypes;
    private readonly bool _isDll;
    private readonly bool _debugMode;

    private readonly Dictionary<SpecialType, TypeReference> _specialTypes = [];
    private readonly Dictionary<TypeSymbol, TypeDefinition> _types = [];
    private readonly Dictionary<MethodSymbol, MethodDefinition> _methods = [];
    private readonly Dictionary<MethodDefinition, (MethodSymbol, BoundBlockStatement)> _methodBodies = [];
    private readonly Dictionary<FieldSymbol, FieldDefinition> _fields = [];
    private readonly Dictionary<MethodSymbol, GenericParameter[]> _methodTypeParameters = [];
    private readonly string _belteDllName;
    private readonly string _tfm;
    private readonly string _version;

    private Dictionary<string, MethodReference> _stlMap;

    // <Globals> class members
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
        string[] references,
        bool debugMode,
        BelteDiagnosticQueue diagnostics) {
        _diagnostics = diagnostics;
        _program = program;
        _debugMode = debugMode;
        _isDll = program.compilation.options.outputKind == OutputKind.DynamicallyLinkedLibrary;

        var currentAssembly = System.Reflection.Assembly.GetExecutingAssembly();
        var attr = currentAssembly
            .GetCustomAttributes(typeof(TargetFrameworkAttribute), false)
            .OfType<TargetFrameworkAttribute>()
            .FirstOrDefault();

        _tfm = attr.FrameworkName.Split('=')[1].Substring(1);
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
            objectDll = Path.Combine(refPackPath, "System.Runtime.dll");

        if (string.IsNullOrEmpty(consoleDll))
            consoleDll = Path.Combine(refPackPath, "System.Console.dll");

        _assemblies = [
            AssemblyDefinition.ReadAssembly(objectDll),
            AssemblyDefinition.ReadAssembly(_belteDllName),
        ];

        _backupAssemblies = [
            AssemblyDefinition.ReadAssembly(consoleDll),
        ];

#if !DEBUG
#pragma warning restore IL3000
#endif

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

        _topLevelTypes = program.GetAllTypes()
            .Where(t => t.kind == SymbolKind.NamedType &&
                t.containingSymbol.kind == SymbolKind.Namespace &&
                t.specialType is SpecialType.None or SpecialType.List or SpecialType.Dictionary or SpecialType.Rect &&
                t.originalDefinition is not PENamedTypeSymbol)
            .ToArray()
            .Cast<NamedTypeSymbol>()
            .ToImmutableArray();

        var linearBuilder = ArrayBuilder<NamedTypeSymbol>.GetInstance();

        foreach (var set in _program.nestedTypes)
            linearBuilder.AddRange(set.Value);

        _linearNestedTypes = linearBuilder.ToImmutable();
    }

    internal static void Emit(
        BoundProgram program,
        string moduleName,
        string[] references,
        string outputPath,
        bool debugMode,
        BelteDiagnosticQueue diagnostics) {
        var emitter = new ILEmitter(program, moduleName, references, debugMode, diagnostics);

        if (SupportedProjectType(program, diagnostics))
            emitter.EmitToFile(outputPath, debugMode);
    }

    internal static string EmitToString(
        BoundProgram program,
        string moduleName,
        string[] references,
        BelteDiagnosticQueue diagnostics) {
        var emitter = new ILEmitter(program, moduleName, references, false, diagnostics);

        if (SupportedProjectType(program, diagnostics))
            return emitter.EmitToString();

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
        EmitRuntimeConfig(outputPath);

        if (debugMode) {
            var debugPath = Path.ChangeExtension(outputPath, ".pdb");

            using var symbolStream = File.Create(debugPath);

            var writerParameters = new WriterParameters {
                WriteSymbols = true,
                SymbolStream = symbolStream,
                SymbolWriterProvider = new PortablePdbWriterProvider()
            };

            _assemblyDefinition.Write(outputPath, writerParameters);
        } else {
            _assemblyDefinition.Write(outputPath);
        }
    }

    private void EmitRuntimeConfig(string outputPath) {
        var runtimeConfigPath = Path.ChangeExtension(outputPath, ".runtimeconfig.json");

        if (File.Exists(runtimeConfigPath))
            File.Delete(runtimeConfigPath);

        var content = $"{{\"runtimeOptions\": {{\"tfm\": \"net{_tfm}\",\"framework\": {{\"name\": \"Microsoft.NETCore.App\",\"version\": \"{_version}\"}}}}}}";

        File.WriteAllText(runtimeConfigPath, content);
    }

    private string EmitToString() {
        EmitInternal();

        var stringWriter = new StringWriter();

        using (var indentedTextWriter = new System.CodeDom.Compiler.IndentedTextWriter(stringWriter, "    ")) {
            foreach (var type in _topLevelTypes) {
                var typeDefinition = _types[type.originalDefinition];
                WriteType(stringWriter, indentedTextWriter, typeDefinition);
            }

            stringWriter.Flush();
        }

        return stringWriter.ToString();

        static void WriteType(
            StringWriter writer,
            System.CodeDom.Compiler.IndentedTextWriter indentedTextWriter,
            TypeDefinition typeDefinition) {
            using var classCurly = new CurlyIndenter(indentedTextWriter, typeDefinition.ToString());
            foreach (var field in typeDefinition.Fields)
                indentedTextWriter.WriteLine(field);

            indentedTextWriter.WriteLine();

            foreach (var method in typeDefinition.Methods) {
                using (var methodCurly = new CurlyIndenter(indentedTextWriter, method.ToString())) {
                    foreach (var instruction in method.Body.Instructions)
                        indentedTextWriter.WriteLine(instruction);
                }

                indentedTextWriter.WriteLine();
            }

            foreach (var nestedType in typeDefinition.NestedTypes)
                WriteType(writer, indentedTextWriter, nestedType);
        }
    }

    internal TypeReference GetType(TypeSymbol type, bool byRef = false) {
        var typeRef = GetTypeCore(type);

        if (byRef)
            typeRef = typeRef.MakeByReferenceType();

        return _assemblyDefinition.MainModule.ImportReference(typeRef);

        TypeReference GetTypeCore(TypeSymbol type) {
            if (type.specialType == SpecialType.Nullable) {
                var underlyingType = type.GetNullableUnderlyingType();
                var genericArgumentType = GetType(underlyingType);

                if (!CodeGenerator.IsValueType(underlyingType))
                    return genericArgumentType;

                var typeReference = new GenericInstanceType(NetTypeReference.Nullable);
                typeReference.GenericArguments.Add(genericArgumentType);
                return typeReference;
            }

            if (type is ArrayTypeSymbol array) {
                var elementType = GetType(array.elementType);
                return elementType.MakeArrayType(array.rank);
            }

            if (type is PointerTypeSymbol pointer) {
                var elementType = GetType(pointer.pointedAtType);
                return elementType.MakePointerType();
            }

            if (type is FunctionPointerTypeSymbol)
                throw ExceptionUtilities.Unreachable();

            if (type.specialType != SpecialType.None && _specialTypes.TryGetValue(type.specialType, out var value))
                return value;

            if (type is TemplateParameterSymbol t) {
                if (t.templateParameterKind == TemplateParameterKind.Method) {
                    var containingMethodTypeParameters = _methodTypeParameters[
                        (MethodSymbol)type.containingSymbol.originalDefinition
                    ];

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
            if (type.ContainsErrorType())
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

                return typeReference;
            }

            return foundType;
        }

        TypeReference GetTypeCoreInternal(NamedTypeSymbol type) {
            if (type.originalDefinition is PENamedTypeSymbol || type.IsErrorType())
                return ResolveType(null, type.ToDisplayString(SymbolDisplayFormat.NetNamespaceQualifiedNameFormat));

            return _types[type.originalDefinition];
        }
    }

    internal MethodReference GetMethod(MethodSymbol method) {
        MethodReference value = null;
        var found = false;

        if (method.originalDefinition is PEMethodSymbol m) {
            value = ResolveMethod(
                m.containingType.ToDisplayString(SymbolDisplayFormat.NetNamespaceQualifiedNameFormat),
                m.metadataName,
                m.GetParameterTypes().Select(p => GetType(p.type).ToString()).ToArray()
            );
            found = true;
        }

        if (!found && _methods.TryGetValue(method.originalDefinition, out var val)) {
            found = true;
            value = (MethodReference)val;
        }

        if (found) {
            var constructedType = GetType(method.containingType);

            if (method.arity > 0) {
                var generic = new GenericInstanceMethod(value) {
                    DeclaringType = constructedType
                };

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

        var ctorRef = _assemblyDefinition.MainModule.ImportReference(ctorDef);
        var genericCtor = new MethodReference(ctorRef.Name, ctorRef.ReturnType, typeReference) {
            HasThis = ctorRef.HasThis,
            ExplicitThis = ctorRef.ExplicitThis,
            CallingConvention = ctorRef.CallingConvention,
        };

        foreach (var p in ctorRef.Parameters)
            genericCtor.Parameters.Add(new Mono.Cecil.ParameterDefinition(p.ParameterType));

        return _assemblyDefinition.MainModule.ImportReference(genericCtor);
    }

    internal MethodReference GetNullableValue(TypeSymbol genericType) {
        var typeReference = new GenericInstanceType(NetTypeReference.Nullable);
        var genericArgumentType = GetType(genericType);
        typeReference.GenericArguments.Add(genericArgumentType);

        var getValueDef = NetMethodReference.Nullable_Value;
        var getValueRef = _assemblyDefinition.MainModule.ImportReference(getValueDef);
        var genericGetValue = new MethodReference(getValueRef.Name, getValueRef.ReturnType, typeReference) {
            HasThis = getValueRef.HasThis,
            ExplicitThis = getValueRef.ExplicitThis,
            CallingConvention = getValueRef.CallingConvention,
        };

        return _assemblyDefinition.MainModule.ImportReference(genericGetValue);
    }

    internal MethodReference GetSort(TypeSymbol elementType) {
        var genericArgumentType = GetType(elementType);

        var sortRef = new GenericInstanceMethod(NetMethodReference.LowLevel_Sort);
        sortRef.GenericArguments.Add(genericArgumentType);

        return _assemblyDefinition.MainModule.ImportReference(sortRef);
    }

    internal MethodReference GetLength(TypeSymbol elementType) {
        var genericArgumentType = GetType(elementType);

        var lengthRef = new GenericInstanceMethod(NetMethodReference.LowLevel_Length);
        lengthRef.GenericArguments.Add(genericArgumentType);

        return _assemblyDefinition.MainModule.ImportReference(lengthRef);
    }

    internal MethodReference GetNullableHasValue(TypeSymbol genericType) {
        var typeReference = new GenericInstanceType(NetTypeReference.Nullable);
        var genericArgumentType = GetType(genericType);
        typeReference.GenericArguments.Add(genericArgumentType);

        var getValueDef = NetMethodReference.Nullable_HasValue;
        var getValueRef = _assemblyDefinition.MainModule.ImportReference(getValueDef);
        var genericGetValue = new MethodReference(getValueRef.Name, getValueRef.ReturnType, typeReference) {
            HasThis = getValueRef.HasThis,
            ExplicitThis = getValueRef.ExplicitThis,
            CallingConvention = getValueRef.CallingConvention,
        };

        return _assemblyDefinition.MainModule.ImportReference(genericGetValue);
    }

    internal MethodReference GetNullAssert(TypeSymbol genericType) {
        var genericMethod = new GenericInstanceMethod(NetMethodReference.AssertNull);
        genericMethod.GenericArguments.Add(GetType(genericType));
        return _assemblyDefinition.MainModule.ImportReference(genericMethod);
    }

    internal FieldReference GetField(FieldSymbol field) {
        if (field is PEFieldSymbol f)
            return GetType(field.containingType).Resolve().Fields.Single(e => e.Name == f.name);

        var fieldRef = _fields[field];
        var constructedType = GetType(field.containingType);

        TypeReference fieldType;

        if (fieldRef.FieldType is GenericParameter gp && gp.Type == GenericParameterType.Type) {
            var index = gp.DeclaringType.GenericParameters.IndexOf(gp);
            fieldType = ((GenericInstanceType)constructedType).GenericArguments[index];
        } else {
            fieldType = fieldRef.FieldType;
        }

        return new FieldReference(
            fieldRef.Name,
            fieldType,
            constructedType
        );
    }

    internal override NamedTypeSymbol GetFixedImplementationType(SourceFixedFieldSymbol field) {
        return _program.fixedImplementationTypes[field];
    }

    internal override void EmitGlobalsClass() {
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
        _assemblyDefinition.MainModule.Types.Add(_globalsClass);
    }

    private void EmitInternal() {
        foreach (var type in _topLevelTypes) {
            var baseStack = new Stack<NamedTypeSymbol>();
            var current = type;

            while (current is not null) {
                if (current.specialType is SpecialType.Object or SpecialType.Exception)
                    break;

                baseStack.Push(current);
                current = current.baseType;
            }

            while (baseStack.Count > 0) {
                var baseType = baseStack.Pop();

                if (_types.ContainsKey(baseType.originalDefinition))
                    continue;

                var typeDefinition = CreateNamedTypeDefinition(baseType);
                _assemblyDefinition.MainModule.Types.Add(typeDefinition);
            }
        }

        foreach (var type in _topLevelTypes)
            CreateMemberDefinitions(type);

        foreach (var type in _linearNestedTypes)
            CreateMemberDefinitions(type);

        foreach (var method in _methods)
            EmitMethod(method.Value);

        var entryPoint = _program.entryPoint;

        if (entryPoint is not null) {
            if (!(entryPoint.returnsVoid || entryPoint.returnType.specialType == SpecialType.Int)) {
                _diagnostics.Push(Error.IncompatibleEntryPointReturn(entryPoint.location, entryPoint));
            } else {
                if (entryPoint.isStatic)
                    _assemblyDefinition.EntryPoint = _methods[entryPoint];
                else
                    CreateStaticEntryPoint(entryPoint);
            }
        }

        if (_debugMode) {
            var debuggableAttribute = new CustomAttribute(ResolveMethod("System.Diagnostics.DebuggableAttribute", ".ctor", ["System.Boolean", "System.Boolean"]));
            debuggableAttribute.ConstructorArguments.Add(new CustomAttributeArgument(_specialTypes[SpecialType.Bool], true));
            debuggableAttribute.ConstructorArguments.Add(new CustomAttributeArgument(_specialTypes[SpecialType.Bool], true));

            _assemblyDefinition.CustomAttributes.Add(debuggableAttribute);
        }
    }

    private void CreateStaticEntryPoint(MethodSymbol entryPoint) {
        var hasArgs = entryPoint.parameterCount > 0;
        var instanceEntry = _methods[entryPoint];

        var staticClass = new TypeDefinition(
            "",
            "<s_Program>",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed,
            _specialTypes[SpecialType.Object]
        );

        var staticEntry = new MethodDefinition(
            "Main",
            MethodAttributes.Static | MethodAttributes.Public,
            GetType(entryPoint.returnType)
        );

        if (hasArgs) {
            staticEntry.Parameters.Add(new Mono.Cecil.ParameterDefinition(
                "args",
                ParameterAttributes.None,
                GetType(entryPoint.parameters[0].type)
            ));
        }

        staticClass.Methods.Add(staticEntry);

        var staticBuilder = new CecilILBuilder(null, this, staticEntry);
        var il = staticBuilder.iLProcessor;

        il.Emit(OpCodes.Newobj, GetMethod(entryPoint.containingType.instanceConstructors[0]));

        if (hasArgs)
            il.Emit(OpCodes.Ldarg_0);

        il.Emit(OpCodes.Callvirt, instanceEntry);
        il.Emit(OpCodes.Ret);

        staticBuilder.Finish();

        _assemblyDefinition.MainModule.Types.Add(staticClass);
        _assemblyDefinition.EntryPoint = staticEntry;
    }

    private TypeDefinition CreateNamedTypeDefinition(NamedTypeSymbol type, bool isNested = false) {
        var typeDefinition = new TypeDefinition(
            GetNamespaceName(type),
            type.name,
            GetTypeAttributes(type, isNested),
            GetBaseType(type)
        );

        if (type.enumFlagsAttribute) {
            var flagsCtor = _assemblyDefinition.MainModule.ImportReference(
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

        CreateNestedTypes(type, typeDefinition, workingParams);

        _types.Add(type.originalDefinition, typeDefinition);
        return typeDefinition;
    }

    private TypeReference GetBaseType(NamedTypeSymbol type) {
        if (type.baseType is null || type.IsStructType())
            return NetTypeReference.ValueType;

        if (type.IsEnumType())
            return NetTypeReference.Enum;

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
                nestedType.templateParameters.Select(t => new GenericParameter(t.name, typeDefinition))).ToArray();

            foreach (var generic in workingParams)
                typeDefinition.GenericParameters.Add(generic);

            CreateNestedTypes(nestedType, nestedDefinition, workingParams);
            typeDefinition.NestedTypes.Add(nestedDefinition);
        }
    }

    private string GetNamespaceName(Symbol symbol) {
        if (symbol.containingNamespace is null || symbol.containingNamespace.isGlobalNamespace)
            return "";

        return symbol.containingNamespace.name;
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

        foreach (var member in type.GetMembers()) {
            if (member is FieldSymbol f) {
                if (f.isFixedSizeBuffer) {
                    CreateFixedSizeBufferField(f as SourceFixedFieldSymbol, typeDefinition);
                    continue;
                }

                var fieldDefinition = new FieldDefinition(
                    f.name,
                    GetFieldAttributes(f),
                    (f.type.typeKind == TypeKind.FunctionPointer)
                        ? _specialTypes[SpecialType.IntPtr]
                        : GetType(f.type, f.refKind != RefKind.None)
                );

                _fields.Add(f, fieldDefinition);
                typeDefinition.Fields.Add(fieldDefinition);
            } else if (member is NamedTypeSymbol t) {
                CreateMemberDefinitions(t);
            } else if (member is MethodSymbol m && m.isAbstract) {
                CreateMethodDefinition(m, null, typeDefinition);
            }
        }

        // Checking program map for methods to make sure synthesized ones are included (such as closure methods)
        foreach (var pair in _program.GetAllMethodBodies()) {
            if (pair.Item1.containingType.Equals(type)) {
                CreateMethodDefinition(pair.Item1, pair.Item2, typeDefinition);

                if (pair.Item1 == _program.entryPoint)
                    CreateAssemblyResolverDefinition(typeDefinition);
            }
        }
    }

    private void CreateFixedSizeBufferField(SourceFixedFieldSymbol field, TypeDefinition typeDefinition) {
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

        typeDefinition.Fields.Add(adaptedFieldDef);

        _fields.Add(field, adaptedFieldDef);
        _fields.Add(nestedBufferField, nestedBufferFieldDef);
        _types.Add(fixedImpl, nestedType);
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

        _methods.Add(method, methodDefinition);

        if (body is not null)
            _methodBodies.Add(methodDefinition, (method, body));

        containingType.Methods.Add(methodDefinition);

        return methodDefinition;

        TypeReference GetTypeOrIntPtr(TypeSymbol type, bool byRef) {
            if (type.typeKind == TypeKind.FunctionPointer)
                return _specialTypes[SpecialType.IntPtr];

            return GetType(type, byRef);
        }
    }

    private MethodDefinition CreatePInvokeMethodDefinition(MethodSymbol method, TypeDefinition containingType) {
        var dllImportData = method.GetDllImportData();
        var methodDefinition = new MethodDefinition(
            method.name,
            GetMethodAttributes(method),
            GetType(method.returnType, method.returnsByRef)
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
                GetType(parameter.type, parameter.refKind != RefKind.None)
            );

            methodDefinition.Parameters.Add(parameterDefinition);
        }

        _methods.Add(method, methodDefinition);
        containingType.Methods.Add(methodDefinition);
        _assemblyDefinition.MainModule.ModuleReferences.Add(moduleReference);

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
        var attributes = TypeAttributes.Class;

        if (type.isStatic)
            attributes |= TypeAttributes.Abstract | TypeAttributes.Sealed;
        if (type.isAbstract)
            attributes |= TypeAttributes.Abstract;
        if (type.isSealed)
            attributes |= TypeAttributes.Sealed;

        if (type.IsStructType())
            attributes |= TypeAttributes.SequentialLayout;

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
        if (method.isVirtual)
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

        var (method, body) = _methodBodies[methodDefinition];
        var ilBuilder = new CecilILBuilder(method, this, methodDefinition);
        var codeGen = new CodeGenerator(this, method, body, ilBuilder, _debugMode);

        if (_program.entryPoint == method)
            EmitAssemblyResolver(methodDefinition);

        codeGen.Generate();

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

        _c9 = new FieldDefinition(
            "<>9",
            FieldAttributes.InitOnly | FieldAttributes.Static | FieldAttributes.Public,
            cDefinition
        );

        _c9__0_0 = new FieldDefinition(
            "<>9__0_0",
            FieldAttributes.InitOnly | FieldAttributes.Static | FieldAttributes.Public,
            ResolveType(null, "System.ResolveEventHandler")
        );

        cDefinition.Fields.Add(_c9);
        cDefinition.Fields.Add(_c9__0_0);

        var cctor = new MethodDefinition(
            ".cctor",
            MethodAttributes.Static | MethodAttributes.Private |
            MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            _specialTypes[SpecialType.Void]
        );

        var ctor = new MethodDefinition(
            ".ctor",
            MethodAttributes.Public |
            MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            _specialTypes[SpecialType.Void]
        );

        var methodDefinition = new MethodDefinition(
            "<Main>AssemblyResolver",
            MethodAttributes.HideBySig | MethodAttributes.Assembly,
            ResolveType(null, "System.Reflection.Assembly")
        );

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
                ResolveType(null, "System.ResolveEventArgs")
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

    private TypeReference ResolveType(string name, string metadataName) {
        var foundTypes = _assemblies
            .SelectMany(a => a.Modules)
            .SelectMany(m => m.Types)
            .Where(t => t.FullName == metadataName)
            .ToArray();

        // TODO Do we actually care about ambiguity
        if (foundTypes.Length >= 1) {
            return _assemblyDefinition.MainModule.ImportReference(foundTypes[0]);
        } else if (foundTypes.Length == 0) {
            throw new BelteInternalException($"Required type not found: {name} ({metadataName})");
        } else {
            throw new BelteInternalException(
                $"Required type ambiguous: {name} ({metadataName}); found {foundTypes.Length} candidates"
            );
        }
    }

    private MethodReference ResolveMethod(string typeName, string methodName, string[] parameterTypeNames) {
        var foundTypes = _assemblies
            .SelectMany(a => a.Modules)
            .SelectMany(m => m.Types)
            .Where(t => t.FullName == typeName)
            .ToArray();

        if (foundTypes.Length >= 1) {
            if (TryResolveMethodCore(foundTypes, typeName, methodName, parameterTypeNames, out var methodRef1))
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

        if (TryResolveMethodCore([foundType], typeName, methodName, parameterTypeNames, out var methodRef2))
            return methodRef2;
        else
            throw new BelteInternalException($"Required method not found: {typeName} {methodName} {parameterTypeNames.Length}");
    }

    private bool TryResolveMethodCore(
        TypeDefinition[] foundTypes,
        string typeName,
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
                if (method.Parameters[i].ParameterType.FullName != parameterTypeNames[i]) {
                    allParametersMatch = false;
                    break;
                }
            }

            if (!allParametersMatch)
                continue;

            methodDefinition = _assemblyDefinition.MainModule.ImportReference(method);
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
            (SpecialType.Int, "System.Int64"),
            (SpecialType.String, "System.String"),
            (SpecialType.Decimal, "System.Double"),
            (SpecialType.Nullable, "System.Nullable`1"),
            (SpecialType.Void, "System.Void"),
            (SpecialType.Type, "System.Type"),
            (SpecialType.Char, "System.Char"),
            (SpecialType.Exception, "System.Exception"),
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
            var typeReference = ResolveType(CorLibrary.GetSpecialType(type).name, metadataName);
            _specialTypes.Add(type, typeReference);
        }

        NetTypeReference.Random = ResolveType(null, "System.Random");
        NetTypeReference.Nullable = ResolveType(null, "System.Nullable`1");
        NetTypeReference.ValueType = ResolveType(null, "System.ValueType");
        NetTypeReference.Enum = ResolveType(null, "System.Enum");
    }

    private MethodReference CheckStandardMap(MethodSymbol method) {
        var mapKey = LibraryHelpers.BuildMapKey(method);

        return mapKey switch {
            "Nullable<>_.ctor" => GetNullableCtor(method.containingType.templateArguments[0].type.type),
            "Nullable<>_get_Value" => GetNullableValue(method.containingType.templateArguments[0].type.type),
            "Nullable<>_get_HasValue" => GetNullableHasValue(method.containingType.templateArguments[0].type.type),
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
        NetMethodReference.Convert_ToInt64_S = ResolveMethod("System.Convert", "ToInt64", ["System.String"]);
        NetMethodReference.Convert_ToInt64_D = ResolveMethod("System.Convert", "ToInt64", ["System.Double"]);
        NetMethodReference.Convert_ToDouble_S = ResolveMethod("System.Convert", "ToDouble", ["System.String"]);
        NetMethodReference.Convert_ToDouble_I = ResolveMethod("System.Convert", "ToDouble", ["System.Int64"]);
        NetMethodReference.Convert_ToString_I = ResolveMethod("System.Convert", "ToString", ["System.Int64"]);
        NetMethodReference.Convert_ToString_D = ResolveMethod("System.Convert", "ToString", ["System.Double"]);
        NetMethodReference.Convert_ToInt32_S = ResolveMethod("System.Convert", "ToInt32", ["System.String"]);
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
        NetMethodReference.Type_GetTypeFromHandle = ResolveMethod("System.Type", "GetTypeFromHandle", ["System.RuntimeTypeHandle"]);
        NetMethodReference.NullReferenceException_ctor = ResolveMethod("System.NullReferenceException", ".ctor", []);
        NetMethodReference.NullConditionException_ctor = ResolveMethod("Belte.Runtime.NullConditionException", ".ctor", []);
        NetMethodReference.LowLevel_Sort = ResolveMethod("Belte.Runtime.Utilities", "Sort", ["T"]);
        NetMethodReference.LowLevel_Length = ResolveMethod("Belte.Runtime.Utilities", "Length", ["T"]);
        NetMethodReference.AssertNull = ResolveMethod("Belte.Runtime.Utilities", "AssertNull", ["T"]);
    }

    private void GenerateSTLMap() {
        _stlMap = new Dictionary<string, MethodReference>() {
            { "Object<>_.ctor", ResolveMethod("System.Object", ".ctor", []) },
            { "Object<>_ToString", ResolveMethod("System.Object", "ToString", []) },
            { "Object<>_Equals_O?", ResolveMethod("System.Object", "Equals", ["System.Object"]) },
            { "Object<>_GetHashCode", ResolveMethod("System.Object", "GetHashCode", []) },
            { "Exception<>_.ctor", ResolveMethod("System.Exception", ".ctor", []) },
            { "Exception<>_.ctor_S?", ResolveMethod("System.Exception", ".ctor", ["System.String"]) },
            { "Console_Clear", ResolveMethod("System.Console", "Clear", []) },
            { "Console_GetWidth", ResolveMethod("Belte.Runtime.Console", "GetWidth", []) },
            { "Console_GetHeight", ResolveMethod("Belte.Runtime.Console", "GetHeight", []) },
            { "Console_Print_S?", ResolveMethod("System.Console", "Write", ["System.String"]) },
            { "Console_Print_A?", ResolveMethod("System.Console", "Write", ["System.Object"]) },
            { "Console_Print_O?", ResolveMethod("System.Console", "Write", ["System.Object"]) },
            { "Console_Print_[?", ResolveMethod("System.Console", "Write", ["System.Char[]"]) },
            { "Console_PrintLine", ResolveMethod("System.Console", "WriteLine", []) },
            { "Console_PrintLine_S?", ResolveMethod("System.Console", "WriteLine", ["System.String"]) },
            { "Console_PrintLine_A?", ResolveMethod("System.Console", "WriteLine", ["System.Object"]) },
            { "Console_PrintLine_O?", ResolveMethod("System.Console", "WriteLine", ["System.Object"]) },
            { "Console_PrintLine_[?", ResolveMethod("System.Console", "WriteLine", ["System.Char[]"]) },
            { "Console_Input", ResolveMethod("System.Console", "ReadLine", []) },
            { "Console_ResetColor", ResolveMethod("System.Console", "ResetColor", []) },
            { "Console_SetForegroundColor_I", ResolveMethod("Belte.Runtime.Console", "SetForegroundColor", ["System.Int64"]) },
            { "Console_SetBackgroundColor_I", ResolveMethod("Belte.Runtime.Console", "SetBackgroundColor", ["System.Int64"]) },
            { "Console_SetCursorPosition_I?I?", ResolveMethod("Belte.Runtime.Console", "SetCursorPosition", ["System.Nullable`1<System.Int64>", "System.Nullable`1<System.Int64>"]) },
            { "Console_SetCursorVisibility_B", ResolveMethod("Belte.Runtime.Console", "SetCursorVisibility", ["System.Boolean"]) },
            { "Directory_Create_S", ResolveMethod("System.IO.Directory", "CreateDirectory", ["System.String"]) },
            { "Directory_Delete_S", ResolveMethod("System.IO.Directory", "Delete", ["System.String"]) },
            { "Directory_Exists_S", ResolveMethod("System.IO.Directory", "Exists", ["System.String"]) },
            { "Directory_GetCurrentDirectory", ResolveMethod("System.IO.Directory", "GetCurrentDirectory", []) },
            { "File_AppendText_SS", ResolveMethod("System.IO.File", "AppendAllText", ["System.String", "System.String"]) },
            { "File_Create_S", ResolveMethod("System.IO.File", "Create", ["System.String"]) },
            { "File_Copy_S", ResolveMethod("System.IO.File", "Copy", ["System.String", "System.String"]) },
            { "File_Delete_S", ResolveMethod("System.IO.File", "Delete", ["System.String"]) },
            { "File_Exists_S", ResolveMethod("System.IO.File", "Exists", ["System.String"]) },
            { "File_ReadText_S", ResolveMethod("System.IO.File", "ReadAllText", ["System.String"]) },
            { "File_WriteText_S", ResolveMethod("System.IO.File", "WriteAllText", ["System.String", "System.String"]) },
            { "String_Ascii_S", ResolveMethod("Belte.Runtime.Utilities", "Ascii", ["System.String"]) },
            { "String_Char_I", ResolveMethod("Belte.Runtime.Utilities", "Char", ["System.Int64"]) },
            { "String_Split_SS", ResolveMethod("Belte.Runtime.Utilities", "Split", ["System.String", "System.String"]) },
            { "String_Length_S", ResolveMethod("Belte.Runtime.Utilities", "StringLength", ["System.String"]) },
            { "String_IsNullOrWhiteSpace_S?", ResolveMethod("System.String", "IsNullOrWhiteSpace", ["System.String"]) },
            { "String_IsNullOrWhiteSpace_C?", ResolveMethod("Belte.Runtime.Utilities", "IsNullOrWhiteSpace", ["System.Nullable`1<System.Char>"]) },
            { "String_IsDigit_C?", ResolveMethod("Belte.Runtime.Utilities", "IsDigit", ["System.Nullable`1<System.Char>"]) },
            { "String_Substring_S?I?I?", ResolveMethod("Belte.Runtime.Utilities", "Substring", ["System.String", "System.Nullable`1<System.Int64>", "System.Nullable`1<System.Int64>"]) },
            { "Int_Parse_S?", ResolveMethod("Belte.Runtime.Utilities", "IntParse", ["System.String"]) },
            { "LowLevel_GetHashCode_O", ResolveMethod("Belte.Runtime.Utilities", "GetHashCode", ["System.Object"]) },
            { "LowLevel_GetTypeName_O", ResolveMethod("Belte.Runtime.Utilities", "GetTypeName", ["System.Object"]) },
            { "LowLevel_ThrowNullConditionException", ResolveMethod("Belte.Runtime.ThrowHelper", "ThrowNullConditionException", []) },
            { "LowLevel_CreateLPCSTR_S", ResolveMethod("Belte.Runtime.Utilities", "CreateLPCSTR", ["System.String"]) },
            { "LowLevel_CreateLPCWSTR_S", ResolveMethod("Belte.Runtime.Utilities", "CreateLPCWSTR", ["System.String"]) },
            { "LowLevel_FreeLPCSTR_U*", ResolveMethod("Belte.Runtime.Utilities", "FreeLPCSTR", ["System.Byte*"]) },
            { "LowLevel_FreeLPCWSTR_C*", ResolveMethod("Belte.Runtime.Utilities", "FreeLPCWSTR", ["System.Char*"]) },
            { "LowLevel_ReadLPCSTR_U*", ResolveMethod("Belte.Runtime.Utilities", "ReadLPCSTR", ["System.Byte*"]) },
            { "LowLevel_ReadLPCWSTR_C*", ResolveMethod("Belte.Runtime.Utilities", "ReadLPCWSTR", ["System.Char*"]) },
            { "LowLevel_GetGCPtr_O", ResolveMethod("Belte.Runtime.Utilities", "GetGCPtr", ["System.Object"]) },
            { "LowLevel_FreeGCHandle_V*", ResolveMethod("Belte.Runtime.Utilities", "FreeGCHandle", ["System.Void*"]) },
            { "LowLevel_GetObject_V*", ResolveMethod("Belte.Runtime.Utilities", "GetObject", ["System.Void*"]) },
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
            { "Math_Clamp_I?I?I?", ResolveMethod("Belte.Runtime.Math", "Clamp", ["System.Nullable`1<System.Int64>", "System.Nullable`1<System.Int64>", "System.Nullable`1<System.Int64>"]) },
            { "Math_Clamp_III", ResolveMethod("System.Math", "Clamp", ["System.Int64","System.Int64", "System.Int64"]) },
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
            { "Math_Max_I?I?", ResolveMethod("Belte.Runtime.Math", "Max", ["System.Nullable`1<System.Int64>", "System.Nullable`1<System.Int64>"]) },
            { "Math_Max_II", ResolveMethod("System.Math", "Max", ["System.Int64", "System.Int64"]) },
            { "Math_Min_D?D?", ResolveMethod("Belte.Runtime.Math", "Min", ["System.Nullable`1<System.Double>", "System.Nullable`1<System.Double>"]) },
            { "Math_Min_DD", ResolveMethod("System.Math", "Min", ["System.Double", "System.Double"]) },
            { "Math_Min_I?I?", ResolveMethod("Belte.Runtime.Math", "Min", ["System.Nullable`1<System.Int64>", "System.Nullable`1<System.Int64>"]) },
            { "Math_Min_II", ResolveMethod("System.Math", "Min", ["System.Int64", "System.Int64"]) },
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

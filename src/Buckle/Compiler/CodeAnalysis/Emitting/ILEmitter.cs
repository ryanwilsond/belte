using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Libraries;
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

    private readonly Dictionary<SpecialType, TypeReference> _specialTypes = [];
    private readonly Dictionary<TypeSymbol, TypeDefinition> _types = [];
    private readonly Dictionary<MethodSymbol, MethodDefinition> _methods = [];
    private readonly Dictionary<MethodDefinition, (MethodSymbol, BoundBlockStatement)> _methodBodies = [];
    private readonly Dictionary<FieldSymbol, FieldDefinition> _fields = [];
    private readonly string _belteDllName;

    private Dictionary<string, MethodReference> _stlMap;

    // <Globals> class members
    private TypeDefinition _globalsClass;
    private MethodDefinition _nullAssertObjectMethod;
    private MethodDefinition _nullAssertValueMethod;
    // private FieldDefinition _c9;
    // private MethodDefinition _cMain;
    // private MethodDefinition _cctor;
    // private MethodDefinition _ctor;
    // private MethodDefinition _pcctor;
    // private MethodDefinition _actualEntryPoint;

    internal FieldDefinition randomField;

    private ILEmitter(
        BoundProgram program,
        string assemblySimpleName,
        string[] references,
        BelteDiagnosticQueue diagnostics) {
        _diagnostics = diagnostics;
        _program = program;
        _isDll = program.compilation.options.outputKind == OutputKind.DynamicallyLinkedLibrary;

        var currentAssembly = System.Reflection.Assembly.GetExecutingAssembly();
        var attr = currentAssembly
            .GetCustomAttributes(typeof(TargetFrameworkAttribute), false)
            .OfType<TargetFrameworkAttribute>()
            .FirstOrDefault();

        var tfm = attr.FrameworkName.Split('=')[1].Substring(1);
        var runtimeDll = DotnetReferenceResolver.ResolveSystemRuntimeDll(tfm);
        var netstandardDll = DotnetReferenceResolver.ResolveNetStandardDll(tfm);
        var privateCoreLibDll = DotnetReferenceResolver.ResolvePrivateCoreLibDll(tfm);
        _belteDllName = typeof(Belte.Runtime.Console).Assembly.Location;

        if (string.IsNullOrEmpty(_belteDllName))
            _belteDllName = Path.Join(AppContext.BaseDirectory, "Belte.Runtime.dll");

        _assemblies = [
            AssemblyDefinition.ReadAssembly(typeof(object).Assembly.Location),
            // AssemblyDefinition.ReadAssembly(_belteDllName),
        ];

        _backupAssemblies = [
            AssemblyDefinition.ReadAssembly(typeof(Console).Assembly.Location),
            // AssemblyDefinition.ReadAssembly(runtimeDll),
            // AssemblyDefinition.ReadAssembly(netstandardDll),
            // AssemblyDefinition.ReadAssembly(privateCoreLibDll),
        ];

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

        _topLevelTypes = program.types.Where(t => t.containingSymbol.kind == SymbolKind.Namespace).ToImmutableArray();

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
        BelteDiagnosticQueue diagnostics) {
        var emitter = new ILEmitter(program, moduleName, references, diagnostics);
        emitter.EmitToFile(outputPath);
    }

    internal static string EmitToString(
        BoundProgram program,
        string moduleName,
        string[] references,
        BelteDiagnosticQueue diagnostics) {
        var emitter = new ILEmitter(program, moduleName, references, diagnostics);
        return emitter.EmitToString();
    }

    private void EmitToFile(string outputPath) {
        EmitInternal();
        _assemblyDefinition.Write(outputPath);
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
                var genericArgumentType = GetTypeCore(underlyingType);

                if (!CodeGenerator.IsValueType(underlyingType))
                    return genericArgumentType;

                var typeReference = new GenericInstanceType(NetTypeReference.Nullable);
                typeReference.GenericArguments.Add(genericArgumentType);
                return typeReference;
            }

            if (type is ArrayTypeSymbol array) {
                var elementType = GetTypeCore(array.elementType);
                var arrayType = elementType.MakeArrayType(array.rank);
                return arrayType.Resolve();
            }

            if (type.specialType != SpecialType.None)
                return _specialTypes[type.specialType];

            return _types[type.originalDefinition];
        }
    }

    internal MethodReference GetMethod(MethodSymbol method) {
        if (_methods.TryGetValue(method, out var value))
            return value;

        return CheckStandardMap(method);
    }

    internal MethodReference GetNullableCtor(TypeSymbol genericType) {
        var typeReference = new GenericInstanceType(NetTypeReference.Nullable);
        var genericArgumentType = GetType(genericType);
        typeReference.GenericArguments.Add(genericArgumentType);

        // var genericDef = NetTypeReference.Nullable.Resolve();

        // var ctorDef = genericDef.Methods
        //     .First(m => m.IsConstructor && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.Name == "T");
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

        var sortRef = new GenericInstanceMethod(NetMethodReference.Array_Sort);
        sortRef.GenericArguments.Add(genericArgumentType);

        return _assemblyDefinition.MainModule.ImportReference(sortRef);
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

    internal MethodReference GetNullAssertObject(TypeSymbol genericType) {
        var genericMethod = new GenericInstanceMethod(_nullAssertObjectMethod);
        genericMethod.GenericArguments.Add(GetType(genericType));
        return _assemblyDefinition.MainModule.ImportReference(genericMethod);
    }

    internal MethodReference GetNullAssertValue(TypeSymbol genericType) {
        var genericMethod = new GenericInstanceMethod(_nullAssertValueMethod);
        genericMethod.GenericArguments.Add(GetType(genericType));
        return _assemblyDefinition.MainModule.ImportReference(genericMethod);
    }

    internal FieldReference GetField(FieldSymbol field) {
        return _fields[field];
    }

    internal override void EmitGlobalsClass() {
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

        // AssertObjectNotNull and AssertValueNotNull
        {
            _nullAssertObjectMethod = new MethodDefinition(
                "<AssertObjectNotNull>",
                MethodAttributes.Static | MethodAttributes.Public,
                _specialTypes[SpecialType.Void]
            );

            var nullAssertObjectT = new GenericParameter("T", _nullAssertObjectMethod) {
                Attributes = GenericParameterAttributes.ReferenceTypeConstraint
            };

            _nullAssertObjectMethod.GenericParameters.Add(nullAssertObjectT);
            _nullAssertObjectMethod.ReturnType = nullAssertObjectT;
            _nullAssertObjectMethod.Parameters.Add(new Mono.Cecil.ParameterDefinition("o", ParameterAttributes.None, nullAssertObjectT));

            _nullAssertObjectMethod.Body.InitLocals = true;
            var nullAssertObjectILProcessor = _nullAssertObjectMethod.Body.GetILProcessor();

            /*

            public static T AssertObjectNotNull<T>(T o) where T : class {
                if (o is null)
                    throw new NullReferenceException();

                return o;
            }

            */
            nullAssertObjectILProcessor.Emit(OpCodes.Ldarg_0);
            nullAssertObjectILProcessor.Emit(OpCodes.Box, nullAssertObjectT);
            nullAssertObjectILProcessor.Emit(OpCodes.Ldnull);
            nullAssertObjectILProcessor.Emit(OpCodes.Ceq);
            nullAssertObjectILProcessor.Emit(OpCodes.Brfalse_S, Instruction.Create(OpCodes.Nop));
            nullAssertObjectILProcessor.Emit(OpCodes.Newobj, NetMethodReference.NullReferenceException_ctor);
            nullAssertObjectILProcessor.Emit(OpCodes.Throw);
            nullAssertObjectILProcessor.Emit(OpCodes.Ldarg_0);
            nullAssertObjectILProcessor.Emit(OpCodes.Ret);

            nullAssertObjectILProcessor.Body.Instructions[4].Operand = nullAssertObjectILProcessor.Body.Instructions[7];

            _globalsClass.Methods.Add(_nullAssertObjectMethod);

            _nullAssertValueMethod = new MethodDefinition(
                "<AssertValueNotNull>",
                MethodAttributes.Static | MethodAttributes.Public,
                _specialTypes[SpecialType.Void]
            );

            var nullAssertValueT = new GenericParameter("T", _nullAssertValueMethod) {
                Attributes = GenericParameterAttributes.NotNullableValueTypeConstraint
            };

            _nullAssertValueMethod.GenericParameters.Add(nullAssertValueT);
            _nullAssertValueMethod.ReturnType = nullAssertValueT;
            _nullAssertValueMethod.Parameters.Add(new Mono.Cecil.ParameterDefinition("v", ParameterAttributes.None, nullAssertValueT));

            _nullAssertValueMethod.Body.InitLocals = true;
            var nullAssertValueILProcessor = _nullAssertValueMethod.Body.GetILProcessor();

            /*

            public static T AssertValueNotNull<T>(T? v) where T : struct {
                if (!v.HasValue)
                    throw new NullReferenceException();

                return v.Value;
            }

            */
            var hvTypeReference = new GenericInstanceType(NetTypeReference.Nullable);
            hvTypeReference.GenericArguments.Add(nullAssertValueT);

            var getHasValueDef = NetMethodReference.Nullable_HasValue;
            var getHasValueRef = _assemblyDefinition.MainModule.ImportReference(getHasValueDef);
            var genericGetHasValue = new MethodReference(getHasValueRef.Name, getHasValueRef.ReturnType, hvTypeReference) {
                HasThis = getHasValueRef.HasThis,
                ExplicitThis = getHasValueRef.ExplicitThis,
                CallingConvention = getHasValueRef.CallingConvention,
            };

            var nullHasValue = _assemblyDefinition.MainModule.ImportReference(genericGetHasValue);

            var gvTypeReference = new GenericInstanceType(NetTypeReference.Nullable);
            gvTypeReference.GenericArguments.Add(nullAssertValueT);

            var getValueDef = NetMethodReference.Nullable_HasValue;
            var getValueRef = _assemblyDefinition.MainModule.ImportReference(getValueDef);
            var genericGetValue = new MethodReference(getValueRef.Name, getValueRef.ReturnType, gvTypeReference) {
                HasThis = getValueRef.HasThis,
                ExplicitThis = getValueRef.ExplicitThis,
                CallingConvention = getValueRef.CallingConvention,
            };

            var nullGetValue = _assemblyDefinition.MainModule.ImportReference(genericGetValue);

            nullAssertValueILProcessor.Emit(OpCodes.Ldarga_S, 0);
            nullAssertValueILProcessor.Emit(OpCodes.Call, nullHasValue);
            nullAssertValueILProcessor.Emit(OpCodes.Ldc_I4_0);
            nullAssertValueILProcessor.Emit(OpCodes.Ceq);
            nullAssertValueILProcessor.Emit(OpCodes.Brfalse_S, Instruction.Create(OpCodes.Nop));
            nullAssertValueILProcessor.Emit(OpCodes.Newobj, NetMethodReference.NullReferenceException_ctor);
            nullAssertValueILProcessor.Emit(OpCodes.Throw);
            nullAssertValueILProcessor.Emit(OpCodes.Ldarga_S, 0);
            nullAssertValueILProcessor.Emit(OpCodes.Call, nullGetValue);
            nullAssertValueILProcessor.Emit(OpCodes.Ret);

            nullAssertValueILProcessor.Body.Instructions[4].Operand = nullAssertValueILProcessor.Body.Instructions[7];

            _globalsClass.Methods.Add(_nullAssertValueMethod);
        }

        _assemblyDefinition.MainModule.Types.Add(_globalsClass);
    }

    private void EmitInternal() {
        foreach (var type in _topLevelTypes) {
            var typeDefinition = CreateNamedTypeDefinition(type);
            _assemblyDefinition.MainModule.Types.Add(typeDefinition);
        }

        foreach (var type in _topLevelTypes)
            CreateMemberDefinitions(type);

        foreach (var type in _linearNestedTypes)
            CreateMemberDefinitions(type);

        foreach (var method in _methods)
            EmitMethod(method.Value);

        var entryPoint = _program.entryPoint;

        if (entryPoint is not null) {
            _assemblyDefinition.EntryPoint = _methods[entryPoint];

            // if (_actualEntryPoint is not null) {
            //     _assemblyDefinition.EntryPoint = _actualEntryPoint;

            if (!(entryPoint.returnsVoid || entryPoint.returnType.specialType == SpecialType.Int))
                _diagnostics.Push(Error.IncompatibleEntryPointReturn(entryPoint.location, entryPoint));
        }
    }

    private TypeDefinition CreateNamedTypeDefinition(NamedTypeSymbol type, bool isNested = false) {
        var typeDefinition = new TypeDefinition(
            GetNamespaceName(type),
            type.name,
            GetTypeAttributes(type, isNested),
            type.typeKind == TypeKind.Struct ? NetTypeReference.ValueType : GetType(type.baseType)
        );

        foreach (var member in type.GetTypeMembers())
            CreateNestedType(member);

        if (_program.nestedTypes.ContainsKey(type)) {
            foreach (var nestedType in _program.nestedTypes[type])
                CreateNestedType(nestedType);
        }

        _types.Add(type.originalDefinition, typeDefinition);
        return typeDefinition;

        void CreateNestedType(NamedTypeSymbol nestedType) {
            var nestedDefinition = CreateNamedTypeDefinition(nestedType, isNested: true);
            typeDefinition.NestedTypes.Add(nestedDefinition);
        }
    }

    private string GetNamespaceName(Symbol symbol) {
        if (symbol.containingNamespace is null || symbol.containingNamespace.isGlobalNamespace)
            return "";

        return symbol.containingNamespace.name;
    }

    private void CreateMemberDefinitions(NamedTypeSymbol type) {
        var typeDefinition = _types[type.originalDefinition];

        foreach (var member in type.GetMembers()) {
            if (member is FieldSymbol f) {
                var fieldDefinition = new FieldDefinition(
                    f.name,
                    GetFieldAttributes(f),
                    GetType(f.type, f.refKind != RefKind.None)
                );

                _fields.Add(f, fieldDefinition);
                typeDefinition.Fields.Add(fieldDefinition);
            } else if (member is NamedTypeSymbol t) {
                CreateMemberDefinitions(t);
            }
        }

        // Checking program map for methods to make sure synthesized ones are included (such as closure methods)
        foreach (var pair in _program.methodBodies) {
            if (pair.Key.containingType.Equals(type)) {
                // var method =
                CreateMethodDefinition(pair.Key, pair.Value, typeDefinition);

                // if (pair.Key == _program.entryPoint)
                //     CreateAssemblyResolverDefinition(typeDefinition, method);
            }
        }
    }

    private MethodDefinition CreateMethodDefinition(MethodSymbol method, BoundBlockStatement body, TypeDefinition containingType) {
        var methodDefinition = new MethodDefinition(
            method.name,
            GetMethodAttributes(method),
            GetType(method.returnType, method.returnsByRef)
        );

        foreach (var parameter in method.parameters) {
            var parameterDefinition = new Mono.Cecil.ParameterDefinition(
                parameter.name,
                ParameterAttributes.None,
                GetType(parameter.type, parameter.refKind != RefKind.None)
            );

            methodDefinition.Parameters.Add(parameterDefinition);
        }

        _methods.Add(method, methodDefinition);
        _methodBodies.Add(methodDefinition, (method, body));
        containingType.Methods.Add(methodDefinition);

        return methodDefinition;
    }

    private static TypeAttributes GetTypeAttributes(NamedTypeSymbol type, bool isNested) {
        var attributes = TypeAttributes.Class;

        if (type.isStatic)
            attributes |= TypeAttributes.Abstract | TypeAttributes.Sealed;
        if (type.isAbstract)
            attributes |= TypeAttributes.Abstract;
        if (type.isSealed)
            attributes |= TypeAttributes.Sealed;

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

        return attributes;
    }

    private void EmitMethod(MethodDefinition methodDefinition) {
        var (method, body) = _methodBodies[methodDefinition];
        var ilBuilder = new CecilILBuilder(method, this, methodDefinition);
        var codeGen = new CodeGenerator(this, method, body, ilBuilder);

        // if (_program.entryPoint == method)
        //     EmitAssemblyResolver(methodDefinition);

        codeGen.Generate();

        methodDefinition.Body.OptimizeMacros();
    }

    /*
        private void CreateAssemblyResolverDefinition(TypeDefinition mainType, MethodDefinition mainMethod) {
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

            cDefinition.Fields.Add(_c9);

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

            _cMain = methodDefinition;
            _cctor = cctor;
            _ctor = ctor;
            cDefinition.Methods.Add(cctor);
            cDefinition.Methods.Add(ctor);
            cDefinition.Methods.Add(_cMain);

            mainType.NestedTypes.Add(cDefinition);

            _pcctor = new MethodDefinition(
                ".cctor",
                MethodAttributes.Static | MethodAttributes.Private |
                MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                _specialTypes[SpecialType.Void]
            );

            _actualEntryPoint = new MethodDefinition(
                "<>Main",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static,
                mainMethod.ReturnType
            );

            mainType.Methods.Add(_pcctor);
            mainType.Methods.Add(_actualEntryPoint);
        }

        private void EmitAssemblyResolver(MethodDefinition mainMethod) {
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

            var cBuilder = new CecilILBuilder(null, this, _cMain);

            cBuilder.AllocateSlot(_assemblyDefinition.MainModule.ImportReference(ResolveType(null, "System.Byte").MakeArrayType(1)), LocalSlotConstraints.None);

            var cIL = cBuilder.iLProcessor;
            var ret = new object();

            // var targetName = Path.Combine(AppContext.BaseDirectory, "a", _belteDllName);
            var targetName = Path.Combine("a", "bin", "debug", _belteDllName);

            cIL.Emit(OpCodes.Ldarg_2);
            cIL.Emit(OpCodes.Callvirt, ResolveMethod("System.ResolveEventArgs", "get_Name", []));
            cIL.Emit(OpCodes.Newobj, ResolveMethod("System.Reflection.AssemblyName", ".ctor", ["System.String"]));
            cIL.Emit(OpCodes.Call, ResolveMethod("System.Reflection.AssemblyName", "get_Name", []));
            cIL.Emit(OpCodes.Ldstr, "Belte.Runtime");
            cIL.Emit(OpCodes.Callvirt, ResolveMethod("System.String", "Contains", ["System.String"]));
            cBuilder.EmitBranch(CodeGeneration.OpCode.Brfalse_S, ret);
            cIL.Emit(OpCodes.Call, ResolveMethod("System.Reflection.Assembly", "GetExecutingAssembly", []));
            cIL.Emit(OpCodes.Ldstr, targetName);
            cIL.Emit(OpCodes.Callvirt, ResolveMethod("System.Reflection.Assembly", "GetManifestResourceStream", ["System.String"]));
            cIL.Emit(OpCodes.Dup);
            cIL.Emit(OpCodes.Callvirt, ResolveMethod("System.IO.Stream", "get_Length", []));
            cIL.Emit(OpCodes.Conv_Ovf_I);
            cIL.Emit(OpCodes.Newarr, ResolveType(null, "System.Byte"));
            cIL.Emit(OpCodes.Stloc_0);
            cIL.Emit(OpCodes.Ldloc_0);
            cIL.Emit(OpCodes.Ldc_I4_0);
            cIL.Emit(OpCodes.Ldloc_0);
            cIL.Emit(OpCodes.Ldlen);
            cIL.Emit(OpCodes.Conv_I4);
            cIL.Emit(OpCodes.Callvirt, ResolveMethod("System.IO.Stream", "Read", ["System.Byte[]", "System.Int32", "System.Int32"]));
            cIL.Emit(OpCodes.Pop);
            cIL.Emit(OpCodes.Ldloc_0);
            cIL.Emit(OpCodes.Call, ResolveMethod("System.Reflection.Assembly", "Load", ["System.Byte[]"]));
            cIL.Emit(OpCodes.Ret);
            cBuilder.MarkLabel(ret);
            cIL.Emit(OpCodes.Ldnull);
            cIL.Emit(OpCodes.Ret);

            cBuilder.Finish();

            // var cBuilder = new CecilILBuilder(null, this, _cMain);

            // cBuilder.AllocateSlot(ResolveType(null, "System.IO.Stream"), LocalSlotConstraints.None);
            // cBuilder.AllocateSlot(_assemblyDefinition.MainModule.ImportReference(ResolveType(null, "System.Byte").MakeArrayType(1)), LocalSlotConstraints.None);
            // cBuilder.AllocateSlot(ResolveType(null, "System.Reflection.Assembly"), LocalSlotConstraints.None);

            // var cIL = cBuilder.iLProcessor;
            // var endTry = new object();
            // var endFinally = new object();
            // var ret = new object();

            // cIL.Emit(OpCodes.Ldarg_2);
            // cIL.Emit(OpCodes.Callvirt, ResolveMethod("System.ResolveEventArgs", "get_Name", []));
            // cIL.Emit(OpCodes.Newobj, ResolveMethod("System.Reflection.AssemblyName", ".ctor", ["System.String"]));
            // cIL.Emit(OpCodes.Call, ResolveMethod("System.Reflection.AssemblyName", "get_Name", []));
            // cIL.Emit(OpCodes.Ldstr, "Belte.Runtime");
            // cIL.Emit(OpCodes.Callvirt, ResolveMethod("System.String", "Contains", ["System.String"]));
            // cBuilder.EmitBranch(CodeGeneration.OpCode.Brfalse_S, endTry);
            // cIL.Emit(OpCodes.Call, ResolveMethod("System.Reflection.Assembly", "GetExecutingAssembly", []));
            // cIL.Emit(OpCodes.Ldstr, "Belte.Runtime.dll");
            // cIL.Emit(OpCodes.Callvirt, ResolveMethod("System.Reflection.Assembly", "GetManifestResourceStream", ["System.String"]));
            // cIL.Emit(OpCodes.Stloc_0);
            // var tryStart = cIL.Create(OpCodes.Ldloc_0);
            // cIL.Append(tryStart);
            // cIL.Emit(OpCodes.Callvirt, ResolveMethod("System.IO.Stream", "get_Length", []));
            // cIL.Emit(OpCodes.Conv_Ovf_I);
            // cIL.Emit(OpCodes.Newarr, ResolveType(null, "System.Byte"));
            // cIL.Emit(OpCodes.Stloc_1);
            // cIL.Emit(OpCodes.Ldloc_0);
            // cIL.Emit(OpCodes.Ldloc_1);
            // cIL.Emit(OpCodes.Ldc_I4_0);
            // cIL.Emit(OpCodes.Ldloc_1);
            // cIL.Emit(OpCodes.Ldlen);
            // cIL.Emit(OpCodes.Conv_I4);
            // cIL.Emit(OpCodes.Callvirt, ResolveMethod("System.IO.Stream", "Read", ["System.Byte[]", "System.Int32", "System.Int32"]));
            // cIL.Emit(OpCodes.Pop);
            // cIL.Emit(OpCodes.Ldloc_1);
            // cIL.Emit(OpCodes.Call, ResolveMethod("System.Reflection.Assembly", "Load", ["System.Byte[]"]));
            // cIL.Emit(OpCodes.Stloc_2);
            // cBuilder.EmitBranch(CodeGeneration.OpCode.Leave_S, ret);
            // var finallyStart = cIL.Create(OpCodes.Ldloc_0);
            // cIL.Append(finallyStart);
            // cBuilder.EmitBranch(CodeGeneration.OpCode.Brfalse_S, endFinally);
            // cIL.Emit(OpCodes.Ldloc_0);
            // cIL.Emit(OpCodes.Callvirt, ResolveMethod("System.IDisposable", "Dispose", []));
            // cBuilder.MarkLabel(endFinally);
            // var finallyEnd = cIL.Create(OpCodes.Endfinally);
            // cIL.Append(finallyEnd);
            // cBuilder.MarkLabel(endTry);
            // cIL.Emit(OpCodes.Ldnull);
            // cIL.Emit(OpCodes.Ret);
            // cBuilder.MarkLabel(ret);
            // cIL.Emit(OpCodes.Ldloc_2);
            // cIL.Emit(OpCodes.Ret);

            // _cMain.Body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Finally) {
            //     TryStart = tryStart,
            //     TryEnd = finallyStart,
            //     HandlerStart = finallyStart,
            //     HandlerEnd = finallyEnd
            // });

            // cBuilder.Finish();

            var pcctorBuilder = new CecilILBuilder(null, this, _pcctor);
            var pcctorIL = pcctorBuilder.iLProcessor;

            var endPcctor = new object();

            // pcctorIL.Emit(OpCodes.Call, ResolveMethod("System.AppDomain", "get_CurrentDomain", []));
            // pcctorIL.Emit(OpCodes.Ldsfld, _c9__0_0);
            // pcctorIL.Emit(OpCodes.Dup);
            // pcctorBuilder.EmitBranch(CodeGeneration.OpCode.Brtrue_S, endPcctor);
            // pcctorIL.Emit(OpCodes.Pop);
            // pcctorIL.Emit(OpCodes.Ldsfld, _c9);
            // pcctorIL.Emit(OpCodes.Ldftn, _cMain);
            // pcctorIL.Emit(OpCodes.Newobj, ResolveMethod("System.ResolveEventHandler", ".ctor", ["System.Object", "System.IntPtr"]));
            // pcctorIL.Emit(OpCodes.Dup);
            // pcctorIL.Emit(OpCodes.Stsfld, _c9__0_0);
            // pcctorBuilder.MarkLabel(endPcctor);
            // pcctorIL.Emit(OpCodes.Callvirt, ResolveMethod("System.AppDomain", "add_AssemblyResolve", ["System.ResolveEventHandler"]));

            pcctorIL.Emit(OpCodes.Call, ResolveMethod("System.AppDomain", "get_CurrentDomain", []));
            pcctorIL.Emit(OpCodes.Ldsfld, _c9);
            pcctorIL.Emit(OpCodes.Ldftn, _cMain);
            pcctorIL.Emit(OpCodes.Newobj, ResolveMethod("System.ResolveEventHandler", ".ctor", ["System.Object", "System.IntPtr"]));
            pcctorIL.Emit(OpCodes.Callvirt, ResolveMethod("System.AppDomain", "add_AssemblyResolve", ["System.ResolveEventHandler"]));
            pcctorIL.Emit(OpCodes.Ret);

            pcctorBuilder.Finish();

            // ? Debug segment for listing manifest resources

            var amBuilder = new CecilILBuilder(null, this, _actualEntryPoint);
            var amIL = amBuilder.iLProcessor;

            amBuilder.AllocateSlot(_assemblyDefinition.MainModule.ImportReference(_specialTypes[SpecialType.String].MakeArrayType(1)), LocalSlotConstraints.None);
            amBuilder.AllocateSlot(ResolveType(null, "System.Int32"), LocalSlotConstraints.None);

            var endLoop = new object();
            var loopAgain = new object();

            amIL.Emit(OpCodes.Call, ResolveMethod("System.Reflection.Assembly", "GetExecutingAssembly", []));
            amIL.Emit(OpCodes.Callvirt, ResolveMethod("System.Reflection.Assembly", "GetManifestResourceNames", []));
            amIL.Emit(OpCodes.Stloc_0);
            amIL.Emit(OpCodes.Ldc_I4_0);
            amIL.Emit(OpCodes.Stloc_1);
            amBuilder.EmitBranch(CodeGeneration.OpCode.Br_S, endLoop);
            amBuilder.MarkLabel(loopAgain);
            amIL.Emit(OpCodes.Ldloc_0);
            amIL.Emit(OpCodes.Ldloc_1);
            amIL.Emit(OpCodes.Ldelem_Ref);
            amIL.Emit(OpCodes.Call, ResolveMethod("System.Console", "WriteLine", ["System.String"]));
            amIL.Emit(OpCodes.Ldloc_1);
            amIL.Emit(OpCodes.Ldc_I4_1);
            amIL.Emit(OpCodes.Add);
            amIL.Emit(OpCodes.Stloc_1);
            amBuilder.MarkLabel(endLoop);
            amIL.Emit(OpCodes.Ldloc_1);
            amIL.Emit(OpCodes.Ldloc_0);
            amIL.Emit(OpCodes.Ldlen);
            amIL.Emit(OpCodes.Conv_I4);
            amBuilder.EmitBranch(CodeGeneration.OpCode.Blt_S, loopAgain);

            amIL.Emit(OpCodes.Call, mainMethod);
            amIL.Emit(OpCodes.Ret);

            amBuilder.Finish();
        }
    */

    private TypeReference ResolveType(string name, string metadataName) {
        var foundTypes = _assemblies
            .SelectMany(a => a.Modules)
            .SelectMany(m => m.Types)
            .Where(t => t.FullName == metadataName)
            .ToArray();

        if (foundTypes.Length == 1) {
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

        if (foundTypes.Length == 1 &&
            TryResolveMethodCore(foundTypes, typeName, methodName, parameterTypeNames, out var methodRef1)) {
            return methodRef1;
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
        };

        foreach (var (type, metadataName) in builtInTypes) {
            var typeReference = ResolveType(CorLibrary.GetSpecialType(type).name, metadataName);
            _specialTypes.Add(type, typeReference);
        }

        NetTypeReference.Random = ResolveType(null, "System.Random");
        NetTypeReference.Nullable = ResolveType(null, "System.Nullable`1");
        NetTypeReference.ValueType = ResolveType(null, "System.ValueType");


        // _specialTypes.Add(SpecialType.Object, _assemblyDefinition.MainModule.TypeSystem.Object);
        // _specialTypes.Add(SpecialType.Any, _assemblyDefinition.MainModule.TypeSystem.Object);
        // _specialTypes.Add(SpecialType.Bool, _assemblyDefinition.MainModule.TypeSystem.Boolean);
        // _specialTypes.Add(SpecialType.Int, _assemblyDefinition.MainModule.TypeSystem.Int64);
        // _specialTypes.Add(SpecialType.String, _assemblyDefinition.MainModule.TypeSystem.String);
        // _specialTypes.Add(SpecialType.Decimal, _assemblyDefinition.MainModule.TypeSystem.Double);
        // _specialTypes.Add(SpecialType.Void, _assemblyDefinition.MainModule.TypeSystem.Void);
        // _specialTypes.Add(SpecialType.Char, _assemblyDefinition.MainModule.TypeSystem.Char);

        // _specialTypes.Add(SpecialType.Type, ResolveType(CorLibrary.GetSpecialType(SpecialType.Type).name, "System.Type"));

        // NetTypeReference.Random = ResolveType(null, "System.Random");
        // NetTypeReference.Nullable = ResolveType(null, "System.Nullable`1");
        // NetTypeReference.ValueType = ResolveType(null, "System.ValueType");
    }

    private MethodReference CheckStandardMap(MethodSymbol method) {
        var mapKey = LibraryHelpers.BuildMapKey(method);

        return mapKey switch {
            "Nullable_.ctor" => GetNullableCtor(method.templateArguments[0].type.type),
            "Nullable_get_Value" => GetNullableValue(method.templateArguments[0].type.type),
            "Nullable_get_HasValue" => GetNullableHasValue(method.templateArguments[0].type.type),
            _ => _stlMap[mapKey],
        };
    }

    private void ResolveMethods() {
        NetMethodReference.Object_Equals_OO = ResolveMethod("System.Object", "Equals", ["System.Object", "System.Object"]);
        NetMethodReference.Object_ToString = ResolveMethod("System.Object", "ToString", []);
        NetMethodReference.String_Concat_SS = ResolveMethod("System.String", "Concat", ["System.String", "System.String"]);
        NetMethodReference.String_Concat_SSS = ResolveMethod("System.String", "Concat", ["System.String", "System.String", "System.String"]);
        NetMethodReference.String_Concat_SSSS = ResolveMethod("System.String", "Concat", ["System.String", "System.String", "System.String", "System.String"]);
        NetMethodReference.String_Concat_A = ResolveMethod("System.String", "Concat", ["System.String[]"]);
        NetMethodReference.String_Equality_SS = ResolveMethod("System.String", "op_Equality", ["System.String", "System.String"]);
        NetMethodReference.Convert_ToBoolean_S = ResolveMethod("System.Convert", "ToBoolean", ["System.String"]);
        NetMethodReference.Convert_ToInt64_S = ResolveMethod("System.Convert", "ToInt64", ["System.String"]);
        NetMethodReference.Convert_ToInt64_D = ResolveMethod("System.Convert", "ToInt64", ["System.Double"]);
        NetMethodReference.Convert_ToDouble_S = ResolveMethod("System.Convert", "ToDouble", ["System.String"]);
        NetMethodReference.Convert_ToDouble_I = ResolveMethod("System.Convert", "ToDouble", ["System.Int64"]);
        NetMethodReference.Convert_ToString_I = ResolveMethod("System.Convert", "ToString", ["System.Int64"]);
        NetMethodReference.Convert_ToString_D = ResolveMethod("System.Convert", "ToString", ["System.Double"]);
        NetMethodReference.Random_ctor = ResolveMethod("System.Random", ".ctor", []);
        NetMethodReference.Random_NextInt64_I = ResolveMethod("System.Random", "NextInt64", ["System.Int64"]);
        NetMethodReference.Random_NextDouble = ResolveMethod("System.Random", "NextDouble", []);
        NetMethodReference.Nullable_ctor = ResolveMethod("System.Nullable`1", ".ctor", ["T"]);
        NetMethodReference.Nullable_Value = ResolveMethod("System.Nullable`1", "get_Value", []);
        NetMethodReference.Nullable_HasValue = ResolveMethod("System.Nullable`1", "get_HasValue", []);
        NetMethodReference.Type_GetTypeFromHandle = ResolveMethod("System.Type", "GetTypeFromHandle", ["System.RuntimeTypeHandle"]);
        NetMethodReference.NullReferenceException_ctor = ResolveMethod("System.NullReferenceException", ".ctor", []);
        NetMethodReference.NullConditionException_ctor = ResolveMethod("Belte.Runtime.NullConditionException", ".ctor", []);
        NetMethodReference.Array_Sort = ResolveMethod("System.Array", "Sort", ["T[]"]);
    }

    private void GenerateSTLMap() {
        _stlMap = new Dictionary<string, MethodReference>() {
            { "Object_.ctor", ResolveMethod("System.Object", ".ctor", []) },
            { "Console_Clear", ResolveMethod("System.Console", "Clear", []) },
            { "Console_GetWidth", ResolveMethod("Belte.Runtime.Console", "GetWidth", []) },
            { "Console_GetHeight", ResolveMethod("Belte.Runtime.Console", "GetHeight", []) },
            { "Console_Print_S?", ResolveMethod("System.Console", "Write", ["System.String"]) },
            { "Console_Print_A?", ResolveMethod("System.Console", "Write", ["System.Object"]) },
            { "Console_Print_O?", ResolveMethod("System.Console", "Write", ["System.Object"]) },
            { "Console_PrintLine", ResolveMethod("System.Console", "WriteLine", []) },
            { "Console_PrintLine_S?", ResolveMethod("System.Console", "WriteLine", ["System.String"]) },
            { "Console_PrintLine_A?", ResolveMethod("System.Console", "WriteLine", ["System.Object"]) },
            { "Console_PrintLine_O?", ResolveMethod("System.Console", "WriteLine", ["System.Object"]) },
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
            { "LowLevel_GetHashCode_O", ResolveMethod("Belte.Runtime.Utilities", "GetHashCode", ["System.Object"]) },
            { "LowLevel_GetTypeName_O", ResolveMethod("Belte.Runtime.Utilities", "GetTypeName", ["System.Object"]) },
            { "LowLevel_Sort_A?", ResolveMethod("Belte.Runtime.Utilities", "Sort", ["System.Object[]"]) },
            { "LowLevel_Length_A?", ResolveMethod("Belte.Runtime.Utilities", "Length", ["System.Object[]"]) },
            { "LowLevel_ThrowNullConditionException", ResolveMethod("Belte.Runtime.ThrowHelper", "ThrowNullConditionException", []) },
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
        };
    }
}

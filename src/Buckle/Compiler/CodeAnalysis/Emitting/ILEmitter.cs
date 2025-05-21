using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Shared;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed partial class ILEmitter {
    private readonly BelteDiagnosticQueue _diagnostics;
    private readonly AssemblyDefinition _assemblyDefinition;
    private readonly List<AssemblyDefinition> _assemblies;
    private readonly BoundProgram _program;
    private readonly ImmutableArray<NamedTypeSymbol> _topLevelTypes;

    private readonly Dictionary<SpecialType, TypeReference> _specialTypes = [];
    private readonly Dictionary<TypeSymbol, TypeDefinition> _types = [];
    private readonly Dictionary<MethodSymbol, MethodDefinition> _methods = [];
    private readonly Dictionary<MethodDefinition, (MethodSymbol, BoundBlockStatement)> _methodBodies = [];
    private readonly Dictionary<FieldSymbol, FieldDefinition> _fields = [];

    private TypeDefinition _globalsClass;
    private FieldDefinition _randomField;

    private ILEmitter(BoundProgram program, string moduleName, string[] references, BelteDiagnosticQueue diagnostics) {
        _diagnostics = diagnostics;
        _program = program;

        _assemblies = [
            AssemblyDefinition.ReadAssembly(typeof(object).Assembly.Location),      // System.Private.CoreLib
            AssemblyDefinition.ReadAssembly(typeof(Console).Assembly.Location),     // System.Console
        ];

        foreach (var reference in references) {
            try {
                var assembly = AssemblyDefinition.ReadAssembly(reference);
                _assemblies.Add(assembly);
            } catch (BadImageFormatException) {
                _diagnostics.Push(Error.InvalidReference(reference));
            }
        }

        var assemblyName = new AssemblyNameDefinition(moduleName, new Version(1, 0));
        _assemblyDefinition = AssemblyDefinition.CreateAssembly(assemblyName, moduleName, ModuleKind.Console);

        ResolveTypes();
        ResolveMethods();

        _topLevelTypes = program.types.Where(t => t.containingSymbol.kind == SymbolKind.Namespace).ToImmutableArray();
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

        using (var indentedTextWriter = new IndentedTextWriter(stringWriter, "    ")) {
            foreach (var type in _topLevelTypes) {
                var typeDefinition = _types[type.originalDefinition];
                WriteType(stringWriter, indentedTextWriter, typeDefinition);
            }

            stringWriter.Flush();
        }

        return stringWriter.ToString();

        static void WriteType(
            StringWriter writer,
            IndentedTextWriter indentedTextWriter,
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

    private TypeReference GetType(TypeSymbol type) {
        if (type.specialType == SpecialType.Nullable && CodeGenerator.IsValueType(type.GetNullableUnderlyingType())) {
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
            var arrayType = elementType.MakeArrayType(array.rank);
            return arrayType.Resolve();
        }

        if (type.specialType != SpecialType.None)
            return _specialTypes[type.specialType];

        return _types[type.originalDefinition];
    }

    private MethodReference GetMethod(MethodSymbol method) {
        if (_methods.TryGetValue(method, out var value))
            return value;

        return CheckStandardMap(method);
    }

    private MethodReference GetNullableCtor(TypeSymbol genericType) {
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
            genericCtor.Parameters.Add(new ParameterDefinition(p.ParameterType));

        return _assemblyDefinition.MainModule.ImportReference(genericCtor);
    }

    private MethodReference GetNullableValue(TypeSymbol genericType) {
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

    private MethodReference GetNullableHasValue(TypeSymbol genericType) {
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

    private FieldReference GetField(FieldSymbol field) {
        return _fields[field];
    }

    private void EmitGlobalsClass() {
        _globalsClass = new TypeDefinition(
            "",
            "<Globals>",
            TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Public
        );

        _randomField = new FieldDefinition(
            "<random>",
            FieldAttributes.Static | FieldAttributes.Public,
            NetTypeReference.Random
        );

        _globalsClass.Fields.Add(_randomField);

        var cctor = new MethodDefinition(
            ".cctor",
            MethodAttributes.Static | MethodAttributes.Private |
            MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            _specialTypes[SpecialType.Void]
        );

        _globalsClass.Methods.Insert(0, cctor);

        var iLProcessor = cctor.Body.GetILProcessor();
        iLProcessor.Emit(OpCodes.Newobj, NetMethodReference.Random_ctor);
        iLProcessor.Emit(OpCodes.Stsfld, _randomField);
        iLProcessor.Emit(OpCodes.Ret);

        _assemblyDefinition.MainModule.Types.Add(_globalsClass);
    }

    private void EmitInternal() {
        foreach (var type in _topLevelTypes) {
            var typeDefinition = CreateNamedTypeDefinition(type);
            _assemblyDefinition.MainModule.Types.Add(typeDefinition);
        }

        foreach (var type in _topLevelTypes)
            CreateMemberDefinitions(type);

        foreach (var type in _topLevelTypes)
            EmitNamedType(type);

        if (_program.entryPoint is not null)
            _assemblyDefinition.EntryPoint = _methods[_program.entryPoint];
    }

    private TypeDefinition CreateNamedTypeDefinition(NamedTypeSymbol type, bool isNested = false) {
        var typeDefinition = new TypeDefinition(
            "",
            type.name,
            GetTypeAttributes(type, isNested),
            type.typeKind == TypeKind.Struct ? NetTypeReference.ValueType : _specialTypes[SpecialType.Object]
        );

        foreach (var member in type.GetTypeMembers()) {
            var nestedDefinition = CreateNamedTypeDefinition(member, isNested: true);
            typeDefinition.NestedTypes.Add(nestedDefinition);
        }

        _types.Add(type.originalDefinition, typeDefinition);
        return typeDefinition;
    }

    private void CreateMemberDefinitions(NamedTypeSymbol type) {
        var typeDefinition = _types[type.originalDefinition];

        foreach (var member in type.GetMembers()) {
            if (member is FieldSymbol f) {
                var fieldDefinition = new FieldDefinition(f.name, GetFieldAttributes(f), GetType(f.type));
                _fields.Add(f, fieldDefinition);
                typeDefinition.Fields.Add(fieldDefinition);
            } else if (member is NamedTypeSymbol t) {
                CreateMemberDefinitions(t);
            }
        }

        // Checking program map for methods to make sure synthesized ones are included (such as closure methods)
        foreach (var pair in _program.methodBodies) {
            if (pair.Key.containingType.Equals(type))
                CreateMethodDefinition(pair.Key, pair.Value, typeDefinition);
        }
    }

    private void CreateMethodDefinition(MethodSymbol method, BoundBlockStatement body, TypeDefinition containingType) {
        var methodDefinition = new MethodDefinition(
            method.name,
            GetMethodAttributes(method),
            GetType(method.returnType)
        );

        foreach (var parameter in method.parameters) {
            var parameterDefinition = new ParameterDefinition(
                parameter.name,
                ParameterAttributes.None,
                GetType(parameter.type)
            );

            methodDefinition.Parameters.Add(parameterDefinition);
        }

        _methods.Add(method, methodDefinition);
        _methodBodies.Add(methodDefinition, (method, body));
        containingType.Methods.Add(methodDefinition);
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
            Accessibility.Public => FieldAttributes.Private,
            Accessibility.Protected => FieldAttributes.Family,
            _ => 0
        };

        if (field.isStatic)
            attributes |= FieldAttributes.Static;

        return attributes;
    }

    private static MethodAttributes GetMethodAttributes(MethodSymbol method) {
        MethodAttributes attributes = method.declaredAccessibility switch {
            Accessibility.Private => MethodAttributes.Private,
            Accessibility.Public => MethodAttributes.Private,
            Accessibility.Protected => MethodAttributes.Family,
            _ => 0
        };

        if (method.isStatic)
            attributes |= MethodAttributes.Static;
        if (method.isAbstract)
            attributes |= MethodAttributes.Abstract;
        if (method.isVirtual)
            attributes |= MethodAttributes.Virtual;
        if (method.overriddenOrHiddenMembers.hiddenMembers.Any())
            attributes |= MethodAttributes.HideBySig;

        return attributes;
    }

    private void EmitNamedType(NamedTypeSymbol type) {
        var typeDefinition = _types[type];

        foreach (var method in typeDefinition.Methods)
            EmitMethod(method);

        foreach (var member in type.GetTypeMembers())
            EmitNamedType(member);
    }

    private void EmitMethod(MethodDefinition methodDefinition) {
        var methodAndBody = _methodBodies[methodDefinition];

        var codeGen = new CodeGenerator(this, methodAndBody.Item1, methodAndBody.Item2, methodDefinition);
        codeGen.Generate();

        methodDefinition.Body.OptimizeMacros();
    }

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

        if (foundTypes.Length == 1) {
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

                return _assemblyDefinition.MainModule.ImportReference(method);
            }

            throw new BelteInternalException(
                $"Required method not found: {typeName} {methodName} {parameterTypeNames.Length}"
            );
        } else if (foundTypes.Length == 0) {
            throw new BelteInternalException($"Required type not found: {typeName}");
        } else {
            throw new BelteInternalException(
                $"Required type not found: {typeName}; found {foundTypes.Length} candidates"
            );
        }
    }

    private void ResolveMethods() {
        NetMethodReference.Object_ctor = ResolveMethod("System.Object", ".ctor", []);
        NetMethodReference.Object_Equals_OO = ResolveMethod("System.Object", "Equals", ["System.Object", "System.Object"]);
        NetMethodReference.Console_Write_S = ResolveMethod("System.Console", "Write", ["System.String"]);
        NetMethodReference.Console_Write_O = ResolveMethod("System.Console", "Write", ["System.Object"]);
        NetMethodReference.Console_WriteLine = ResolveMethod("System.Console", "WriteLine", []);
        NetMethodReference.Console_WriteLine_S = ResolveMethod("System.Console", "WriteLine", ["System.String"]);
        NetMethodReference.Console_WriteLine_O = ResolveMethod("System.Console", "WriteLine", ["System.Object"]);
        NetMethodReference.Console_ReadLine = ResolveMethod("System.Console", "ReadLine", []);
        NetMethodReference.String_Concat_SS = ResolveMethod("System.String", "Concat", ["System.String", "System.String"]);
        NetMethodReference.String_Concat_SSS = ResolveMethod("System.String", "Concat", ["System.String", "System.String", "System.String"]);
        NetMethodReference.String_Concat_SSSS = ResolveMethod("System.String", "Concat", ["System.String", "System.String", "System.String", "System.String"]);
        NetMethodReference.String_Concat_A = ResolveMethod("System.String", "Concat", ["System.String[]"]);
        NetMethodReference.Convert_ToBoolean_O = ResolveMethod("System.Convert", "ToBoolean", ["System.Object"]);
        NetMethodReference.Convert_ToInt32_O = ResolveMethod("System.Convert", "ToInt32", ["System.Object"]);
        NetMethodReference.Convert_ToInt64_O = ResolveMethod("System.Convert", "ToInt64", ["System.Object"]);
        NetMethodReference.Convert_ToSingle_O = ResolveMethod("System.Convert", "ToSingle", ["System.Object"]);
        NetMethodReference.Convert_ToDouble_O = ResolveMethod("System.Convert", "ToDouble", ["System.Object"]);
        NetMethodReference.Convert_ToString_O = ResolveMethod("System.Convert", "ToString", ["System.Object"]);
        NetMethodReference.Random_ctor = ResolveMethod("System.Random", ".ctor", []);
        NetMethodReference.Random_Next_I = ResolveMethod("System.Random", "Next", ["System.Int32"]);
        NetMethodReference.Nullable_ctor = ResolveMethod("System.Nullable`1", ".ctor", ["T"]);
        NetMethodReference.Nullable_Value = ResolveMethod("System.Nullable`1", "get_Value", []);
        NetMethodReference.Nullable_HasValue = ResolveMethod("System.Nullable`1", "get_HasValue", []);
        NetMethodReference.Type_GetTypeFromHandle = ResolveMethod("System.Type", "GetTypeFromHandle", ["System.RuntimeTypeHandle"]);
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
        };

        foreach (var (type, metadataName) in builtInTypes) {
            var typeReference = ResolveType(CorLibrary.GetSpecialType(type).name, metadataName);
            _specialTypes.Add(type, typeReference);
        }

        NetTypeReference.Random = ResolveType(null, "System.Random");
        NetTypeReference.Nullable = ResolveType(null, "System.Nullable`1");
        NetTypeReference.ValueType = ResolveType(null, "System.ValueType");
    }

    private MethodReference CheckStandardMap(MethodSymbol method) {
        var mapKey = LibraryHelpers.BuildMapKey(method);

        switch (mapKey) {
            case "Object_.ctor":
                return NetMethodReference.Object_ctor;
            case "Nullable_.ctor":
                return GetNullableCtor(method.templateArguments[0].type.type);
            case "Nullable_get_Value":
                return GetNullableValue(method.templateArguments[0].type.type);
            case "Nullable_get_HasValue":
                return GetNullableHasValue(method.templateArguments[0].type.type);
            case "Console_Print_S?":
                return NetMethodReference.Console_Write_S;
            case "Console_Print_O?":
                return NetMethodReference.Console_Write_O;
            case "Console_PrintLine":
                return NetMethodReference.Console_WriteLine;
            case "Console_PrintLine_S?":
                return NetMethodReference.Console_WriteLine_S;
            case "Console_PrintLine_O?":
                return NetMethodReference.Console_WriteLine_O;
            case "Console_Input":
                return NetMethodReference.Console_ReadLine;
            default:
                throw ExceptionUtilities.UnexpectedValue(mapKey);
        }
    }
}

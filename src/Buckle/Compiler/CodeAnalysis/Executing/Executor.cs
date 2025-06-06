using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Evaluating;

internal sealed class Executor : ModuleBuilder {
    private readonly BoundProgram _program;

    private readonly ImmutableArray<NamedTypeSymbol> _topLevelTypes;

    private readonly Dictionary<SpecialType, Type> _specialTypes = new Dictionary<SpecialType, Type>{
        { SpecialType.Object, typeof(object) },
        { SpecialType.Any, typeof(object) },
        { SpecialType.Bool, typeof(bool) },
        { SpecialType.Int, typeof(long) },
        { SpecialType.Decimal, typeof(double) },
        { SpecialType.Nullable, typeof(Nullable) },
        { SpecialType.Void, typeof(void) },
        { SpecialType.Type, typeof(Type) },
    };

    private readonly Dictionary<TypeSymbol, TypeBuilder> _types = [];
    private readonly Dictionary<MethodSymbol, MethodBuilder> _methods = [];
    private readonly Dictionary<MethodBuilder, (MethodSymbol, BoundBlockStatement)> _methodBodies = [];
    private readonly Dictionary<FieldSymbol, FieldBuilder> _fields = [];

    private readonly AssemblyBuilder _assemblyBuilder;
    private readonly System.Reflection.Emit.ModuleBuilder _moduleBuilder;

    private NamedTypeSymbol _programNamedType;
    private Type _programType;

    internal Executor(BoundProgram program, string[] arguments) {
        _program = program;

        _topLevelTypes = program.types.Where(t => t.containingSymbol.kind == SymbolKind.Namespace).ToImmutableArray();

        var assemblyName = new AssemblyName("DynamicBoundTreeAssembly");
        _assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        _moduleBuilder = _assemblyBuilder.DefineDynamicModule("MainModule");
    }

    internal object Execute() {
        /*

        Generates and executes the following dynamic method:

        object DynamicEvaluate(Executor executor) {
            <Program> <program> = new <Program>();
            return <program>.Main(executor);
        }

        The method varies depending on whether or not <Program> is static and <Program>.Main returns void

        */
        var entryPoint = _program.entryPoint;

        if (entryPoint is null)
            return null;

        _programNamedType = entryPoint.containingType;

        EmitInternal();

        var evaluateMethod = new DynamicMethod(
            "DynamicEvaluate",
            entryPoint.returnsVoid ? typeof(void) : typeof(object),
            [typeof(Executor)],
            typeof(Executor),
            true
        );

        var iLGenerator = evaluateMethod.GetILGenerator();

        if (_programNamedType.isStatic) {
            var mainMethod = _programType.GetMethod("Main", BindingFlags.Public | BindingFlags.Static);

            iLGenerator.Emit(OpCodes.Ldarg_0);
            iLGenerator.Emit(OpCodes.Call, mainMethod);

            if (entryPoint.returnsVoid)
                iLGenerator.Emit(OpCodes.Ldnull);

            iLGenerator.Emit(OpCodes.Ret);
        } else {
            var mainMethod = _programType.GetMethod("Main", BindingFlags.Public | BindingFlags.Instance);

            var programCtor = _programType.GetConstructor(Type.EmptyTypes);
            iLGenerator.DeclareLocal(_programType);
            iLGenerator.Emit(OpCodes.Newobj, programCtor);
            iLGenerator.Emit(OpCodes.Stloc_0);

            iLGenerator.Emit(OpCodes.Ldloc_0);
            iLGenerator.Emit(OpCodes.Ldarg_0);
            iLGenerator.Emit(OpCodes.Callvirt, mainMethod);

            if (entryPoint.returnsVoid)
                iLGenerator.Emit(OpCodes.Ldnull);

            iLGenerator.Emit(OpCodes.Ret);
        }

        var evaluateDelegate = (Func<Executor, object>)evaluateMethod.CreateDelegate(typeof(Func<Executor, object>));
        return evaluateDelegate(this);
    }

    internal Type GetType(TypeSymbol type) {
        if (type.specialType == SpecialType.Nullable) {
            var underlyingType = type.GetNullableUnderlyingType();
            var genericArgumentType = GetType(underlyingType);

            if (!CodeGenerator.IsValueType(underlyingType))
                return genericArgumentType;

            return typeof(Nullable).MakeGenericType(genericArgumentType);
        }

        if (type is ArrayTypeSymbol array) {
            var elementType = GetType(array.elementType);
            return elementType.MakeArrayType(array.rank);
        }

        if (type.specialType != SpecialType.None)
            return _specialTypes[type.specialType];

        return _types[type.originalDefinition];
    }

    internal override void EmitGlobalsClass() { }

    private void EmitInternal() {
        foreach (var type in _topLevelTypes)
            CreateTypeBuilder(type);

        foreach (var type in _topLevelTypes)
            CreateMemberDefinitions(type);

        foreach (var method in _methods)
            EmitMethod(method.Value);

        foreach (var type in _types.Values) {
            if (type is TypeBuilder t) {
                if (type.Equals(_programNamedType))
                    _programType = t.CreateType();
                else
                    t.CreateType();
            }
        }
    }

    private void CreateTypeBuilder(NamedTypeSymbol type) {
        var typeBuilder = _moduleBuilder.DefineType(type.name, GetTypeAttributes(type, false));
        CreateNestedTypes(type, typeBuilder);
        _types.Add(type.originalDefinition, typeBuilder);
    }

    private void CreateNestedTypes(NamedTypeSymbol type, TypeBuilder typeBuilder) {
        foreach (var member in type.GetTypeMembers()) {
            var nestedBuilder = typeBuilder.DefineNestedType(member.name, GetTypeAttributes(member, true));
            CreateNestedTypes(member, nestedBuilder);
            _types.Add(member.originalDefinition, nestedBuilder);
        }
    }

    private TypeAttributes GetTypeAttributes(NamedTypeSymbol type, bool isNested) {
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

    private void CreateMemberDefinitions(NamedTypeSymbol type) {
        var typeBuilder = _types[type.originalDefinition] as TypeBuilder;

        foreach (var member in type.GetMembers()) {
            if (member is FieldSymbol f) {
                var fieldBuilder = typeBuilder.DefineField(f.name, GetType(f.type), GetFieldAttributes(f));
                _fields.Add(f, fieldBuilder);
            } else if (member is NamedTypeSymbol t) {
                CreateMemberDefinitions(t);
            }
        }

        foreach (var pair in _program.methodBodies) {
            if (pair.Key.containingType.Equals(type)) {
                CreateMethodDefinition(pair.Key, pair.Value, typeBuilder);
            }
        }
    }

    private void CreateMethodDefinition(MethodSymbol method, BoundBlockStatement body, TypeBuilder typeBuilder) {
        var methodBuilder = typeBuilder.DefineMethod(
            method.name,
            GetMethodAttributes(method),
            GetType(method.returnType),
            method.parameters.Select(p => GetType(p.type)).Append(typeof(Executor)).ToArray()
        );

        _methods.Add(method, methodBuilder);
        _methodBodies.Add(methodBuilder, (method, body));
    }

    private void EmitMethod(MethodBuilder methodBuilder) {
        var (method, body) = _methodBodies[methodBuilder];
        var ilBuilder = new RefILBuilder(method, this, methodBuilder.GetILGenerator());
        var codeGen = new CodeGenerator(this, method, body, ilBuilder);
        codeGen.Generate();
    }
}

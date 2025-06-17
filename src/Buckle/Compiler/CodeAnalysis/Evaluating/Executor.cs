using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Libraries;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Evaluating;

internal sealed partial class Executor : ModuleBuilder {
    public static readonly Random Random = new Random();
    public static GraphicsHandler GraphicsHandler;
    public static object Program;

    private readonly BoundProgram _program;

    private readonly ImmutableArray<NamedTypeSymbol> _topLevelTypes;

    private readonly Dictionary<SpecialType, Type> _specialTypes = new Dictionary<SpecialType, Type>{
        { SpecialType.Object, typeof(object) },
        { SpecialType.Any, typeof(object) },
        { SpecialType.Bool, typeof(bool) },
        { SpecialType.Int, typeof(long) },
        { SpecialType.Decimal, typeof(double) },
        { SpecialType.Nullable, typeof(Nullable<>) },
        { SpecialType.Void, typeof(void) },
        { SpecialType.Type, typeof(Type) },
        { SpecialType.String, typeof(string) },
        { SpecialType.Sprite, typeof(BSprite) },
        { SpecialType.Rect, typeof(BRect) },
        { SpecialType.Vec2, typeof(BVec2) },
        { SpecialType.Texture, typeof(BTexture) },
        { SpecialType.Text, typeof(BText) },
    };

    private readonly Dictionary<TypeSymbol, TypeBuilder> _types = [];
    private readonly Dictionary<MethodSymbol, MethodInfo> _methods = [];
    private readonly Dictionary<MethodSymbol, ConstructorInfo> _constructors = [];
    private readonly Dictionary<MethodSymbol, BoundBlockStatement> _methodBodies = [];
    private readonly Dictionary<ConstructorBuilder, (MethodSymbol, BoundBlockStatement)> _constructorBodies = [];
    private readonly Dictionary<FieldSymbol, FieldInfo> _fields = [];

    private readonly System.Reflection.Emit.ModuleBuilder _moduleBuilder;
    private readonly bool _graphicsEnabled;
    private readonly string[] _arguments;

    private NamedTypeSymbol _programNamedType;
    private Type _programType;
    private Dictionary<string, MethodInfo> _stlMap;
    private bool _graphicsInitialized;
    // Used for debugging
    private bool _logIL;

    internal FieldInfo randomField;
    internal FieldInfo graphicsHandlerField;
    internal FieldInfo programField;

    internal Executor(BoundProgram program, string[] arguments) {
        _arguments = arguments;
        _program = program;
        _graphicsEnabled = program.compilation.options.outputKind == OutputKind.Graphics;

        _topLevelTypes = program.types.Where(t => t.containingSymbol.kind == SymbolKind.Namespace).ToImmutableArray();

        var assemblyName = new AssemblyName("DynamicBoundTreeAssembly");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        _moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
    }

    public static T AssertNull<T>(T value) {
        if (value is null)
            throw new NullReferenceException();

        return value;
    }

    internal object Execute(bool log) {
        var entryPoint = _program.entryPoint;

        if (entryPoint is null)
            return null;

        _programNamedType = entryPoint.containingType;
        graphicsHandlerField = typeof(Executor).GetField("GraphicsHandler", BindingFlags.Public | BindingFlags.Static);

        EmitInternal();

        if (!_programNamedType.isStatic) {
            Program = Activator.CreateInstance(_programType);
            programField = typeof(Executor).GetField("Program");
        }

        if (_graphicsEnabled && _graphicsInitialized)
            GraphicsHandler = new GraphicsHandler(false, false);

        var mainMethod = _programType.GetMethod(
            "Main",
            _programNamedType.isStatic
                ? BindingFlags.Public | BindingFlags.Static
                : BindingFlags.Public | BindingFlags.Instance
        );

        if (_graphicsEnabled && _graphicsInitialized && _program.updatePoint is not null) {
            var updateMethod = _programType.GetMethod(
                "Update",
                _programNamedType.isStatic
                    ? BindingFlags.Public | BindingFlags.Static
                    : BindingFlags.Public | BindingFlags.Instance
            );

            var updateAction = (Action<double>)Delegate.CreateDelegate(typeof(Action<double>), Program, updateMethod);
            GraphicsHandler.SetExecuteHandler(updateAction);
        }

        if (log) {
            var assemblyPath = "DynamicBoundTreeAssembly.dll";
            Console.WriteLine($"Dumping dynamic executor assembly to '{assemblyPath}'");
            var generator = new Lokad.ILPack.AssemblyGenerator();
            generator.GenerateAssembly(_programType.Assembly, assemblyPath);
        }

        if (_graphicsEnabled && _graphicsInitialized) {
            var mainAction = (Action)Delegate.CreateDelegate(typeof(Action), Program, mainMethod);
            GraphicsHandler.SetExecuteMain(mainAction);
            GraphicsHandler.Run();
            return null;
        }

        return mainMethod.Invoke(Program, _program.entryPoint.parameterCount == 0 ? null : [_arguments]);
    }

    internal Type GetType(TypeSymbol type) {
        if (type.specialType == SpecialType.Nullable) {
            var underlyingType = type.GetNullableUnderlyingType();
            var genericArgumentType = GetType(underlyingType);

            if (!CodeGenerator.IsValueType(underlyingType))
                return genericArgumentType;

            return typeof(Nullable<>).MakeGenericType(genericArgumentType);
        }

        if (type is ArrayTypeSymbol array) {
            var elementType = GetType(array.elementType);
            return elementType.MakeArrayType(array.rank);
        }

        if (type.specialType != SpecialType.None && _specialTypes.TryGetValue(type.specialType, out var value))
            return value;

        return _types[type.originalDefinition];
    }

    internal FieldInfo GetField(FieldSymbol field) {
        return _fields[field];
    }

    internal MethodInfo GetMethod(MethodSymbol method) {
        if (_methods.TryGetValue(method, out var value))
            return value;

        return CheckStandardMap(method);
    }

    internal ConstructorInfo GetConstructor(MethodSymbol method) {
        if (_constructors.TryGetValue(method, out var value))
            return value;

        return CheckConstructorsStandardMap(method);
    }

    internal override void EmitGlobalsClass() {
        randomField = typeof(Executor).GetField("Random", BindingFlags.Public | BindingFlags.Static);
    }

    internal MethodInfo GetNullAssert(TypeSymbol type) {
        var assertNull = typeof(Executor).GetMethod("AssertNull", BindingFlags.Public | BindingFlags.Static);
        var closedMethod = assertNull.MakeGenericMethod(GetType(type));
        return closedMethod;
    }

    internal ConstructorInfo GetNullableCtor(TypeSymbol type) {
        var generic = GetType(type);
        var nullable = typeof(Nullable<>);
        var closedType = nullable.MakeGenericType(generic);
        return closedType.GetConstructor([generic]);
    }

    internal MethodInfo GetNullableValue(TypeSymbol type) {
        var generic = GetType(type);
        var nullable = typeof(Nullable<>);
        var closedType = nullable.MakeGenericType(generic);
        return closedType.GetMethod("get_Value", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
    }

    internal MethodInfo GetNullableHasValue(TypeSymbol type) {
        var generic = GetType(type);
        var nullable = typeof(Nullable<>);
        var closedType = nullable.MakeGenericType(generic);
        return closedType.GetMethod("get_HasValue", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
    }

    private void EmitInternal() {
        GenerateSTLMap();
        CompleteSpecialTypes();

        foreach (var type in _topLevelTypes)
            CreateTypeBuilder(type);

        foreach (var type in _topLevelTypes)
            CreateMemberDefinitions(type);

        foreach (var method in _methods) {
            if (method.Value is MethodBuilder mb)
                EmitMethod(method.Key, mb);
        }

        foreach (var method in _constructors) {
            if (method.Value is ConstructorBuilder cb)
                EmitConstructor(cb);
        }

        foreach (var (typeSymbol, typeBuilder) in _types) {
            if (typeSymbol.Equals(_programNamedType.originalDefinition))
                _programType = typeBuilder.CreateType();
            else
                typeBuilder.CreateType();
        }
    }

    private void CompleteSpecialTypes() {
        if (!_graphicsEnabled)
            return;

        foreach (var type in new[] { SpecialType.Rect, SpecialType.Text, SpecialType.Sprite,
                              SpecialType.Vec2, SpecialType.Texture }) {
            var typeSymbol = CorLibrary.GetSpecialType(type);
            var native = _specialTypes[type];

            foreach (var member in typeSymbol.GetMembers()) {
                if (member is FieldSymbol f) {
                    _fields.Add(f, native.GetField(f.name));
                } else if (member is MethodSymbol m) {
                    if (m.methodKind == MethodKind.Constructor) {
                        _constructors.Add(
                            m,
                            native.GetConstructor(m.parameters.Select(p => GetType(p.type)).ToArray())
                        );
                    } else {
                        _methods.Add(m, native.GetMethod(
                            m.name,
                            BindingFlags.Public | BindingFlags.Instance,
                            m.parameters.Select(p => GetType(p.type)).ToArray()
                        ));
                    }
                }
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
        var attributes = MethodAttributes.Public;

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
        var typeBuilder = _types[type.originalDefinition];

        foreach (var member in type.GetMembers()) {
            if (member is FieldSymbol f) {
                var fieldBuilder = typeBuilder.DefineField(f.name, GetType(f.type), GetFieldAttributes(f));
                _fields.Add(f, fieldBuilder);
            } else if (member is NamedTypeSymbol t) {
                CreateMemberDefinitions(t);
            }
        }

        foreach (var pair in _program.methodBodies) {
            if (pair.Key.containingType.originalDefinition.Equals(type))
                CreateMethodDefinition(pair.Key, pair.Value, typeBuilder);
        }
    }

    private void CreateMethodDefinition(MethodSymbol method, BoundBlockStatement body, TypeBuilder typeBuilder) {
        if (method.methodKind == MethodKind.Constructor)
            CreateConstructorDefinition(method, body, typeBuilder);
        else
            CreateNormalMethodDefinition(method, body, typeBuilder);
    }

    private void CreateConstructorDefinition(MethodSymbol method, BoundBlockStatement body, TypeBuilder typeBuilder) {
        var constructorBuilder = typeBuilder.DefineConstructor(
            GetMethodAttributes(method),
            CallingConventions.Standard,
            method.parameters.Select(p => GetType(p.type)).ToArray()
        );

        _constructors.Add(method, constructorBuilder);
        _constructorBodies.Add(constructorBuilder, (method, body));
    }

    private void CreateNormalMethodDefinition(MethodSymbol method, BoundBlockStatement body, TypeBuilder typeBuilder) {
        var methodBuilder = typeBuilder.DefineMethod(
            method.name,
            GetMethodAttributes(method),
            GetType(method.returnType),
            method.parameters.Select(p => GetType(p.type)).ToArray()
        );

        _methods.Add(method, methodBuilder);
        _methodBodies.Add(method, body);
    }

    private void EmitMethod(MethodSymbol method, MethodBuilder methodBuilder) {
        if (_logIL)
            Console.WriteLine($"Emitting method {method}");

        var body = _methodBodies[method];
        var ilBuilder = new RefILBuilder(method, this, methodBuilder.GetILGenerator(), _logIL);
        var codeGen = new CodeGenerator(this, method, body, ilBuilder);
        codeGen.Generate();
    }

    private void EmitConstructor(ConstructorBuilder constructorBuilder) {
        var (constructor, body) = _constructorBodies[constructorBuilder];

        if (_logIL)
            Console.WriteLine($"Emitting constructor {constructor}");

        var ilBuilder = new RefILBuilder(constructor, this, constructorBuilder.GetILGenerator(), _logIL);
        var codeGen = new CodeGenerator(this, constructor, body, ilBuilder);
        codeGen.Generate();
    }

    private ConstructorInfo CheckConstructorsStandardMap(MethodSymbol method) {
        var mapKey = LibraryHelpers.BuildMapKey(method);

        switch (mapKey) {
            case "Object_.ctor":
                return MethodInfoCache.Object_ctor;
            case "Nullable_.ctor":
                return GetNullableCtor(method.templateArguments[0].type.type);
            default:
                throw ExceptionUtilities.UnexpectedValue(mapKey);
        }
    }

    private MethodInfo CheckStandardMap(MethodSymbol method) {
        var mapKey = LibraryHelpers.BuildMapKey(method);

        switch (mapKey) {
            case "Nullable_get_Value":
                return GetNullableValue(method.templateArguments[0].type.type);
            case "Nullable_get_HasValue":
                return GetNullableHasValue(method.templateArguments[0].type.type);
            case "Graphics_Initialize_SIIB":
                _graphicsInitialized = true;
                goto default;
            default:
                return _stlMap[mapKey];
        }
    }

    #region Libraries

    public static double? Lerp(double? a, double? b, double? c) {
        return a + c * (b - a);
    }

    public static double Lerp(double a, double b, double c) {
        return a + c * (b - a);
    }

    public static double? Clamp(double? a, double? b, double? c) {
        if (a is null || b is null || c is null)
            return null;

        return Math.Clamp(a.Value, b.Value, c.Value);
    }

    public static double? Cos(double? a) {
        if (a is null)
            return null;

        return Math.Cos(a.Value);
    }

    public static double? Sin(double? a) {
        if (a is null)
            return null;

        return Math.Sin(a.Value);
    }

    public static void ThrowNullConditionException() {
        throw new NullConditionException();
    }

    public static void Fill(long r, long g, long b) {
        GraphicsHandler.Fill(r, g, b);
    }

    public static bool GetKey(string text) {
        return GraphicsHandler.GetKey(text);
    }

    public static long? DrawSprite(BSprite sprite) {
        GraphicsHandler.DrawSprite(
            sprite.texture.mTexture,
            (int)sprite.src.x,
            (int)sprite.src.y,
            (int)sprite.src.w,
            (int)sprite.src.h,
            (int)sprite.dst.x,
            (int)sprite.dst.y,
            (int)sprite.dst.w,
            (int)sprite.dst.h,
            sprite.rotation
        );

        return 0;
    }

    public static long? DrawText(BText text) {
        GraphicsHandler.DrawText(
            text.mFont,
            text.text,
            text.position.x,
            text.position.y,
            text.r,
            text.g,
            text.b
        );

        return 0;
    }

    public static void InitializeGraphics(string title, long width, long height, bool usePointClamp = false) {
        GraphicsHandler.Title = title;
        GraphicsHandler.Width = (int)width;
        GraphicsHandler.Height = (int)height;
        GraphicsHandler.SetUsePointClamp(usePointClamp);
    }

    public static BSprite LoadSprite(string path, BVec2 position, BVec2 scale, long? rotation) {
        var mTexture = GraphicsHandler.LoadTexture(path, false, 0, 0, 0);
        var texture = new BTexture(mTexture);
        return new BSprite(texture, position, scale, rotation);
    }

    public static BText LoadText(
        string text,
        string path,
        BVec2 position,
        double fontSize,
        double? angle,
        long? r,
        long? g,
        long? b) {
        var mText = GraphicsHandler.LoadText(path, (float)fontSize);
        return new BText(text, path, position, fontSize, angle, r, g, b, mText);
    }

    public static void LockFramerate(long fps) {
        GraphicsHandler.LockFramerate((int)fps);
    }

    private void GenerateSTLMap() {
        var flags = BindingFlags.Public | BindingFlags.Static;
        _stlMap = new Dictionary<string, MethodInfo>() {
            { "Console_Print_S?", typeof(Console).GetMethod("Write", flags, [typeof(string)]) },
            { "Console_Print_O?", typeof(Console).GetMethod("Write", flags, [typeof(object)]) },
            { "Console_PrintLine", typeof(Console).GetMethod("WriteLine", flags, Type.EmptyTypes) },
            { "Console_PrintLine_S?", typeof(Console).GetMethod("WriteLine", flags, [typeof(string)]) },
            { "Console_PrintLine_O?", typeof(Console).GetMethod("WriteLine", flags, [typeof(object)]) },
            { "Console_Input", typeof(Console).GetMethod("ReadLine", flags, Type.EmptyTypes) },
            { "Math_Clamp_D?D?D?", typeof(Executor).GetMethod("Clamp", flags, [typeof(double?), typeof(double?), typeof(double?)]) },
            { "Math_Lerp_D?D?D?", typeof(Executor).GetMethod("Lerp", flags, [typeof(double?), typeof(double?), typeof(double?)]) },
            { "Math_Lerp_DDD", typeof(Executor).GetMethod("Lerp", flags, [typeof(double), typeof(double), typeof(double)]) },
            { "Math_Cos_D?", typeof(Executor).GetMethod("Cos", flags, [typeof(double?)]) },
            { "Math_Sin_D?", typeof(Executor).GetMethod("Sin", flags, [typeof(double?)]) },
            { "LowLevel_ThrowNullConditionException", typeof(Executor).GetMethod("ThrowNullConditionException", flags, Type.EmptyTypes) },
            { "Graphics_Initialize_SIIB", typeof(Executor).GetMethod("InitializeGraphics", flags, [typeof(string), typeof(long), typeof(long), typeof(bool)]) },
            { "Graphics_Fill_III", typeof(Executor).GetMethod("Fill", flags, [typeof(long), typeof(long), typeof(long)]) },
            { "Graphics_GetKey_S", typeof(Executor).GetMethod("GetKey", flags, [typeof(string)]) },
            { "Graphics_DrawSprite_S?", typeof(Executor).GetMethod("DrawSprite", flags, [typeof(BSprite)]) },
            { "Graphics_DrawText_T?", typeof(Executor).GetMethod("DrawText", flags, [typeof(BText)]) },
            { "Graphics_LoadSprite_SV?V?I?", typeof(Executor).GetMethod("LoadSprite", flags, [typeof(string), typeof(BVec2), typeof(BVec2), typeof(long?)]) },
            { "Graphics_LoadText_S?SV?DD?I?I?I?", typeof(Executor).GetMethod("LoadText", flags, [typeof(string), typeof(string), typeof(BVec2), typeof(double), typeof(double?), typeof(long?), typeof(long?), typeof(long?)]) },
            { "Graphics_LockFramerate_I", typeof(Executor).GetMethod("LockFramerate", flags, [typeof(long)]) },
        };
    }

    #endregion
}

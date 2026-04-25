using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Evaluating;

internal sealed partial class Executor : ModuleBuilder {
    public static readonly Random Random = new Random();
    public static GraphicsHandler GraphicsHandler;
    public static object Program;

    private static readonly ConcurrentDictionary<string, Assembly> AssemblyCache = [];

    private const string DynamicAssemblyName = "DynamicBoundTreeAssembly";

    private readonly BoundProgram _program;
    private readonly ImmutableArray<NamedTypeSymbol> _topLevelTypes;
    private readonly ImmutableArray<NamedTypeSymbol> _linearNestedTypes;

    private readonly Dictionary<SpecialType, Type> _specialTypes = new Dictionary<SpecialType, Type>{
        { SpecialType.Object, typeof(object) },
        { SpecialType.Any, typeof(object) },
        { SpecialType.Bool, typeof(bool) },
        { SpecialType.Int, typeof(long) },
        { SpecialType.Int8, typeof(sbyte) },
        { SpecialType.Int16, typeof(short) },
        { SpecialType.Int32, typeof(int) },
        { SpecialType.Int64, typeof(long) },
        { SpecialType.UInt8, typeof(byte) },
        { SpecialType.UInt16, typeof(ushort) },
        { SpecialType.UInt32, typeof(uint) },
        { SpecialType.UInt64, typeof(ulong) },
        { SpecialType.Decimal, typeof(double) },
        { SpecialType.Float32, typeof(float) },
        { SpecialType.Float64, typeof(double) },
        { SpecialType.IntPtr, typeof(IntPtr) },
        { SpecialType.UIntPtr, typeof(UIntPtr) },
        { SpecialType.Nullable, typeof(Nullable<>) },
        { SpecialType.Char, typeof(char) },
        { SpecialType.Void, typeof(void) },
        { SpecialType.Type, typeof(Type) },
        { SpecialType.String, typeof(string) },
        { SpecialType.Sprite, typeof(BSprite) },
        { SpecialType.Rect, typeof(BRect) },
        { SpecialType.Vec2, typeof(BVec2) },
        { SpecialType.Texture, typeof(BTexture) },
        { SpecialType.Text, typeof(BText) },
        { SpecialType.Sound, typeof(BSound) },
        { SpecialType.Exception, typeof(Exception) },
    };

    private readonly Dictionary<TypeSymbol, TypeBuilder> _types = [];
    private readonly Dictionary<NamedTypeSymbol, Type> _bakedTypes = [];
    private readonly ConcurrentDictionary<TypeSymbol, EnumBuilder> _workingEnums = [];
    private readonly ConcurrentDictionary<TypeSymbol, Type> _enums = [];
    private readonly ConcurrentDictionary<MethodSymbol, MethodInfo> _methods = [];
    private readonly ConcurrentDictionary<MethodSymbol, GenericTypeParameterBuilder[]> _methodTypeParameters = [];
    private readonly ConcurrentDictionary<MethodSymbol, ConstructorInfo> _constructors = [];
    private readonly ConcurrentDictionary<MethodSymbol, BoundBlockStatement> _methodBodies = [];
    private readonly ConcurrentDictionary<ConstructorBuilder, (MethodSymbol, BoundBlockStatement)> _constructorBodies = [];
    private readonly ConcurrentDictionary<FieldSymbol, FieldInfo> _fields = [];

    private readonly System.Reflection.Emit.ModuleBuilder _moduleBuilder;
    private readonly bool _graphicsEnabled;
    private readonly string[] _arguments;
    private readonly BelteDiagnosticQueue _diagnostics;

    private NamedTypeSymbol _programNamedType;
    private Type _programType;
    private Dictionary<string, MethodInfo> _stlMap;
    private bool _graphicsInitialized;
    private StringWriter _logger;
    private volatile bool _reportedGraphicsCall;

    internal FieldInfo randomField;
    internal FieldInfo graphicsHandlerField;
    internal FieldInfo programField;

    internal Executor(BoundProgram program, string[] arguments, BelteDiagnosticQueue diagnostics) {
        _arguments = arguments;
        _program = program;
        _diagnostics = diagnostics;
        _graphicsEnabled = program.compilation.options.outputKind == OutputKind.GraphicsApplication;

        _topLevelTypes = program.GetTypesToEmit();

        var assemblyName = new AssemblyName(DynamicAssemblyName);
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        _moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        var linearBuilder = ArrayBuilder<NamedTypeSymbol>.GetInstance();

        foreach (var set in _program.nestedTypes)
            linearBuilder.AddRange(set.Value);

        _linearNestedTypes = linearBuilder.ToImmutable();
    }

    internal object Execute(bool verbose, bool logTime, string verbosePath, bool noArtifacts) {
        var timer = logTime ? Stopwatch.StartNew() : null;
        _logger = (verbose && !noArtifacts) ? new StringWriter() : null;

        var entryPoint = _program.entryPoint;

        if (entryPoint is null)
            return null;

        _programNamedType = entryPoint.containingType;
        graphicsHandlerField = typeof(Executor).GetField("GraphicsHandler", BindingFlags.Public | BindingFlags.Static);

        EmitInternal();

        if (_diagnostics.AnyErrors())
            return null;

        if (!_programNamedType.isStatic) {
            Program = Activator.CreateInstance(_programType);
            programField = typeof(Executor).GetField("Program");
        }

        if (_graphicsEnabled && _graphicsInitialized)
            GraphicsHandler = new GraphicsHandler(null, false, false);

        var mainMethod = _programType.GetMethod(
            entryPoint.name,
            _programNamedType.isStatic
                ? BindingFlags.Public | BindingFlags.Static
                : BindingFlags.Public | BindingFlags.Instance
        );

        if (_graphicsEnabled && _graphicsInitialized && _program.updatePoint is not null) {
            var updateMethod = _programType.GetMethod(
                _program.updatePoint.name,
                _programNamedType.isStatic
                    ? BindingFlags.Public | BindingFlags.Static
                    : BindingFlags.Public | BindingFlags.Instance
            );

            var updateAction = (Action<double>)Delegate.CreateDelegate(typeof(Action<double>), Program, updateMethod);
            GraphicsHandler.SetExecuteHandler(updateAction);
        }

        if (logTime) {
            timer.Stop();

            if (verbose && !noArtifacts && _program.compilation.options.enableOutput) {
                var assemblyName = $"{DynamicAssemblyName}.g.dll";
                var assemblyPath = verbosePath is null ? assemblyName : Path.Combine(verbosePath, assemblyName);
                Console.WriteLine($"Dumping dynamic executor assembly to \"{assemblyPath}\"");

                try {
                    var generator = new Lokad.ILPack.AssemblyGenerator();
                    generator.GenerateAssembly(_programType.Assembly, assemblyPath);
                } catch (Exception e) {
                    // Don't let Lokad's bugs crash our program, fallback
                    Console.WriteLine($"\tError: Failed to generate dynamic assembly! Exception caught: {e}");
                    Console.WriteLine("\tFalling back to manual IL logger");
                    var logName = $"{DynamicAssemblyName}.txt";
                    var logPath = verbosePath is null ? logName : Path.Combine(verbosePath, logName);
                    Console.WriteLine($"\tWriting IL log to \"{logPath}\"");

                    using var streamWriter = new StreamWriter(logPath);
                    streamWriter.Write(_logger);
                }
            }

            _diagnostics.Push(new BelteDiagnostic(
                DiagnosticSeverity.Debug,
                $"Emitted the program in {timer.ElapsedMilliseconds} ms"
            ));

            timer.Restart();
        }

        if (!_program.compilation.options.enableOutput)
            return null;

        object result;

        if (_graphicsEnabled && _graphicsInitialized) {
            var mainAction = (Action)Delegate.CreateDelegate(typeof(Action), Program, mainMethod);
            GraphicsHandler.SetExecuteMain(mainAction);
            GraphicsHandler.Run();
            result = null;
        } else {
            result = mainMethod.Invoke(Program, _program.entryPoint.parameterCount == 0 ? null : [_arguments]);
        }

        if (logTime) {
            timer.Stop();
            _diagnostics.Push(new BelteDiagnostic(
                DiagnosticSeverity.Debug,
                $"Executed the program in {timer.ElapsedMilliseconds} ms"
            ));
        }

        return result;
    }

    internal static Executor CreateForHandler(
        BoundProgram program,
        BelteDiagnosticQueue diagnostics,
        NamedTypeSymbol type) {
        var executor = new Executor(program, [], diagnostics) {
            _programNamedType = type,
            _logger = new StringWriter()
        };

        executor.EmitInternal();
        return executor;
    }

    internal object ExecuteMethod(MethodSymbol method, params object[] arguments) {
        var methodInv = _programType.GetMethod(method.name, BindingFlags.Public | BindingFlags.Static);
        return methodInv.Invoke(null, arguments);
    }

    internal Type GetType(TypeSymbol type, bool byRef = false) {
        var typeRef = GetTypeCore(type);

        if (byRef)
            typeRef = typeRef.MakeByRefType();

        return typeRef;

        Type GetTypeCore(TypeSymbol type) {
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

            if (type.IsEnumType() && type.originalDefinition is not PENamedTypeSymbol)
                return _enums[type.originalDefinition];

            if (type is TemplateParameterSymbol t) {
                if (t.templateParameterKind == TemplateParameterKind.Method) {
                    var containingMethodTypeParameters = _methodTypeParameters[
                        (MethodSymbol)type.containingSymbol.originalDefinition
                    ];

                    return containingMethodTypeParameters[t.ordinal];
                }

                if (_types.TryGetValue(type.containingType.originalDefinition, out var found))
                    return found.GenericTypeParameters[t.ordinal];

                var containingType = GetTypeCoreInternal(type.containingType);

                if (containingType is TypeBuilder tb)
                    return tb.GenericTypeParameters[t.ordinal];
                else
                    return containingType.GetGenericArguments()[t.ordinal];
            }

            return GetTypeWithContainingGenerics((NamedTypeSymbol)type);
        }

        Type GetTypeWithContainingGenerics(NamedTypeSymbol type) {
            var foundType = GetTypeCoreInternal(type);

            // Error types acceptable inside specific contexts like typeof
            if (type.ContainsErrorType() || type.IsEnumType())
                return foundType;

            var chain = new Stack<NamedTypeSymbol>();
            var current = type;

            while (current is not null) {
                chain.Push(current);
                current = current.containingType;
            }

            var allTypeArgs = new List<Type>();

            while (chain.Count > 0) {
                var s = chain.Pop();

                if (s.arity > 0) {
                    foreach (var arg in s.templateArguments)
                        allTypeArgs.Add(GetType(arg.type.type));
                }
            }

            if (allTypeArgs.Count > 0)
                return foundType.MakeGenericType(allTypeArgs.ToArray());

            return foundType;
        }

        Type GetTypeCoreInternal(NamedTypeSymbol type) {
            if (type.originalDefinition is PENamedTypeSymbol t)
                return ResolveType(t);

            if (_bakedTypes.TryGetValue(type.originalDefinition, out var baked))
                return baked;

            if (_types.TryGetValue(type.originalDefinition, out var found))
                return found;

            if (_topLevelTypes.Contains(type.originalDefinition))
                CreateTypeBuilderAndBases(type);

            return _types[type.originalDefinition];
        }
    }

    private Type GetFuncType(FunctionMethodSymbol signature) {
        if (signature.returnsVoid && signature.parameterCount == 0) {
            return Type.GetType($"System.Action", throwOnError: true);
        } else if (signature.returnsVoid) {
            var typeRef = Type.GetType($"System.Action`{signature.parameterCount}", throwOnError: true);
            var builder = ArrayBuilder<Type>.GetInstance();

            foreach (var p in signature.GetParameterTypes())
                builder.Add(GetType(p.type));

            return typeRef.MakeGenericType(builder.ToArrayAndFree());
        } else {
            var typeRef = Type.GetType($"System.Func`{signature.parameterCount + 1}", throwOnError: true);
            var builder = ArrayBuilder<Type>.GetInstance();

            foreach (var p in signature.GetParameterTypes())
                builder.Add(GetType(p.type));

            builder.Add(GetType(signature.returnType));

            return typeRef.MakeGenericType(builder.ToArrayAndFree());
        }
    }

    private Type GetOpenFuncType(FunctionMethodSymbol signature) {
        if (signature.returnsVoid && signature.parameterCount == 0)
            return Type.GetType($"System.Action", throwOnError: true);
        else if (signature.returnsVoid)
            return Type.GetType($"System.Action`{signature.parameterCount}", throwOnError: true);
        else
            return Type.GetType($"System.Func`{signature.parameterCount + 1}", throwOnError: true);
    }

    internal static Type ResolveType(PENamedTypeSymbol type) {
        var metadata = (type.containingAssembly as PEAssemblySymbol).@assembly;

        if (!AssemblyCache.TryGetValue(metadata.location, out var assembly)) {
            try {
                assembly = Assembly.LoadFrom(metadata.location);
            } catch (FileLoadException) {
                Console.WriteLine(metadata.location);
                throw;
            }

            AssemblyCache.Add(metadata.location, assembly);
        }

        var stack = new Stack<PENamedTypeSymbol>();
        var current = type;

        while (current is not null) {
            stack.Push(current);
            current = current.containingType as PENamedTypeSymbol;
        }

        var topType = stack.Pop();
        var displayName = topType.ToDisplayString(SymbolDisplayFormat.NetNamespaceQualifiedNameFormat);
        var currentFoundType = assembly.GetType(displayName);

        while (stack.Count > 0) {
            var nestedType = stack.Pop();
            var flags = nestedType.declaredAccessibility == Accessibility.Public
                ? BindingFlags.Public
                : BindingFlags.NonPublic;
            currentFoundType = currentFoundType.GetNestedType(nestedType.name, flags);
        }

        return currentFoundType;
    }

    internal FieldInfo GetField(FieldSymbol field) {
        FieldInfo value = null;
        var found = false;

        if (field.originalDefinition is PEFieldSymbol f) {
            var containingType = GetType(f.containingType);

            if (IsEmitType(containingType) || containingType.GenericTypeArguments.Any(IsEmitType))
                containingType = ResolveType((PENamedTypeSymbol)f.containingType);

            value = containingType.GetField(f.name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            found = true;
        }

        if (!found)
            value = _fields[field.originalDefinition];

        var constructedType = GetType(field.containingType);

        if (constructedType.IsConstructedGenericType) {
            if (IsEmitType(constructedType) || constructedType.GenericTypeArguments.Any(IsEmitType)) {
                value = TypeBuilder.GetField(constructedType, value);
            } else {
                value = constructedType
                    .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .First(f => f.MetadataToken == value.MetadataToken);
            }
        }

        return value;
    }

    internal MethodInfo GetMethod(MethodSymbol method) {
        MethodInfo value = null;
        var found = false;

        if (method.originalDefinition is PEMethodSymbol m) {
            var containingType = GetType(m.containingType);

            if (IsEmitType(containingType) || containingType.GenericTypeArguments.Any(IsEmitType))
                containingType = ResolveType((PENamedTypeSymbol)m.containingType);

            var paramTypes = m.GetParameterTypes().Select(p => GetType(p.type)).ToArray();
            value = containingType.GetMethod(m.name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance, paramTypes);
            found = true;
        }

        if (!found && method.originalDefinition is FunctionMethodSymbol s) {
            var typeRef = GetFuncType(s);

            if (typeRef.ContainsGenericParameters || typeRef is TypeBuilder || typeRef is GenericTypeParameterBuilder)
                return TypeBuilder.GetMethod(typeRef, GetOpenFuncType(s).GetMethod("Invoke"));
            else
                return typeRef.GetMethod("Invoke");
        }

        if (!found && _methods.TryGetValue(method.originalDefinition, out var val)) {
            found = true;
            value = val;
        }

        if (found) {
            var constructedType = GetType(method.containingType);

            if (constructedType.IsConstructedGenericType) {
                if (IsEmitType(constructedType) || constructedType.GenericTypeArguments.Any(IsEmitType)) {
                    value = TypeBuilder.GetMethod(constructedType, value);
                } else {
                    value = constructedType
                        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                        .First(m => m.MetadataToken == value.MetadataToken);
                }
            }

            if (method.arity > 0)
                value = value.MakeGenericMethod(method.templateArguments.Select(t => GetType(t.type.type)).ToArray());

            return value;
        }

        return CheckStandardMap(method);
    }

    private static bool IsEmitType(Type t) {
        return t.Module is System.Reflection.Emit.ModuleBuilder;
    }

    internal ConstructorInfo GetConstructor(MethodSymbol method) {
        ConstructorInfo value = null;
        var found = false;

        if (method.originalDefinition is PEMethodSymbol m) {
            var containingType = GetType(m.containingType);

            if (IsEmitType(containingType) || containingType.GenericTypeArguments.Any(IsEmitType))
                containingType = ResolveType((PENamedTypeSymbol)m.containingType);

            var flags = BindingFlags.Public | BindingFlags.NonPublic;

            if (m.isStatic)
                flags |= BindingFlags.Static;
            else
                flags |= BindingFlags.Instance;

            value = containingType.GetConstructor(flags, m.GetParameterTypes().Select(p => GetType(p.type)).ToArray());
            found = true;
        }

        if (!found && _constructors.TryGetValue(method.originalDefinition, out var val)) {
            found = true;
            value = val;
        }

        if (found) {
            var constructedType = GetType(method.containingType);

            if (constructedType.IsConstructedGenericType) {
                if (IsEmitType(constructedType) || constructedType.GenericTypeArguments.Any(IsEmitType)) {
                    value = TypeBuilder.GetConstructor(constructedType, value);
                } else {
                    value = constructedType
                        .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                        .First(m => m.MetadataToken == value.MetadataToken);
                }
            }

            return value;
        }

        return CheckConstructorsStandardMap(method);
    }

    internal override void EmitGlobalsClass() {
        randomField = typeof(Executor).GetField("Random", BindingFlags.Public | BindingFlags.Static);
    }

    internal override NamedTypeSymbol GetFixedImplementationType(SourceFixedFieldSymbol field) {
        return _program.fixedImplementationTypes[field];
    }

    internal MethodInfo GetNullAssert(TypeSymbol type) {
        var assertNull = typeof(Belte.Runtime.Utilities).GetMethod("AssertNull", BindingFlags.Public | BindingFlags.Static);
        var closedMethod = assertNull.MakeGenericMethod(GetType(type));
        return closedMethod;
    }

    internal ConstructorInfo GetFuncCtor(FunctionMethodSymbol signature) {
        var typeRef = GetFuncType(signature);

        if (typeRef.ContainsGenericParameters || typeRef is TypeBuilder || typeRef is GenericTypeParameterBuilder)
            return TypeBuilder.GetConstructor(typeRef, GetOpenFuncType(signature).GetConstructor([typeof(object), typeof(nint)]));
        else
            return typeRef.GetConstructor([typeof(object), typeof(nint)]);

    }

    internal ConstructorInfo GetNullableCtor(TypeSymbol type) {
        var generic = GetType(type);
        var nullable = typeof(Nullable<>);
        var closedType = nullable.MakeGenericType(generic);

        if (closedType.ContainsGenericParameters || generic is TypeBuilder || generic is GenericTypeParameterBuilder)
            return TypeBuilder.GetConstructor(closedType, MethodInfoCache.Nullable_ctor);
        else
            return closedType.GetConstructor([generic]);
    }

    internal MethodInfo GetNullableValue(TypeSymbol type) {
        var generic = GetType(type);
        var nullable = typeof(Nullable<>);
        var closedType = nullable.MakeGenericType(generic);

        if (closedType.ContainsGenericParameters || generic is TypeBuilder || generic is GenericTypeParameterBuilder)
            return TypeBuilder.GetMethod(closedType, MethodInfoCache.Nullable_get_Value);
        else
            return closedType.GetMethod("get_Value", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
    }

    internal MethodInfo GetNullableValueOrDefault(TypeSymbol type) {
        var generic = GetType(type);
        var nullable = typeof(Nullable<>);
        var closedType = nullable.MakeGenericType(generic);

        if (closedType.ContainsGenericParameters || generic is TypeBuilder || generic is GenericTypeParameterBuilder)
            return TypeBuilder.GetMethod(closedType, MethodInfoCache.Nullable_GetValueOrDefault);
        else
            return closedType.GetMethod("GetValueOrDefault", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
    }

    internal MethodInfo GetNullableHasValue(TypeSymbol type) {
        var generic = GetType(type);
        var nullable = typeof(Nullable<>);
        var closedType = nullable.MakeGenericType(generic);

        if (closedType.ContainsGenericParameters || generic is TypeBuilder || generic is GenericTypeParameterBuilder)
            return TypeBuilder.GetMethod(closedType, MethodInfoCache.Nullable_get_HasValue);
        else
            return closedType.GetMethod("get_HasValue", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
    }

    internal MethodInfo GetSort(TypeSymbol elementType) {
        var generic = GetType(elementType);
        var sort = typeof(Belte.Runtime.Utilities).GetMethod("Sort");
        var closedMethod = sort.MakeGenericMethod(generic);
        return closedMethod;
    }

    internal MethodInfo GetLength(TypeSymbol elementType) {
        var generic = GetType(elementType);
        var length = typeof(Belte.Runtime.Utilities).GetMethod("Length");
        var closedMethod = length.MakeGenericMethod(generic);
        return closedMethod;
    }

    internal MethodInfo GetSizeOf(TypeSymbol elementType) {
        var generic = GetType(elementType);
        var length = typeof(System.Runtime.InteropServices.Marshal).GetMethod("SizeOf", Type.EmptyTypes);
        var closedMethod = length.MakeGenericMethod(generic);
        return closedMethod;
    }

    private void CreateTypeBuilderAndBases(NamedTypeSymbol type) {
        var baseStack = new Stack<NamedTypeSymbol>();
        var current = type;

        while (current is not null) {
            if (current.specialType is SpecialType.Object or SpecialType.Exception)
                break;

            baseStack.Push(current);
            current = current.baseType;
        }

        while (baseStack.Count > 0)
            CreateTypeBuilder(baseStack.Pop());
    }

    private void EmitInternal() {
        GenerateSTLMap();
        CompleteSpecialTypes();

        foreach (var type in _topLevelTypes)
            CreateTypeBuilderAndBases(type);

        if (_program.compilation.options.concurrentBuild) {
            var maxParallels = _program.compilation.options.maxCoreCount;
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = maxParallels };

            var topLevelTypes = _topLevelTypes;
            Parallel.For(0, topLevelTypes.Length, parallelOptions, i => CreateMemberDefinitions(topLevelTypes[i]));

            var linearTypes = _linearNestedTypes;
            Parallel.For(0, linearTypes.Length, parallelOptions, i => CreateMemberDefinitions(linearTypes[i]));

            Parallel.ForEach(_methods, parallelOptions, method => {
                if (method.Value is MethodBuilder mb)
                    EmitMethod(method.Key, mb);
            });

            Parallel.ForEach(_constructors, parallelOptions, method => {
                if (method.Value is ConstructorBuilder mb)
                    EmitConstructor(mb);
            });
        } else {
            foreach (var type in _topLevelTypes)
                CreateMemberDefinitions(type);

            foreach (var type in _linearNestedTypes)
                CreateMemberDefinitions(type);

            foreach (var method in _methods) {
                if (method.Value is MethodBuilder mb)
                    EmitMethod(method.Key, mb);
            }

            foreach (var method in _constructors) {
                if (method.Value is ConstructorBuilder cb)
                    EmitConstructor(cb);
            }
        }

        BakeTypes();
    }

    private void BakeTypes() {
        // Topologically sorts struct types tracking dependencies (field types) so that when the struct is created it's
        // layout is fully known
        var deps = new Dictionary<TypeSymbol, List<NamedTypeSymbol>>();

        foreach (var type in _types.Keys) {
            if (!type.IsStructType())
                continue;

            var list = new List<NamedTypeSymbol>();

            foreach (var member in type.GetMembers()) {
                if (member is FieldSymbol f) {
                    if (f.type is NamedTypeSymbol nt &&
                        nt.IsStructType() &&
                        _types.ContainsKey(nt.originalDefinition)) {

                        list.Add(nt.originalDefinition);
                    }
                }
            }

            deps[type.originalDefinition] = list;
        }

        var result = new List<TypeSymbol>();
        var visited = new List<TypeSymbol>();

        void Visit(TypeSymbol t) {
            if (visited.Contains(t))
                return;

            if (deps.TryGetValue(t, out var children)) {
                foreach (var dep in children)
                    Visit(dep);
            }

            visited.Add(t);
            result.Add(t);
        }

        foreach (var type in _types.Keys) {
            if (type.IsStructType())
                Visit(type.originalDefinition);
        }

        foreach (var type in result) {
            var tb = _types[type];
            var baked = tb.CreateType();
            _bakedTypes[(NamedTypeSymbol)type] = baked;
        }

        foreach (var (type, tb) in _types) {
            if (!type.IsStructType()) {
                if (type.Equals(_programNamedType.originalDefinition)) {
                    _programType = tb.CreateType();
                    continue;
                }

                var baked = tb.CreateType();
                _bakedTypes[(NamedTypeSymbol)type] = baked;
            }
        }
    }

    private void CompleteSpecialTypes() {
        if (_program.compilation.options.noStdLib)
            return;

        foreach (var type in new[] { SpecialType.Rect, SpecialType.Text, SpecialType.Sprite,
                                     SpecialType.Vec2, SpecialType.Texture, SpecialType.Sound }) {
            var typeSymbol = CorLibrary.GetSpecialType(type);
            var native = _specialTypes[type];

            foreach (var member in typeSymbol.GetMembers()) {
                if (member is FieldSymbol f) {
                    _fields.Add(f, native.GetField(f.name));
                } else if (member is MethodSymbol m) {
                    if (m.methodKind == MethodKind.Constructor) {
                        _constructors.Add(
                            m,
                            native.GetConstructor(
                                m.parameters.Select(p => GetType(p.type, p.refKind != RefKind.None)).ToArray()
                            )
                        );
                    } else {
                        _methods.Add(m, native.GetMethod(
                            m.name,
                            BindingFlags.Public | BindingFlags.Instance,
                            m.parameters.Select(p => GetType(p.type, p.refKind != RefKind.None)).ToArray()
                        ));
                    }
                }
            }
        }
    }

    private void CreateTypeBuilder(NamedTypeSymbol type) {
        if (type.IsEnumType()) {
            if (_workingEnums.ContainsKey(type.originalDefinition))
                return;

            var underlyingType = GetType(type.enumUnderlyingType);
            var enumBuilder = _moduleBuilder.DefineEnum(
                GetTypeName(type, false),
                GetTypeAttributes(type, false) & TypeAttributes.VisibilityMask,
                underlyingType
            );

            if (type.enumFlagsAttribute) {
                var flagsCtor = typeof(FlagsAttribute).GetConstructor(Type.EmptyTypes);
                var flagsAttr = new CustomAttributeBuilder(flagsCtor, []);
                enumBuilder.SetCustomAttribute(flagsAttr);
            }

            _workingEnums.Add(type, enumBuilder);

            CreateEnumMemberDefinitions(type);
            return;
        }

        if (_types.ContainsKey(type.originalDefinition))
            return;

        var typeBuilder = _moduleBuilder.DefineType(
            GetTypeName(type, false),
            GetTypeAttributes(type, false),
            GetBaseType(type)
        );

        _types.Add(type.originalDefinition, typeBuilder);

        string[] workingParams = [];

        if (type.arity > 0) {
            workingParams = type.templateParameters.Select(t => t.name).ToArray();
            typeBuilder.DefineGenericParameters(workingParams);
        }

        CreateNestedTypes(type, typeBuilder, workingParams);
    }

    private Type GetBaseType(NamedTypeSymbol type) {
        if (type.baseType is null || type.IsStructType())
            return typeof(ValueType);

        if (type.IsEnumType())
            return typeof(Enum);

        return GetType(type.baseType);
    }

    private void CreateNestedTypes(NamedTypeSymbol type, TypeBuilder typeBuilder, string[] workingParams) {
        foreach (var member in type.GetTypeMembers())
            AddNestedType(member, workingParams);

        if (_program.nestedTypes.ContainsKey(type)) {
            foreach (var nestedType in _program.nestedTypes[type])
                AddNestedType(nestedType, workingParams);
        }

        void AddNestedType(NamedTypeSymbol nestedType, string[] workingParams) {
            var nestedBuilder = typeBuilder.DefineNestedType(
                GetTypeName(nestedType, true),
                GetTypeAttributes(nestedType, true),
                GetBaseType(nestedType)
            );

            workingParams = workingParams.Concat(nestedType.templateParameters.Select(t => t.name)).ToArray();

            if (workingParams.Length > 0)
                nestedBuilder.DefineGenericParameters(workingParams);

            CreateNestedTypes(nestedType, nestedBuilder, workingParams);
            _types.Add(nestedType.originalDefinition, nestedBuilder);
        }
    }

    private string GetTypeName(NamedTypeSymbol type, bool isNested) {
        if (type.IsFromCompilation(_program.compilation)) {
            if (isNested || (type.containingNamespace?.isGlobalNamespace ?? true))
                return type.name;

            return $"{type.containingNamespace.name}.{type.name}";
        }

        return $"Belte.{type.name}";
    }

    private TypeAttributes GetTypeAttributes(NamedTypeSymbol type, bool isNested) {
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
            Accessibility.Private when isNested => TypeAttributes.NestedPrivate,
            Accessibility.Public when isNested => TypeAttributes.NestedPublic,
            Accessibility.Public => TypeAttributes.Public,
            Accessibility.Protected => TypeAttributes.NestedFamily,
            Accessibility.NotApplicable => 0,
            _ => throw ExceptionUtilities.UnexpectedValue(type.declaredAccessibility)
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
        var attributes = MethodAttributes.Public | MethodAttributes.HideBySig;

        if (method.isStatic)
            attributes |= MethodAttributes.Static;
        if (method.isAbstract)
            attributes |= MethodAttributes.Abstract;
        if (method.IsMetadataVirtual())
            attributes |= MethodAttributes.Virtual;
        if (method.isOverride)
            attributes |= MethodAttributes.ReuseSlot;
        if (method.isMetadataFinal)
            attributes |= MethodAttributes.Final;

        return attributes;
    }

    private void CreateEnumMemberDefinitions(NamedTypeSymbol type) {
        var enumBuilder = _workingEnums[type.originalDefinition];

        foreach (var member in type.GetMembers()) {
            if (member is not FieldSymbol f)
                continue;

            enumBuilder.DefineLiteral(f.name, f.constantValue);
        }

        var finalEnum = enumBuilder.CreateType();
        _enums.Add(type, finalEnum);

        foreach (var member in type.GetMembers()) {
            if (member is not FieldSymbol f)
                continue;

            _fields.Add(f, finalEnum.GetField(f.name));
        }
    }

    private void CreateMemberDefinitions(NamedTypeSymbol type) {
        if (type.IsEnumType())
            return;

        var typeBuilder = _types[type.originalDefinition];

        foreach (var member in type.GetMembers()) {
            if (member is FieldSymbol f) {
                if (f.isFixedSizeBuffer) {
                    CreateFixedSizeBufferField(f as SourceFixedFieldSymbol, typeBuilder);
                    continue;
                }

                var fieldType = (f.type.typeKind == TypeKind.FunctionPointer)
                    ? typeof(IntPtr)
                    : GetType(f.type, f.refKind != RefKind.None);

                var fieldBuilder = typeBuilder.DefineField(
                    f.name,
                    fieldType,
                    GetFieldAttributes(f)
                );

                _fields.Add(f, fieldBuilder);
            } else if (member is NamedTypeSymbol t) {
                CreateMemberDefinitions(t);
            } else if (member is MethodSymbol m && m.isAbstract) {
                CreateMethodDefinition(m, null, typeBuilder);
            }
        }

        foreach (var pair in _program.GetAllMethodBodies()) {
            if (pair.Item1.containingType.originalDefinition.Equals(type))
                CreateMethodDefinition(pair.Item1, pair.Item2, typeBuilder);
        }
    }

    private void CreateFixedSizeBufferField(SourceFixedFieldSymbol field, TypeBuilder typeBuilder) {
        var fixedImpl = GetFixedImplementationType(field);

        var elementType = ((PointerTypeSymbol)field.type).pointedAtType;
        var elementSize = elementType.FixedBufferElementSizeInBytes();

        var nestedBuilder = typeBuilder.DefineNestedType(
            fixedImpl.name,
            GetTypeAttributes(fixedImpl, true),
            GetBaseType(fixedImpl),
            PackingSize.Unspecified,
            field.fixedSize * elementSize
        );

        var nestedBufferField = fixedImpl.fixedElementField;
        var nestedBufferFieldBuilder = nestedBuilder.DefineField(
            nestedBufferField.name,
            GetType(nestedBufferField.type),
            GetFieldAttributes(nestedBufferField)
        );

        var adaptedFieldBuilder = typeBuilder.DefineField(
            field.name,
            nestedBuilder,
            GetFieldAttributes(field)
        );

        _fields.Add(field, adaptedFieldBuilder);
        _fields.Add(nestedBufferField, nestedBufferFieldBuilder);

        lock (_types)
            _types.Add(fixedImpl, nestedBuilder);

        nestedBuilder.CreateType();
    }

    private void CreateMethodDefinition(MethodSymbol method, BoundBlockStatement body, TypeBuilder typeBuilder) {
        if (method.methodKind is MethodKind.Constructor or MethodKind.StaticConstructor)
            CreateConstructorDefinition(method, body, typeBuilder);
        else if (method.isExtern)
            CreatePInvokeMethodDefinition(method, typeBuilder);
        else
            CreateNormalMethodDefinition(method, body, typeBuilder);
    }

    private void CreateConstructorDefinition(MethodSymbol method, BoundBlockStatement body, TypeBuilder typeBuilder) {
        var constructorBuilder = typeBuilder.DefineConstructor(
            GetMethodAttributes(method),
            CallingConventions.Standard,
            method.parameters.Select(p => GetType(p.type, p.refKind != RefKind.None)).ToArray()
        );

        _constructors.Add(method, constructorBuilder);
        _constructorBodies.Add(constructorBuilder, (method, body));
    }

    private void CreatePInvokeMethodDefinition(MethodSymbol method, TypeBuilder typeBuilder) {
        var dllImportData = method.GetDllImportData();
        var methodBuilder = typeBuilder.DefinePInvokeMethod(
            method.name,
            dllImportData.moduleName,
            GetMethodAttributes(method),
            CallingConventions.Standard,
            GetType(method.returnType, method.returnsByRef),
            method.parameters.Select(p => GetType(p.type, p.refKind != RefKind.None)).ToArray(),
            GetCallingConvention(dllImportData.callingConvention),
            dllImportData.characterSet
        );

        methodBuilder.SetImplementationFlags(
            System.Reflection.MethodImplAttributes.PreserveSig
        );

        _methods.Add(method, methodBuilder);
    }

    private System.Runtime.InteropServices.CallingConvention GetCallingConvention(CallingConvention callingConvention) {
        return callingConvention switch {
            CallingConvention.Winapi => System.Runtime.InteropServices.CallingConvention.Winapi,
            CallingConvention.FastCall => System.Runtime.InteropServices.CallingConvention.FastCall,
            CallingConvention.Cdecl => System.Runtime.InteropServices.CallingConvention.Cdecl,
            CallingConvention.StdCall => System.Runtime.InteropServices.CallingConvention.StdCall,
            CallingConvention.ThisCall => System.Runtime.InteropServices.CallingConvention.ThisCall,
            _ => throw ExceptionUtilities.UnexpectedValue(callingConvention)
        };
    }

    private void CreateNormalMethodDefinition(MethodSymbol method, BoundBlockStatement body, TypeBuilder typeBuilder) {
        var methodBuilder = typeBuilder.DefineMethod(
            method.name,
            GetMethodAttributes(method)
        );

        if (method.arity > 0) {
            var typeParameters = methodBuilder.DefineGenericParameters(
                method.templateParameters.Select(t => t.name).ToArray()
            );

            _methodTypeParameters.Add(method, typeParameters);
        }

        methodBuilder.SetReturnType(GetTypeOrIntPtr(method.returnType, method.returnsByRef));
        methodBuilder.SetParameters(
            method.parameters.Select(p => GetTypeOrIntPtr(p.type, p.refKind != RefKind.None)).ToArray()
        );

        _methods.Add(method, methodBuilder);

        if (body is not null)
            _methodBodies.Add(method, body);

        Type GetTypeOrIntPtr(TypeSymbol type, bool byRef) {
            if (type.typeKind == TypeKind.FunctionPointer)
                return typeof(IntPtr);

            return GetType(type, byRef);
        }
    }

    private void EmitMethod(MethodSymbol method, MethodBuilder methodBuilder) {
        if (_logger is not null) lock (_logger) _logger.WriteLine($"Emitting method {method}");

        if (method.isAbstract || method.isExtern)
            return;

        var body = _methodBodies[method];
        var ilBuilder = new RefILBuilder(method, this, methodBuilder.GetILGenerator(), _logger);
        var codeGen = new CodeGenerator(this, method, body, ilBuilder, false);
        codeGen.Generate();
    }

    private void EmitConstructor(ConstructorBuilder constructorBuilder) {
        var (constructor, body) = _constructorBodies[constructorBuilder];

        if (_logger is not null) lock (_logger) _logger.WriteLine($"Emitting constructor {constructor}");

        var ilBuilder = new RefILBuilder(constructor, this, constructorBuilder.GetILGenerator(), _logger);
        var codeGen = new CodeGenerator(this, constructor, body, ilBuilder, false);
        codeGen.Generate();
    }

    private ConstructorInfo CheckConstructorsStandardMap(MethodSymbol method) {
        var mapKey = LibraryHelpers.BuildMapKey(method);

        return mapKey switch {
            "Object<>_.ctor" => MethodInfoCache.Object_ctor,
            "Exception<>_.ctor" => MethodInfoCache.Exception_ctor,
            "Exception<>_.ctor_S?" => MethodInfoCache.Exception_ctor_S,
            "Nullable<>_.ctor" => GetNullableCtor(method.containingType.templateArguments[0].type.type),
            _ => throw ExceptionUtilities.UnexpectedValue(mapKey),
        };
    }

    private MethodInfo CheckStandardMap(MethodSymbol method) {
        var mapKey = LibraryHelpers.BuildMapKey(method);

        if (!_program.compilation.options.noStdLib && !_reportedGraphicsCall) {
            if ((object)method.containingType == GraphicsLibrary.Graphics.underlyingNamedType &&
                _program.compilation.options.outputKind != OutputKind.GraphicsApplication) {
                lock (_diagnostics) {
                    if (!_reportedGraphicsCall) {
                        _diagnostics.Push(Error.Unsupported.GraphicsCall());
                        _reportedGraphicsCall = true;
                    }
                }
            }
        }

        switch (mapKey) {
            case "Nullable<>_get_Value":
                return GetNullableValue(method.containingType.templateArguments[0].type.type);
            case "Nullable<>_get_HasValue":
                return GetNullableHasValue(method.containingType.templateArguments[0].type.type);
            case "Nullable<>_GetValueOrDefault":
                return GetNullableValueOrDefault(method.containingType.templateArguments[0].type.type);
            case "Graphics_Initialize_SIIB":
                _graphicsInitialized = true;
                goto default;
            default:
                return _stlMap[mapKey];
        }
    }

    #region Libraries

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

    public static long? DrawSprite(BSprite sprite, BVec2 offset) {
        var dstX = (int)sprite.dst.x;
        var dstY = (int)sprite.dst.y;

        if (offset is not null) {
            dstX -= (int)offset.x;
            dstY -= (int)offset.y;
        }

        GraphicsHandler.DrawSprite(
            sprite.texture.mTexture,
            (int)sprite.src.x,
            (int)sprite.src.y,
            (int)sprite.src.w,
            (int)sprite.src.h,
            dstX,
            dstY,
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

    public static long? DrawRect(BRect rect, long? r, long? g, long? b) {
        GraphicsHandler.DrawRect((int)rect.x, (int)rect.y, (int)rect.w, (int)rect.h, r, g, b, null);
        return 0;
    }

    public static long? DrawRect(BRect rect, long? r, long? g, long? b, long? a) {
        GraphicsHandler.DrawRect((int)rect.x, (int)rect.y, (int)rect.w, (int)rect.h, r, g, b, a);
        return 0;
    }

    public static long? Draw(
        BTexture texture,
        BRect srcRect,
        BRect dstRect,
        long? rotation,
        bool? flip,
        double? alpha) {
        Microsoft.Xna.Framework.Rectangle? src = srcRect is null
            ? null
            : new Microsoft.Xna.Framework.Rectangle((int)srcRect.x, (int)srcRect.y, (int)srcRect.w, (int)srcRect.h);

        var dst = new Microsoft.Xna.Framework.Rectangle((int)dstRect.x, (int)dstRect.y, (int)dstRect.w, (int)dstRect.h);

        GraphicsHandler.Draw(
            texture.mTexture,
            src,
            dst,
            rotation,
            flip,
            alpha
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

    public static BTexture LoadTexture(string path) {
        var mTexture = GraphicsHandler.LoadTexture(path, false, 255, 255, 255);
        return new BTexture(mTexture);
    }

    public static BTexture LoadTexture(string path, long r, long g, long b) {
        var mTexture = GraphicsHandler.LoadTexture(path, true, r, g, b);
        return new BTexture(mTexture);
    }

    public static bool GetMouseButton(string button) {
        return GraphicsHandler.GetMouseButton(button);
    }

    public static BVec2 GetMousePosition() {
        var (x, y) = GraphicsHandler.GetMousePosition();
        return new BVec2(x, y);
    }

    public static long GetScroll() {
        return GraphicsHandler.GetScroll();
    }

    public static BSound LoadSound(string path) {
        var mSound = GraphicsHandler.LoadSound(path);
        return new BSound(null, null, mSound);
    }

    public static void PlaySound(BSound sound) {
        GraphicsHandler.PlaySound(sound.mSound, sound.volume, sound.loop);
    }

    public static void SetCursorVisibility(bool visible) {
        GraphicsHandler.SetCursorVisibility(visible);
    }

    public static void LockFramerate(long fps) {
        GraphicsHandler.LockFramerate((int)fps);
    }

    private void GenerateSTLMap() {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.Static;
        const BindingFlags InstFlags = BindingFlags.Public | BindingFlags.Instance;

        _stlMap = new Dictionary<string, MethodInfo>() {
            { "Console_Clear", typeof(Console).GetMethod("Clear", Flags, Type.EmptyTypes) },
            { "Console_GetWidth", typeof(Belte.Runtime.Console).GetMethod("GetWidth", Flags, Type.EmptyTypes) },
            { "Console_GetHeight", typeof(Belte.Runtime.Console).GetMethod("GetHeight", Flags, Type.EmptyTypes) },
            { "Console_Print_S?", typeof(Console).GetMethod("Write", Flags, [typeof(string)]) },
            { "Console_Print_A?", typeof(Console).GetMethod("Write", Flags, [typeof(object)]) },
            { "Console_Print_O?", typeof(Console).GetMethod("Write", Flags, [typeof(object)]) },
            { "Console_Print_[?", typeof(Console).GetMethod("Write", Flags, [typeof(char[])]) },
            { "Console_PrintLine", typeof(Console).GetMethod("WriteLine", Flags, Type.EmptyTypes) },
            { "Console_PrintLine_S?", typeof(Console).GetMethod("WriteLine", Flags, [typeof(string)]) },
            { "Console_PrintLine_A?", typeof(Console).GetMethod("WriteLine", Flags, [typeof(object)]) },
            { "Console_PrintLine_O?", typeof(Console).GetMethod("WriteLine", Flags, [typeof(object)]) },
            { "Console_PrintLine_[?", typeof(Console).GetMethod("WriteLine", Flags, [typeof(char[])]) },
            { "Console_Input", typeof(Console).GetMethod("ReadLine", Flags, Type.EmptyTypes) },
            { "Console_ResetColor", typeof(Console).GetMethod("ResetColor", Flags, Type.EmptyTypes) },
            { "Console_SetForegroundColor_I", typeof(Belte.Runtime.Console).GetMethod("SetForegroundColor", Flags, [typeof(long)]) },
            { "Console_SetBackgroundColor_I", typeof(Belte.Runtime.Console).GetMethod("SetBackgroundColor", Flags, [typeof(long)]) },
            { "Console_SetCursorPosition_I?I?", typeof(Belte.Runtime.Console).GetMethod("SetCursorPosition", Flags, [typeof(long?), typeof(long?)]) },
            { "Console_SetCursorVisibility_B", typeof(Belte.Runtime.Console).GetMethod("SetCursorVisibility", Flags, [typeof(bool)]) },
            { "Directory_Create_S", typeof(Belte.Runtime.Utilities).GetMethod("CreateDirectory", Flags, [typeof(string)]) },
            { "Directory_Delete_S", typeof(Belte.Runtime.Utilities).GetMethod("DeleteDirectory", Flags, [typeof(string)]) },
            { "Directory_Exists_S", typeof(Directory).GetMethod("Exists", Flags, [typeof(string)]) },
            { "File_AppendText_SS", typeof(File).GetMethod("AppendAllText", Flags, [typeof(string), typeof(string)]) },
            { "File_Create_S", typeof(File).GetMethod("Create", Flags, [typeof(string)]) },
            { "File_Copy_SS", typeof(File).GetMethod("Copy", Flags, [typeof(string), typeof(string)]) },
            { "File_Delete_S", typeof(File).GetMethod("Delete", Flags, [typeof(string)]) },
            { "File_Exists_S", typeof(File).GetMethod("Exists", Flags, [typeof(string)]) },
            { "File_ReadText_S", typeof(File).GetMethod("ReadAllText", Flags, [typeof(string)]) },
            { "File_WriteText_SS", typeof(File).GetMethod("WriteAllText", Flags, [typeof(string), typeof(string)]) },
            { "Math_Clamp_D?D?D?", typeof(Belte.Runtime.Math).GetMethod("Clamp", Flags, [typeof(double?), typeof(double?), typeof(double?)]) },
            { "Math_Clamp_DDD", typeof(Math).GetMethod("Clamp", Flags, [typeof(double), typeof(double), typeof(double)]) },
            { "Math_Clamp_I?I?I?", typeof(Belte.Runtime.Math).GetMethod("Clamp", Flags, [typeof(long?), typeof(long?), typeof(long?)]) },
            { "Math_Clamp_III", typeof(Math).GetMethod("Clamp", Flags, [typeof(long), typeof(long), typeof(long)]) },
            { "Math_Lerp_D?D?D?", typeof(Belte.Runtime.Math).GetMethod("Lerp", Flags, [typeof(double?), typeof(double?), typeof(double?)]) },
            { "Math_Lerp_DDD", typeof(Belte.Runtime.Math).GetMethod("Lerp", Flags, [typeof(double), typeof(double), typeof(double)]) },
            { "Math_Cos_D", typeof(Math).GetMethod("Cos", Flags, [typeof(double)]) },
            { "Math_Cos_D?", typeof(Belte.Runtime.Math).GetMethod("Cos", Flags, [typeof(double?)]) },
            { "Math_Cosh_D", typeof(Math).GetMethod("Cosh", Flags, [typeof(double)]) },
            { "Math_Cosh_D?", typeof(Belte.Runtime.Math).GetMethod("Cosh", Flags, [typeof(double?)]) },
            { "Math_Acos_D", typeof(Math).GetMethod("Acos", Flags, [typeof(double)]) },
            { "Math_Acos_D?", typeof(Belte.Runtime.Math).GetMethod("Acos", Flags, [typeof(double?)]) },
            { "Math_Acosh_D", typeof(Math).GetMethod("Acosh", Flags, [typeof(double)]) },
            { "Math_Acosh_D?", typeof(Belte.Runtime.Math).GetMethod("Acosh", Flags, [typeof(double?)]) },
            { "Math_Sin_D", typeof(Math).GetMethod("Sin", Flags, [typeof(double)]) },
            { "Math_Sin_D?", typeof(Belte.Runtime.Math).GetMethod("Sin", Flags, [typeof(double?)]) },
            { "Math_Sinh_D", typeof(Math).GetMethod("Sinh", Flags, [typeof(double)]) },
            { "Math_Sinh_D?", typeof(Belte.Runtime.Math).GetMethod("Sinh", Flags, [typeof(double?)]) },
            { "Math_Asin_D", typeof(Math).GetMethod("Asin", Flags, [typeof(double)]) },
            { "Math_Asin_D?", typeof(Belte.Runtime.Math).GetMethod("Asin", Flags, [typeof(double?)]) },
            { "Math_Asinh_D", typeof(Math).GetMethod("Asinh", Flags, [typeof(double)]) },
            { "Math_Asinh_D?", typeof(Belte.Runtime.Math).GetMethod("Asinh", Flags, [typeof(double?)]) },
            { "Math_Tan_D", typeof(Math).GetMethod("Tan", Flags, [typeof(double)]) },
            { "Math_Tan_D?", typeof(Belte.Runtime.Math).GetMethod("Tan", Flags, [typeof(double?)]) },
            { "Math_Tanh_D", typeof(Math).GetMethod("Tanh", Flags, [typeof(double)]) },
            { "Math_Tanh_D?", typeof(Belte.Runtime.Math).GetMethod("Tanh", Flags, [typeof(double?)]) },
            { "Math_Atan_D", typeof(Math).GetMethod("Atan", Flags, [typeof(double)]) },
            { "Math_Atan_D?", typeof(Belte.Runtime.Math).GetMethod("Atan", Flags, [typeof(double?)]) },
            { "Math_Atanh_D", typeof(Math).GetMethod("Atanh", Flags, [typeof(double)]) },
            { "Math_Atanh_D?", typeof(Belte.Runtime.Math).GetMethod("Atanh", Flags, [typeof(double?)]) },
            { "Math_Pow_DD", typeof(Math).GetMethod("Pow", Flags, [typeof(double), typeof(double)]) },
            { "Math_Pow_D?D?", typeof(Belte.Runtime.Math).GetMethod("Pow", Flags, [typeof(double?), typeof(double?)]) },
            { "Math_Pow_II", typeof(Belte.Runtime.Math).GetMethod("Pow", Flags, [typeof(long), typeof(long)]) },
            { "Math_Pow_I?I?", typeof(Belte.Runtime.Math).GetMethod("Pow", Flags, [typeof(long?), typeof(long?)]) },
            { "Math_Max_D?D?", typeof(Belte.Runtime.Math).GetMethod("Max", Flags, [typeof(double?), typeof(double?)]) },
            { "Math_Max_DD", typeof(Math).GetMethod("Max", Flags, [typeof(double), typeof(double)]) },
            { "Math_Max_I?I?", typeof(Belte.Runtime.Math).GetMethod("Max", Flags, [typeof(long?), typeof(long?)]) },
            { "Math_Max_II", typeof(Math).GetMethod("Max", Flags, [typeof(long), typeof(long)]) },
            { "Math_Min_D?D?", typeof(Belte.Runtime.Math).GetMethod("Min", Flags, [typeof(double?), typeof(double?)]) },
            { "Math_Min_DD", typeof(Math).GetMethod("Min", Flags, [typeof(double), typeof(double)]) },
            { "Math_Min_I?I?", typeof(Belte.Runtime.Math).GetMethod("Min", Flags, [typeof(long?), typeof(long?)]) },
            { "Math_Min_II", typeof(Math).GetMethod("Min", Flags, [typeof(long), typeof(long)]) },
            { "Math_Abs_D?", typeof(Belte.Runtime.Math).GetMethod("Abs", Flags, [typeof(double?)]) },
            { "Math_Abs_D", typeof(Math).GetMethod("Abs", Flags, [typeof(double)]) },
            { "Math_Abs_I?", typeof(Belte.Runtime.Math).GetMethod("Abs", Flags, [typeof(long?)]) },
            { "Math_Abs_I", typeof(Math).GetMethod("Abs", Flags, [typeof(long)]) },
            { "Math_Round_D?", typeof(Belte.Runtime.Math).GetMethod("Round", Flags, [typeof(double?)]) },
            { "Math_Round_D", typeof(Math).GetMethod("Round", Flags, [typeof(double)]) },
            { "Math_Floor_D?", typeof(Belte.Runtime.Math).GetMethod("Floor", Flags, [typeof(double?)]) },
            { "Math_Floor_D", typeof(Math).GetMethod("Floor", Flags, [typeof(double)]) },
            { "Math_Ceiling_D?", typeof(Belte.Runtime.Math).GetMethod("Ceiling", Flags, [typeof(double?)]) },
            { "Math_Ceiling_D", typeof(Math).GetMethod("Ceiling", Flags, [typeof(double)]) },
            { "Math_Sign_D?", typeof(Belte.Runtime.Math).GetMethod("Sign", Flags, [typeof(double?)]) },
            { "Math_Sign_D", typeof(Math).GetMethod("Sign", Flags, [typeof(double)]) },
            { "Math_Sign_I?", typeof(Belte.Runtime.Math).GetMethod("Sign", Flags, [typeof(long?)]) },
            { "Math_Sign_I", typeof(Math).GetMethod("Sign", Flags, [typeof(long)]) },
            { "Math_Exp_D?", typeof(Belte.Runtime.Math).GetMethod("Exp", Flags, [typeof(double?)]) },
            { "Math_Exp_D", typeof(Math).GetMethod("Exp", Flags, [typeof(double)]) },
            { "Math_Log_D?D?", typeof(Belte.Runtime.Math).GetMethod("Log", Flags, [typeof(double?), typeof(double?)]) },
            { "Math_Log_DD", typeof(Math).GetMethod("Log", Flags, [typeof(double), typeof(double)]) },
            { "Math_Log_D?", typeof(Belte.Runtime.Math).GetMethod("Log", Flags, [typeof(double?)]) },
            { "Math_Log_D", typeof(Math).GetMethod("Log", Flags, [typeof(double)]) },
            { "Math_Sqrt_D?", typeof(Belte.Runtime.Math).GetMethod("Sqrt", Flags, [typeof(double?)]) },
            { "Math_Sqrt_D", typeof(Math).GetMethod("Sqrt", Flags, [typeof(double)]) },
            { "Math_Truncate_D?", typeof(Belte.Runtime.Math).GetMethod("Truncate", Flags, [typeof(double?)]) },
            { "Math_Truncate_D", typeof(Math).GetMethod("Truncate", Flags, [typeof(double)]) },
            { "Math_DegToRad_D?", typeof(Belte.Runtime.Math).GetMethod("DegToRad", Flags, [typeof(double?)]) },
            { "Math_DegToRad_D", typeof(double).GetMethod("DegreesToRadians", Flags, [typeof(double)]) },
            { "Math_RadToDeg_D?", typeof(Belte.Runtime.Math).GetMethod("RadToDeg", Flags, [typeof(double?)]) },
            { "Math_RadToDeg_D", typeof(double).GetMethod("RadiansToDegrees", Flags, [typeof(double)]) },
            { "LowLevel_GetHashCode_O", typeof(Belte.Runtime.Utilities).GetMethod("GetHashCode", Flags, [typeof(object)]) },
            { "LowLevel_GetTypeName_O", typeof(Belte.Runtime.Utilities).GetMethod("GetTypeName", Flags, [typeof(object)]) },
            { "LowLevel_GetType_A", typeof(Belte.Runtime.Utilities).GetMethod("AnyGetType", Flags, [typeof(object)]) },
            { "LowLevel_ThrowNullConditionException", typeof(Belte.Runtime.ThrowHelper).GetMethod("ThrowNullConditionException", Flags, Type.EmptyTypes) },
            { "LowLevel_CreateLPCSTR_S", typeof(Belte.Runtime.Utilities).GetMethod("CreateLPCSTR", Flags, [typeof(string)]) },
            { "LowLevel_CreateLPCWSTR_S", typeof(Belte.Runtime.Utilities).GetMethod("CreateLPCWSTR", Flags, [typeof(string)]) },
            { "LowLevel_FreeLPCSTR_U*", typeof(Belte.Runtime.Utilities).GetMethod("FreeLPCSTR", Flags, [typeof(byte*)]) },
            { "LowLevel_FreeLPCWSTR_C*", typeof(Belte.Runtime.Utilities).GetMethod("FreeLPCWSTR", Flags, [typeof(char*)]) },
            { "LowLevel_ReadLPCSTR_U*", typeof(Belte.Runtime.Utilities).GetMethod("ReadLPCSTR", Flags, [typeof(byte*)]) },
            { "LowLevel_ReadLPCWSTR_C*", typeof(Belte.Runtime.Utilities).GetMethod("ReadLPCWSTR", Flags, [typeof(char*)]) },
            { "LowLevel_GetGCPtr_O", typeof(Belte.Runtime.Utilities).GetMethod("GetGCPtr", Flags, [typeof(object)]) },
            { "LowLevel_FreeGCHandle_V*", typeof(Belte.Runtime.Utilities).GetMethod("FreeGCHandle", Flags, [typeof(void*)]) },
            { "LowLevel_GetObject_V*", typeof(Belte.Runtime.Utilities).GetMethod("GetObject", Flags, [typeof(void*)]) },
            { "Time_Now", typeof(Belte.Runtime.Utilities).GetMethod("TimeNow", Flags, Type.EmptyTypes) },
            { "Time_Sleep_I", typeof(Belte.Runtime.Utilities).GetMethod("TimeSleep", Flags, [typeof(long)]) },
            { "String_Ascii_S", typeof(Belte.Runtime.Utilities).GetMethod("Ascii", Flags, [typeof(string)]) },
            { "String_Char_I", typeof(Belte.Runtime.Utilities).GetMethod("Char", Flags, [typeof(long)]) },
            { "String_Split_SS", typeof(Belte.Runtime.Utilities).GetMethod("Split", Flags, [typeof(string), typeof(string)]) },
            { "String_Length_S", typeof(Belte.Runtime.Utilities).GetMethod("StringLength", Flags, [typeof(string)]) },
            { "String_IsNullOrWhiteSpace_S?", typeof(string).GetMethod("IsNullOrWhiteSpace", Flags, [typeof(string)]) },
            { "String_IsNullOrWhiteSpace_C?", typeof(Belte.Runtime.Utilities).GetMethod("IsNullOrWhiteSpace", Flags, [typeof(char?)]) },
            { "String_IsDigit_C?", typeof(Belte.Runtime.Utilities).GetMethod("IsDigit", Flags, [typeof(char?)]) },
            { "String_Substring_S?I?I?", typeof(Belte.Runtime.Utilities).GetMethod("Substring", Flags, [typeof(string), typeof(long?), typeof(long?)]) },
            { "Int_Parse_S?", typeof(Belte.Runtime.Utilities).GetMethod("IntParse", Flags, [typeof(string)]) },
            { "Object<>_ToString", typeof(object).GetMethod("ToString", InstFlags, Type.EmptyTypes) },
            { "Object<>_Equals_O?", typeof(object).GetMethod("Equals", InstFlags, [typeof(object)]) },
            { "Object<>_GetHashCode", typeof(object).GetMethod("GetHashCode", InstFlags, Type.EmptyTypes) },
            { "Graphics_Initialize_SIIB", typeof(Executor).GetMethod("InitializeGraphics", Flags, [typeof(string), typeof(long), typeof(long), typeof(bool)]) },
            { "Graphics_Fill_III", typeof(Executor).GetMethod("Fill", Flags, [typeof(long), typeof(long), typeof(long)]) },
            { "Graphics_GetKey_S", typeof(Executor).GetMethod("GetKey", Flags, [typeof(string)]) },
            { "Graphics_DrawSprite_S?", typeof(Executor).GetMethod("DrawSprite", Flags, [typeof(BSprite)]) },
            { "Graphics_DrawText_T?", typeof(Executor).GetMethod("DrawText", Flags, [typeof(BText)]) },
            { "Graphics_DrawRect_R?I?I?I?", typeof(Executor).GetMethod("DrawRect", Flags, [typeof(BRect), typeof(long?), typeof(long?), typeof(long?)]) },
            { "Graphics_LoadSprite_SV?V?I?", typeof(Executor).GetMethod("LoadSprite", Flags, [typeof(string), typeof(BVec2), typeof(BVec2), typeof(long?)]) },
            { "Graphics_LoadText_S?SV?DD?I?I?I?", typeof(Executor).GetMethod("LoadText", Flags, [typeof(string), typeof(string), typeof(BVec2), typeof(double), typeof(double?), typeof(long?), typeof(long?), typeof(long?)]) },
            { "Graphics_LockFramerate_I", typeof(Executor).GetMethod("LockFramerate", Flags, [typeof(long)]) },
            { "Graphics_DrawSprite_S?V?", typeof(Executor).GetMethod("DrawSprite", Flags, [typeof(BSprite), typeof(BVec2)]) },
            { "Graphics_LoadTexture_S", typeof(Executor).GetMethod("LoadTexture", Flags, [typeof(string)]) },
            { "Graphics_LoadTexture_SIII", typeof(Executor).GetMethod("LoadTexture", Flags, [typeof(string), typeof(long), typeof(long), typeof(long)]) },
            { "Graphics_Draw_T?R?R?I?B?D?", typeof(Executor).GetMethod("Draw", Flags, [typeof(BTexture), typeof(BRect), typeof(BRect), typeof(long?), typeof(bool?), typeof(double?)]) },
            { "Graphics_DrawRect_R?I?I?I?I?", typeof(Executor).GetMethod("DrawRect", Flags, [typeof(BRect), typeof(long?), typeof(long?), typeof(long?), typeof(long?)]) },
            { "Graphics_GetMouseButton_S", typeof(Executor).GetMethod("GetMouseButton", Flags, [typeof(string)]) },
            { "Graphics_GetMousePosition", typeof(Executor).GetMethod("GetMousePosition", Flags, Type.EmptyTypes) },
            { "Graphics_GetScroll", typeof(Executor).GetMethod("GetScroll", Flags, Type.EmptyTypes) },
            { "Graphics_LoadSound_S", typeof(Executor).GetMethod("LoadSound", Flags, [typeof(string)]) },
            { "Graphics_PlaySound_S", typeof(Executor).GetMethod("PlaySound", Flags, [typeof(BSound)]) },
            { "Graphics_SetCursorVisibility_B", typeof(Executor).GetMethod("SetCursorVisibility", Flags, [typeof(bool)]) },
            { "Vec2_Copy", typeof(BVec2).GetMethod("Copy", InstFlags, Type.EmptyTypes) },
        };
    }

    #endregion
}

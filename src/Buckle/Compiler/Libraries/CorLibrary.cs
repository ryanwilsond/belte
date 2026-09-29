using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

namespace Buckle.Libraries;

internal sealed class CorLibrary {
    // TODO We don't want to have static singletons, this is a temporary solution as we migrate to instance access
    internal static CorLibrary Instance;

#if DEBUG
    private static int InstantiationCount = 0;
#endif

    private const int TotalSpecialTypes = (int)SpecialType.LastCorType;
    private const int TotalWellKnownMembers = (int)WellKnownMember.LastCorMember;
    private const int TotalWellKnownTypes = (int)(WellKnownType.LastNativeType - WellKnownType.First + 1);

    private readonly ConcurrentDictionary<SpecialType, NamedTypeSymbol> _specialTypes = [];
    private readonly ConcurrentDictionary<WellKnownMember, Symbol> _wellKnownMembers = [];
    private readonly ConcurrentDictionary<WellKnownType, NamedTypeSymbol> _wellKnownTypes = [];

    private int _registeredSpecialTypes;
    private int _registeredWellKnownMembers;
    private int _registeredWellKnownTypes;
    private bool _complete = false;

    private bool _lazyComplete = false;
    private readonly Lock _lazyCompleteLock = new();

    private readonly Compilation _compilation;

    private SynthesizedBelteNamespaceSymbol _belteNamespace;

    internal CorLibrary(Compilation compilation) {
        _compilation = compilation;

#if DEBUG
        InstantiationCount++;
#endif

        RegisterPrimitiveCorTypes();

        if (Instance is null)
            Interlocked.Exchange(ref Instance, this);
    }

    internal void SetBelteNamespace(SynthesizedBelteNamespaceSymbol belteNamespace) {
        Debug.Assert(_belteNamespace is null);
        _belteNamespace = belteNamespace;
    }

    internal NamespaceSymbol belteNamespace => _belteNamespace;

    internal void SetReducedState() {
        Debug.Assert(false);
        _registeredWellKnownTypes += (int)WellKnownType.LastNativeType - (int)WellKnownType.LastNativeRequiredType;
    }

    #region Public Model

    internal Symbol GetWellKnownMember(WellKnownMember wellKnownMember) {
        EnsureCorLibraryIsComplete();
        return GetWellKnownMemberCore(wellKnownMember);
    }

    internal MethodSymbol GetWellKnownMethod(WellKnownMember wellKnownMember) {
        EnsureCorLibraryIsComplete();
        return (MethodSymbol)GetWellKnownMemberCore(wellKnownMember);
    }

    // This should only be used by observational APIs (like DisplayText)
    // Everything else should use GetWellKnownType or TryGetWellKnownType
    internal bool HasWellKnownType(WellKnownType wellKnownType) {
        Debug.Assert(wellKnownType <= WellKnownType.LastNativeType, "PE well known types should be accessed through a Compilation");
        EnsureCorLibraryIsComplete();
        return HasWellKnownTypeCore(wellKnownType);
    }

    internal NamedTypeSymbol GetWellKnownType(WellKnownType wellKnownType) {
        Debug.Assert(wellKnownType <= WellKnownType.LastNativeType, "PE well known types should be accessed through a Compilation");
        EnsureCorLibraryIsComplete();
        return GetWellKnownTypeCore(wellKnownType);
    }

    internal NamedTypeSymbol TryGetWellKnownType(WellKnownType wellKnownType, Compilation compilation) {
        Debug.Assert(wellKnownType <= WellKnownType.LastNativeType, "PE well known types should be accessed through a Compilation");
        EnsureCorLibraryIsComplete();
        return TryGetWellKnownTypeCore(wellKnownType, compilation.assembly);
    }

    internal NamedTypeSymbol GetSpecialType(SpecialType specialType) {
        EnsureCorLibraryIsComplete();
        return GetSpecialTypeCore(specialType);
    }

    internal NamedTypeSymbol GetNullableType(SpecialType specialType) {
        EnsureCorLibraryIsComplete();
        return GetNullableTypeCore(specialType);
    }

    internal TypeSymbol GetOrCreateNullableType(TypeSymbol type) {
        EnsureCorLibraryIsComplete();

        if (type.IsNullableType())
            return type;

        return CreateNullableType(type);
    }

    internal NamedTypeSymbol GetOrCreateNullableType(NamedTypeSymbol type) {
        EnsureCorLibraryIsComplete();

        if (type.IsNullableType())
            return type;

        return CreateNullableType(type);
    }

    internal void RegisterDeclaredSpecialType(NamedTypeSymbol type) {
        EnsureCorLibraryIsComplete();
        RegisterSpecialType(type);
    }

    internal void RegisterDeclaredWellKnownType(WellKnownType wellKnownType, NamedTypeSymbol type) {
        EnsureCorLibraryIsComplete();
        RegisterWellKnownType(wellKnownType, type);
    }

    internal bool StillLookingForSpecialTypes() {
        EnsureCorLibraryIsComplete();
        return _registeredSpecialTypes < TotalSpecialTypes;
    }

    internal bool StillLookingForWellKnownTypes() {
        EnsureCorLibraryIsComplete();
        return _registeredWellKnownTypes < TotalWellKnownTypes;
    }

    #endregion

    #region Types

    private void EnsureCorLibraryIsComplete() {
        if (!_complete) {
            _complete = true;
            RegisterNonPrimitiveCorTypes();
            RegisterWellKnownMembers();
        }
    }

    private NamedTypeSymbol GetSpecialTypeCore(SpecialType specialType) {
        if (!_specialTypes.TryGetValue(specialType, out var result))
            throw new ArgumentException($"Special type {specialType} has not been registered");

        return result;
    }

    private NamedTypeSymbol GetNullableTypeCore(SpecialType specialType) {
        Debug.Assert(specialType != SpecialType.Void);
        return GetSpecialTypeCore(SpecialType.Nullable)
            .Construct([new TypeOrConstant(GetSpecialTypeCore(specialType))]);
    }

    private NamedTypeSymbol CreateNullableType(TypeSymbol type) {
        Debug.Assert(!type.IsVoidType());
        return GetSpecialTypeCore(SpecialType.Nullable).Construct([new TypeOrConstant(type)]);
    }

    private Symbol GetWellKnownMemberCore(WellKnownMember wellKnownMember) {
        if (!_lazyComplete && wellKnownMember.IsTupleMember() || wellKnownMember.IsArrayMember())
            CompleteLazyMembers();

        if (!_wellKnownMembers.TryGetValue(wellKnownMember, out var result))
            throw new ArgumentException($"Well known member {wellKnownMember} has not been registered");

        return result;
    }

    private bool HasWellKnownTypeCore(WellKnownType wellKnownType) {
        return _wellKnownTypes.ContainsKey(wellKnownType);
    }

    private NamedTypeSymbol GetWellKnownTypeCore(WellKnownType wellKnownType) {
        if (!_wellKnownTypes.TryGetValue(wellKnownType, out var result))
            throw new ArgumentException($"Well known type {wellKnownType} has not been registered");

        return result;
    }

    private NamedTypeSymbol TryGetWellKnownTypeCore(WellKnownType wellKnownType, AssemblySymbol assembly) {
        if (!_wellKnownTypes.TryGetValue(wellKnownType, out var result)) {
            var name = wellKnownType.GetMetadataName();
            var error = new BelteDiagnostic(Error.PredefinedTypeNotFound(name));
            var emittedName = MetadataTypeName.FromFullName(name, useCLSCompliantNameArityEncoding: true);
            result = new MissingMetadataTypeSymbol.TopLevel(assembly.modules[0], ref emittedName, error);
        }

        return result;
    }

    private void RegisterSpecialType(NamedTypeSymbol type) {
        var specialType = type.specialType;

        if (specialType == SpecialType.None)
            throw new ArgumentException($"Cannot register type {type} because it is not a special type");

        if (!_specialTypes.TryAdd(specialType, type))
            throw new ArgumentException($"Special type {specialType} was already registered");

        Interlocked.Increment(ref _registeredSpecialTypes);

        if (_registeredSpecialTypes > TotalSpecialTypes)
            throw new UnreachableException($"Registered more special types than there are special types");
    }

    private void RegisterPrimitiveCorTypes() {
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "any", SpecialType.Any));
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "int", SpecialType.Int));
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "bool", SpecialType.Bool));
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "winbool", SpecialType.WinBool));
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "char", SpecialType.Char));
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "string", SpecialType.String));
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "decimal", SpecialType.Decimal));
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "type", SpecialType.Type));
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "void", SpecialType.Void));
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "int8", SpecialType.Int8));
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "uint8", SpecialType.UInt8));
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "int16", SpecialType.Int16));
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "uint16", SpecialType.UInt16));
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "int32", SpecialType.Int32));
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "uint32", SpecialType.UInt32));
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "int64", SpecialType.Int64));
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "uint64", SpecialType.UInt64));
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "float32", SpecialType.Float32));
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "float64", SpecialType.Float64));
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "intptr", SpecialType.IntPtr));
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "uintptr", SpecialType.UIntPtr));
    }

    private void RegisterNonPrimitiveCorTypes() {
        var valueType = new PrimitiveTypeSymbol(_compilation, "ValueType", SpecialType.ValueType);
        RegisterSpecialType(valueType);

        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "Array", SpecialType.Array));
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "Enum", SpecialType.Enum, baseType: valueType));
        RegisterSpecialType(new PrimitiveTypeSymbol(_compilation, "TypedReference", SpecialType.TypedReference));

        RegisterSpecialType(new SynthesizedSimpleNamedTypeSymbol(
            "Nullable",
            TypeKind.Struct,
            valueType,
            DeclarationModifiers.None,
            null,
            [new TypeWithAnnotations(_specialTypes[SpecialType.Type])],
            SpecialType.Nullable
        ));
    }

    private void RegisterWellKnownMembers() {
        var nullableType = GetSpecialTypeCore(SpecialType.Nullable);

        RegisterWellKnownMember(WellKnownMember.Nullable_ctor,
            new SynthesizedFinishedMethodSymbol(
                new SynthesizedInstanceConstructorSymbol(nullableType),
                nullableType,
                [SynthesizedParameterSymbol.Create(
                    null,
                    new TypeWithAnnotations(nullableType.templateParameters[0]),
                    0,
                    RefKind.None
                )
            ]));

        RegisterWellKnownMember(WellKnownMember.Nullable_getValue,
            new SynthesizedFinishedMethodSymbol(
            new SynthesizedSimpleOrdinaryMethodSymbol(
                "get_Value",
                new TypeWithAnnotations(nullableType.templateParameters[0]),
                RefKind.None,
                DeclarationModifiers.None
            ), nullableType, []));

        RegisterWellKnownMember(WellKnownMember.Nullable_getHasValue,
            new SynthesizedFinishedMethodSymbol(
            new SynthesizedSimpleOrdinaryMethodSymbol(
                "get_HasValue",
                new TypeWithAnnotations(GetSpecialTypeCore(SpecialType.Bool)),
                RefKind.None,
                DeclarationModifiers.None
            ), nullableType, []));

        RegisterWellKnownMember(WellKnownMember.Nullable_GetValueOrDefault,
            new SynthesizedFinishedMethodSymbol(
            new SynthesizedSimpleOrdinaryMethodSymbol(
                "GetValueOrDefault",
                new TypeWithAnnotations(nullableType.templateParameters[0]),
                RefKind.None,
                DeclarationModifiers.None
            ), nullableType, []));

        RegisterWellKnownMember(WellKnownMember.Nullable_GetValueOrDefault_T,
            new SynthesizedFinishedMethodSymbol(
            new SynthesizedSimpleOrdinaryMethodSymbol(
                "GetValueOrDefault",
                new TypeWithAnnotations(nullableType.templateParameters[0]),
                RefKind.None,
                DeclarationModifiers.None
            ),
            nullableType,
            [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(nullableType.templateParameters[0]), 0, RefKind.None, "default")]));
    }

    private void CompleteLazyMembers() {
        lock (_lazyCompleteLock) {
            if (_lazyComplete)
                return;

#if DEBUG
            var completedAnything = false;
#endif

            // We assume if one tuple is missing, all of them are
            if (_wellKnownTypes.ContainsKey(WellKnownType.ValueTuple_T1)) {
                LazyWellKnownTupleMembers(GetWellKnownType(WellKnownType.ValueTuple_T1));
                LazyWellKnownTupleMembers(GetWellKnownType(WellKnownType.ValueTuple_T2));
                LazyWellKnownTupleMembers(GetWellKnownType(WellKnownType.ValueTuple_T3));
                LazyWellKnownTupleMembers(GetWellKnownType(WellKnownType.ValueTuple_T4));
                LazyWellKnownTupleMembers(GetWellKnownType(WellKnownType.ValueTuple_T5));
                LazyWellKnownTupleMembers(GetWellKnownType(WellKnownType.ValueTuple_T6));
                LazyWellKnownTupleMembers(GetWellKnownType(WellKnownType.ValueTuple_T7));
                LazyWellKnownTupleMembers(GetWellKnownType(WellKnownType.ValueTuple_TRest));
#if DEBUG
                completedAnything = true;
#endif
            }

            if (_wellKnownTypes.ContainsKey(WellKnownType.Array)) {
                var type = GetWellKnownType(WellKnownType.Array);
                Debug.Assert(type.instanceConstructors.Length == 2);
                RegisterWellKnownMember(WellKnownMember.Array_ctor_1, type.instanceConstructors.Single(c => c.parameterCount == 1));
                RegisterWellKnownMember(WellKnownMember.Array_ctor_2, type.instanceConstructors.Single(c => c.parameterCount == 2));
                RegisterWellKnownMember(WellKnownMember.Array_Get, type.GetMembers("Get")[0]);
                RegisterWellKnownMember(WellKnownMember.Array_Set, type.GetMembers("Set")[0]);
#if DEBUG
                completedAnything = true;
#endif
            }

#if DEBUG
            Debug.Assert(completedAnything);
#endif

            _lazyComplete = true;
        }

        void LazyWellKnownTupleMembers(NamedTypeSymbol type) {
            var arity = type.arity;

            Debug.Assert(type.instanceConstructors.Length == 1);
            RegisterWellKnownMember(NamedTypeSymbol.GetTupleCtor(arity), type.instanceConstructors[0]);

            for (var i = 0; i < arity; i++) {
                RegisterWellKnownMember(
                    NamedTypeSymbol.GetTupleTypeMember(arity, i + 1),
                    type.GetMembers(i < 7 ? $"Item{i + 1}" : "Rest")[0]
                );
            }
        }
    }

    private void RegisterWellKnownMember(WellKnownMember wellKnownMember, Symbol member) {
        if (wellKnownMember == WellKnownMember.None)
            throw new ArgumentException($"Cannot register member {member}; no given well-known-member id");

        if (!_wellKnownMembers.TryAdd(wellKnownMember, member))
            throw new ArgumentException($"Well known member {wellKnownMember} was already registered");

        Interlocked.Increment(ref _registeredWellKnownMembers);

        if (_registeredWellKnownMembers > TotalWellKnownMembers)
            throw new UnreachableException($"Registered more well known members than there are well known members");
    }

    private void RegisterWellKnownType(WellKnownType wellKnownType, NamedTypeSymbol type) {
        if (wellKnownType == WellKnownType.None)
            throw new ArgumentException($"Cannot register type {type}; no given well-known-member id");

        if (!_wellKnownTypes.TryAdd(wellKnownType, type))
            throw new ArgumentException($"Well known type {wellKnownType} was already registered");

        Interlocked.Increment(ref _registeredWellKnownTypes);

        if (_registeredWellKnownTypes > TotalWellKnownTypes)
            throw new UnreachableException($"Registered more well known types than there are well known types");
    }

    #endregion
}

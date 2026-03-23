using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.Libraries;

internal sealed class CorLibrary {
    private static readonly CorLibrary Instance = new CorLibrary();

    private const int TotalSpecialTypes = 27;
    private const int TotalSpecialTypesIncludingGraphicsTypes = TotalSpecialTypes + 6;
    private const int TotalWellKnownMembers = 3;

    private readonly ConcurrentDictionary<SpecialType, NamedTypeSymbol> _specialTypes = [];
    private readonly ConcurrentDictionary<WellKnownMembers, MethodSymbol> _wellKnownMembers = [];

    private ImmutableArray<UnaryOperatorSignature>[] _builtInUnaryOperators;
    private ImmutableArray<BinaryOperatorSignature>[][] _builtInBinaryOperators;
    private int _registeredSpecialTypes;
    private int _registeredWellKnownMembers;
    private bool _complete = false;

    private CorLibrary() {
        RegisterPrimitiveCorTypes();
    }

    #region Public Model

    internal static MethodSymbol GetWellKnownMember(WellKnownMembers wellKnownMember) {
        Instance.EnsureCorLibraryIsComplete();
        return Instance.GetWellKnownMemberCore(wellKnownMember);
    }

    internal static NamedTypeSymbol GetSpecialType(SpecialType specialType) {
        Instance.EnsureCorLibraryIsComplete();
        return Instance.GetSpecialTypeCore(specialType);
    }

    internal static NamedTypeSymbol GetNullableType(SpecialType specialType) {
        Instance.EnsureCorLibraryIsComplete();
        return Instance.GetNullableTypeCore(specialType);
    }

    internal static TypeSymbol GetOrCreateNullableType(TypeSymbol type) {
        Instance.EnsureCorLibraryIsComplete();

        if (type.IsNullableType())
            return type;

        return Instance.CreateNullableType(type);
    }

    internal static NamedTypeSymbol GetOrCreateNullableType(NamedTypeSymbol type) {
        Instance.EnsureCorLibraryIsComplete();

        if (type.IsNullableType())
            return type;

        return Instance.CreateNullableType(type);
    }

    internal static void RegisterDeclaredSpecialType(NamedTypeSymbol type) {
        Instance.EnsureCorLibraryIsComplete();
        Instance.RegisterSpecialType(type);
    }

    internal static bool StillLookingForSpecialTypes() {
        Instance.EnsureCorLibraryIsComplete();
        return Instance._registeredSpecialTypes < TotalSpecialTypesIncludingGraphicsTypes;
    }

    internal static void GetAllBuiltInBinaryOperators(
        BinaryOperatorKind kind,
        ArrayBuilder<BinaryOperatorSignature> operators) {
        Instance.EnsureCorLibraryIsComplete();
        Instance.EnsureBuiltInBinaryOperators();
        Instance.GetBinaryOperators(kind, operators);
    }

    internal static void GetAllBuiltInUnaryOperators(
        UnaryOperatorKind kind,
        ArrayBuilder<UnaryOperatorSignature> operators) {
        Instance.EnsureCorLibraryIsComplete();
        Instance.EnsureBuiltInUnaryOperators();
        Instance.GetUnaryOperators(kind, operators);
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
        return GetSpecialTypeCore(SpecialType.Nullable)
            .Construct([new TypeOrConstant(GetSpecialTypeCore(specialType))]);
    }

    private NamedTypeSymbol CreateNullableType(TypeSymbol type) {
        return GetSpecialTypeCore(SpecialType.Nullable).Construct([new TypeOrConstant(type)]);
    }

    private MethodSymbol GetWellKnownMemberCore(WellKnownMembers wellKnownMember) {
        if (!_wellKnownMembers.TryGetValue(wellKnownMember, out var result))
            throw new ArgumentException($"Well known member {wellKnownMember} has not been registered");

        return result;
    }

    private void RegisterSpecialType(NamedTypeSymbol type) {
        var specialType = type.specialType;

        if (specialType == SpecialType.None)
            throw new ArgumentException($"Cannot register type {type} because it is not a special type");

        if (!_specialTypes.TryAdd(specialType, type))
            throw new ArgumentException($"Special type {specialType} was already registered");

        Interlocked.Increment(ref _registeredSpecialTypes);

        if (_registeredSpecialTypes > TotalSpecialTypesIncludingGraphicsTypes)
            throw new UnreachableException($"Registered more special types than there are special types");
    }

    private void RegisterPrimitiveCorTypes() {
        RegisterSpecialType(new PrimitiveTypeSymbol("any", SpecialType.Any));
        RegisterSpecialType(new PrimitiveTypeSymbol("int", SpecialType.Int));
        RegisterSpecialType(new PrimitiveTypeSymbol("bool", SpecialType.Bool));
        RegisterSpecialType(new PrimitiveTypeSymbol("char", SpecialType.Char));
        RegisterSpecialType(new PrimitiveTypeSymbol("string", SpecialType.String));
        RegisterSpecialType(new PrimitiveTypeSymbol("decimal", SpecialType.Decimal));
        RegisterSpecialType(new PrimitiveTypeSymbol("type", SpecialType.Type));
        RegisterSpecialType(new PrimitiveTypeSymbol("void", SpecialType.Void));
        RegisterSpecialType(new PrimitiveTypeSymbol("int8", SpecialType.Int8));
        RegisterSpecialType(new PrimitiveTypeSymbol("uint8", SpecialType.UInt8));
        RegisterSpecialType(new PrimitiveTypeSymbol("int16", SpecialType.Int16));
        RegisterSpecialType(new PrimitiveTypeSymbol("uint16", SpecialType.UInt16));
        RegisterSpecialType(new PrimitiveTypeSymbol("int32", SpecialType.Int32));
        RegisterSpecialType(new PrimitiveTypeSymbol("uint32", SpecialType.UInt32));
        RegisterSpecialType(new PrimitiveTypeSymbol("int64", SpecialType.Int64));
        RegisterSpecialType(new PrimitiveTypeSymbol("uint64", SpecialType.UInt64));
        RegisterSpecialType(new PrimitiveTypeSymbol("float32", SpecialType.Float32));
        RegisterSpecialType(new PrimitiveTypeSymbol("float64", SpecialType.Float64));
        RegisterSpecialType(new PrimitiveTypeSymbol("intptr", SpecialType.IntPtr));
        RegisterSpecialType(new PrimitiveTypeSymbol("uintptr", SpecialType.UIntPtr));
    }

    private void RegisterNonPrimitiveCorTypes() {
        RegisterSpecialType(new PrimitiveTypeSymbol("Array", SpecialType.Array));

        RegisterSpecialType(new SynthesizedSimpleNamedTypeSymbol(
            "Nullable",
            TypeKind.Class,
            null,
            CodeAnalysis.DeclarationModifiers.None,
            null,
            [new TypeWithAnnotations(_specialTypes[SpecialType.Type])],
            SpecialType.Nullable
        ));
    }

    private void RegisterWellKnownMembers() {
        var nullableType = GetSpecialTypeCore(SpecialType.Nullable);

        RegisterWellKnownMember(WellKnownMembers.Nullable_ctor, new SynthesizedInstanceConstructorSymbol(nullableType));

        RegisterWellKnownMember(WellKnownMembers.Nullable_getValue,
            new SynthesizedFinishedMethodSymbol(
            new SynthesizedSimpleOrdinaryMethodSymbol(
                "get_Value",
                new TypeWithAnnotations(nullableType.templateParameters[0]),
                RefKind.None,
                CodeAnalysis.DeclarationModifiers.None
            ), nullableType, []));

        RegisterWellKnownMember(WellKnownMembers.Nullable_getHasValue,
            new SynthesizedFinishedMethodSymbol(
            new SynthesizedSimpleOrdinaryMethodSymbol(
                "get_HasValue",
                new TypeWithAnnotations(GetSpecialTypeCore(SpecialType.Bool)),
                RefKind.None,
                CodeAnalysis.DeclarationModifiers.None
            ), nullableType, []));
    }

    private void RegisterWellKnownMember(WellKnownMembers wellKnownMember, MethodSymbol member) {
        if (wellKnownMember == WellKnownMembers.None)
            throw new ArgumentException($"Cannot register member {member}; no given well-known-member id");

        if (!_wellKnownMembers.TryAdd(wellKnownMember, member))
            throw new ArgumentException($"Well known member {wellKnownMember} was already registered");

        Interlocked.Increment(ref _registeredWellKnownMembers);

        if (_registeredWellKnownMembers > TotalWellKnownMembers)
            throw new UnreachableException($"Registered more well known members than there are well known members");
    }

    #endregion

    #region Operators

    private void EnsureBuiltInUnaryOperators() {
        if (_builtInUnaryOperators is null) {
            var allOperators = new ImmutableArray<UnaryOperatorSignature>[] {
                GetSignaturesFromUnaryOperatorKinds([
                    (int)UnaryOperatorKind.IntPostfixIncrement,
                    (int)UnaryOperatorKind.UIntPostfixIncrement,
                    (int)UnaryOperatorKind.Float32PostfixIncrement,
                    (int)UnaryOperatorKind.Float64PostfixIncrement,
                    (int)UnaryOperatorKind.LiftedIntPostfixIncrement,
                    (int)UnaryOperatorKind.LiftedUIntPostfixIncrement,
                    (int)UnaryOperatorKind.LiftedFloat32PostfixIncrement,
                    (int)UnaryOperatorKind.LiftedFloat64PostfixIncrement,
                ]),
                GetSignaturesFromUnaryOperatorKinds([
                    (int)UnaryOperatorKind.IntPrefixIncrement,
                    (int)UnaryOperatorKind.UIntPrefixIncrement,
                    (int)UnaryOperatorKind.Float32PrefixIncrement,
                    (int)UnaryOperatorKind.Float64PrefixIncrement,
                    (int)UnaryOperatorKind.LiftedIntPrefixIncrement,
                    (int)UnaryOperatorKind.LiftedUIntPrefixIncrement,
                    (int)UnaryOperatorKind.LiftedFloat32PrefixIncrement,
                    (int)UnaryOperatorKind.LiftedFloat64PrefixIncrement,
                ]),
                GetSignaturesFromUnaryOperatorKinds([
                    (int)UnaryOperatorKind.IntPostfixDecrement,
                    (int)UnaryOperatorKind.UIntPostfixDecrement,
                    (int)UnaryOperatorKind.Float32PostfixDecrement,
                    (int)UnaryOperatorKind.Float64PostfixDecrement,
                    (int)UnaryOperatorKind.LiftedIntPostfixDecrement,
                    (int)UnaryOperatorKind.LiftedUIntPostfixDecrement,
                    (int)UnaryOperatorKind.LiftedFloat32PostfixDecrement,
                    (int)UnaryOperatorKind.LiftedFloat64PostfixDecrement,
                ]),
                GetSignaturesFromUnaryOperatorKinds([
                    (int)UnaryOperatorKind.IntPrefixDecrement,
                    (int)UnaryOperatorKind.UIntPrefixDecrement,
                    (int)UnaryOperatorKind.Float32PrefixDecrement,
                    (int)UnaryOperatorKind.Float64PrefixDecrement,
                    (int)UnaryOperatorKind.LiftedIntPrefixDecrement,
                    (int)UnaryOperatorKind.LiftedUIntPrefixDecrement,
                    (int)UnaryOperatorKind.LiftedFloat32PrefixDecrement,
                    (int)UnaryOperatorKind.LiftedFloat64PrefixDecrement,
                ]),
                GetSignaturesFromUnaryOperatorKinds([
                    (int)UnaryOperatorKind.IntUnaryPlus,
                    (int)UnaryOperatorKind.UIntUnaryPlus,
                    (int)UnaryOperatorKind.Float32UnaryPlus,
                    (int)UnaryOperatorKind.Float64UnaryPlus,
                    (int)UnaryOperatorKind.LiftedIntUnaryPlus,
                    (int)UnaryOperatorKind.LiftedUIntUnaryPlus,
                    (int)UnaryOperatorKind.LiftedFloat32UnaryPlus,
                    (int)UnaryOperatorKind.LiftedFloat64UnaryPlus,
                ]),
                GetSignaturesFromUnaryOperatorKinds([
                    (int)UnaryOperatorKind.IntUnaryMinus,
                    (int)UnaryOperatorKind.Float32UnaryMinus,
                    (int)UnaryOperatorKind.Float64UnaryMinus,
                    (int)UnaryOperatorKind.LiftedIntUnaryMinus,
                    (int)UnaryOperatorKind.LiftedFloat32UnaryMinus,
                    (int)UnaryOperatorKind.LiftedFloat64UnaryMinus,
                ]),
                GetSignaturesFromUnaryOperatorKinds([
                    (int)UnaryOperatorKind.BoolLogicalNegation,
                    (int)UnaryOperatorKind.LiftedBoolLogicalNegation,
                ]),
                GetSignaturesFromUnaryOperatorKinds([
                    (int)UnaryOperatorKind.IntBitwiseComplement,
                    (int)UnaryOperatorKind.LiftedIntBitwiseComplement,
                ]),
                [],
                [],
            };

            Interlocked.CompareExchange(ref _builtInUnaryOperators, allOperators, null);
        }
    }

    private void EnsureBuiltInBinaryOperators() {
        if (_builtInBinaryOperators is null) {
            var conditionalOperators = new ImmutableArray<BinaryOperatorSignature>[] {
                [], //multiplication
                [], //addition
                [], //subtraction
                [], //division
                [], //modulo
                [], //left shift
                [], //right shift
                [], //equal
                [], //not equal
                [], //greater than
                [], //less than
                [], //greater than or equal
                [], //less than or equal
                [], //unsigned right shift
                [OperatorFacts.GetSignature(BinaryOperatorKind.BoolConditionalAnd)], //and
                [OperatorFacts.GetSignature(BinaryOperatorKind.BoolConditionalOr)], //or
                [], //xor
                [], //power
            };

            var nonConditionalOperators = new ImmutableArray<BinaryOperatorSignature>[] {
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntMultiplication,
                    (int)BinaryOperatorKind.UIntMultiplication,
                    (int)BinaryOperatorKind.Float32Multiplication,
                    (int)BinaryOperatorKind.Float64Multiplication,
                    (int)BinaryOperatorKind.LiftedIntMultiplication,
                    (int)BinaryOperatorKind.LiftedUIntMultiplication,
                    (int)BinaryOperatorKind.LiftedFloat32Multiplication,
                    (int)BinaryOperatorKind.LiftedFloat64Multiplication,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntAddition,
                    (int)BinaryOperatorKind.UIntAddition,
                    (int)BinaryOperatorKind.Float32Addition,
                    (int)BinaryOperatorKind.Float64Addition,
                    (int)BinaryOperatorKind.StringConcatenation,
                    (int)BinaryOperatorKind.LiftedIntAddition,
                    (int)BinaryOperatorKind.LiftedUIntAddition,
                    (int)BinaryOperatorKind.LiftedFloat32Addition,
                    (int)BinaryOperatorKind.LiftedFloat64Addition,
                    (int)BinaryOperatorKind.LiftedStringConcatenation,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntSubtraction,
                    (int)BinaryOperatorKind.UIntSubtraction,
                    (int)BinaryOperatorKind.Float32Subtraction,
                    (int)BinaryOperatorKind.Float64Subtraction,
                    (int)BinaryOperatorKind.LiftedIntSubtraction,
                    (int)BinaryOperatorKind.LiftedUIntSubtraction,
                    (int)BinaryOperatorKind.LiftedFloat32Subtraction,
                    (int)BinaryOperatorKind.LiftedFloat64Subtraction,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntDivision,
                    (int)BinaryOperatorKind.UIntDivision,
                    (int)BinaryOperatorKind.Float32Division,
                    (int)BinaryOperatorKind.Float64Division,
                    (int)BinaryOperatorKind.LiftedIntDivision,
                    (int)BinaryOperatorKind.LiftedUIntDivision,
                    (int)BinaryOperatorKind.LiftedFloat32Division,
                    (int)BinaryOperatorKind.LiftedFloat64Division,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntModulo,
                    (int)BinaryOperatorKind.UIntModulo,
                    (int)BinaryOperatorKind.Float32Modulo,
                    (int)BinaryOperatorKind.Float64Modulo,
                    (int)BinaryOperatorKind.LiftedIntModulo,
                    (int)BinaryOperatorKind.LiftedUIntModulo,
                    (int)BinaryOperatorKind.LiftedFloat32Modulo,
                    (int)BinaryOperatorKind.LiftedFloat64Modulo,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntLeftShift,
                    (int)BinaryOperatorKind.UIntLeftShift,
                    (int)BinaryOperatorKind.LiftedIntLeftShift,
                    (int)BinaryOperatorKind.LiftedUIntLeftShift,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntRightShift,
                    (int)BinaryOperatorKind.UIntRightShift,
                    (int)BinaryOperatorKind.LiftedIntRightShift,
                    (int)BinaryOperatorKind.LiftedUIntRightShift,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntEqual,
                    (int)BinaryOperatorKind.UIntEqual,
                    (int)BinaryOperatorKind.Float32Equal,
                    (int)BinaryOperatorKind.Float64Equal,
                    (int)BinaryOperatorKind.BoolEqual,
                    (int)BinaryOperatorKind.ObjectEqual,
                    (int)BinaryOperatorKind.StringEqual,
                    (int)BinaryOperatorKind.CharEqual,
                    (int)BinaryOperatorKind.TypeEqual,
                    (int)BinaryOperatorKind.LiftedIntEqual,
                    (int)BinaryOperatorKind.LiftedUIntEqual,
                    (int)BinaryOperatorKind.LiftedFloat32Equal,
                    (int)BinaryOperatorKind.LiftedFloat64Equal,
                    (int)BinaryOperatorKind.LiftedBoolEqual,
                    (int)BinaryOperatorKind.LiftedObjectEqual,
                    (int)BinaryOperatorKind.LiftedStringEqual,
                    (int)BinaryOperatorKind.LiftedCharEqual,
                    (int)BinaryOperatorKind.LiftedTypeEqual,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntNotEqual,
                    (int)BinaryOperatorKind.UIntNotEqual,
                    (int)BinaryOperatorKind.Float32NotEqual,
                    (int)BinaryOperatorKind.Float64NotEqual,
                    (int)BinaryOperatorKind.BoolNotEqual,
                    (int)BinaryOperatorKind.ObjectNotEqual,
                    (int)BinaryOperatorKind.StringNotEqual,
                    (int)BinaryOperatorKind.CharNotEqual,
                    (int)BinaryOperatorKind.TypeNotEqual,
                    (int)BinaryOperatorKind.LiftedIntNotEqual,
                    (int)BinaryOperatorKind.LiftedUIntNotEqual,
                    (int)BinaryOperatorKind.LiftedFloat32NotEqual,
                    (int)BinaryOperatorKind.LiftedFloat64NotEqual,
                    (int)BinaryOperatorKind.LiftedBoolNotEqual,
                    (int)BinaryOperatorKind.LiftedObjectNotEqual,
                    (int)BinaryOperatorKind.LiftedStringNotEqual,
                    (int)BinaryOperatorKind.LiftedCharNotEqual,
                    (int)BinaryOperatorKind.LiftedTypeNotEqual,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntGreaterThan,
                    (int)BinaryOperatorKind.UIntGreaterThan,
                    (int)BinaryOperatorKind.Float32GreaterThan,
                    (int)BinaryOperatorKind.Float64GreaterThan,
                    (int)BinaryOperatorKind.LiftedIntGreaterThan,
                    (int)BinaryOperatorKind.LiftedUIntGreaterThan,
                    (int)BinaryOperatorKind.LiftedFloat32GreaterThan,
                    (int)BinaryOperatorKind.LiftedFloat64GreaterThan,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntLessThan,
                    (int)BinaryOperatorKind.UIntLessThan,
                    (int)BinaryOperatorKind.Float32LessThan,
                    (int)BinaryOperatorKind.Float64LessThan,
                    (int)BinaryOperatorKind.LiftedIntLessThan,
                    (int)BinaryOperatorKind.LiftedUIntLessThan,
                    (int)BinaryOperatorKind.LiftedFloat32LessThan,
                    (int)BinaryOperatorKind.LiftedFloat64LessThan,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntGreaterThanOrEqual,
                    (int)BinaryOperatorKind.UIntGreaterThanOrEqual,
                    (int)BinaryOperatorKind.Float32GreaterThanOrEqual,
                    (int)BinaryOperatorKind.Float64GreaterThanOrEqual,
                    (int)BinaryOperatorKind.LiftedIntGreaterThanOrEqual,
                    (int)BinaryOperatorKind.LiftedUIntGreaterThanOrEqual,
                    (int)BinaryOperatorKind.LiftedFloat32GreaterThanOrEqual,
                    (int)BinaryOperatorKind.LiftedFloat64GreaterThanOrEqual,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntLessThanOrEqual,
                    (int)BinaryOperatorKind.UIntLessThanOrEqual,
                    (int)BinaryOperatorKind.Float32LessThanOrEqual,
                    (int)BinaryOperatorKind.Float64LessThanOrEqual,
                    (int)BinaryOperatorKind.LiftedIntLessThanOrEqual,
                    (int)BinaryOperatorKind.LiftedUIntLessThanOrEqual,
                    (int)BinaryOperatorKind.LiftedFloat32LessThanOrEqual,
                    (int)BinaryOperatorKind.LiftedFloat64LessThanOrEqual,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntUnsignedRightShift,
                    (int)BinaryOperatorKind.UIntUnsignedRightShift,
                    (int)BinaryOperatorKind.LiftedIntUnsignedRightShift,
                    (int)BinaryOperatorKind.LiftedUIntUnsignedRightShift,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntAnd,
                    (int)BinaryOperatorKind.UIntAnd,
                    (int)BinaryOperatorKind.BoolAnd,
                    (int)BinaryOperatorKind.LiftedIntAnd,
                    (int)BinaryOperatorKind.LiftedUIntAnd,
                    (int)BinaryOperatorKind.LiftedBoolAnd,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntOr,
                    (int)BinaryOperatorKind.UIntOr,
                    (int)BinaryOperatorKind.BoolOr,
                    (int)BinaryOperatorKind.LiftedIntOr,
                    (int)BinaryOperatorKind.LiftedUIntOr,
                    (int)BinaryOperatorKind.LiftedBoolOr,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntXor,
                    (int)BinaryOperatorKind.UIntXor,
                    (int)BinaryOperatorKind.BoolXor,
                    (int)BinaryOperatorKind.LiftedIntXor,
                    (int)BinaryOperatorKind.LiftedUIntXor,
                    (int)BinaryOperatorKind.LiftedBoolXor,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntPower,
                    (int)BinaryOperatorKind.UIntPower,
                    (int)BinaryOperatorKind.Float32Power,
                    (int)BinaryOperatorKind.Float64Power,
                    (int)BinaryOperatorKind.LiftedIntPower,
                    (int)BinaryOperatorKind.LiftedUIntPower,
                    (int)BinaryOperatorKind.LiftedFloat32Power,
                    (int)BinaryOperatorKind.LiftedFloat64Power,
                ]),
            };

            var allOperators = new[] { nonConditionalOperators, conditionalOperators };
            Interlocked.CompareExchange(ref _builtInBinaryOperators, allOperators, null);
        }
    }

    private ImmutableArray<BinaryOperatorSignature> GetSignaturesFromBinaryOperatorKinds(int[] operatorKinds) {
        var builder = ArrayBuilder<BinaryOperatorSignature>.GetInstance();

        foreach (var kind in operatorKinds)
            builder.Add(OperatorFacts.GetSignature((BinaryOperatorKind)kind));

        return builder.ToImmutableAndFree();
    }

    private ImmutableArray<UnaryOperatorSignature> GetSignaturesFromUnaryOperatorKinds(int[] operatorKinds) {
        var builder = ArrayBuilder<UnaryOperatorSignature>.GetInstance();

        foreach (var kind in operatorKinds)
            builder.Add(OperatorFacts.GetSignature((UnaryOperatorKind)kind));

        return builder.ToImmutableAndFree();
    }

    private void GetBinaryOperators(BinaryOperatorKind kind, ArrayBuilder<BinaryOperatorSignature> operators) {
        foreach (var op in _builtInBinaryOperators[kind.IsConditional() ? 1 : 0][kind.OperatorIndex()])
            operators.Add(op);
    }

    private void GetUnaryOperators(UnaryOperatorKind kind, ArrayBuilder<UnaryOperatorSignature> operators) {
        foreach (var op in _builtInUnaryOperators[kind.OperatorIndex()])
            operators.Add(op);
    }

    #endregion

}

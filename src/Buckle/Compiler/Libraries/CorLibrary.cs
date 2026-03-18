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

    private const int TotalSpecialTypes = 13 - 2; // TODO remove -2 after adding List and Dict
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

        RegisterWellKnownMember(WellKnownMembers.Nullable_ctor, new SynthesizedConstructorSymbol(nullableType));

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
                    (int)UnaryOperatorKind.DecimalPostfixIncrement,
                    (int)UnaryOperatorKind.LiftedIntPostfixIncrement,
                    (int)UnaryOperatorKind.LiftedDecimalPostfixIncrement,
                ]),
                GetSignaturesFromUnaryOperatorKinds([
                    (int)UnaryOperatorKind.IntPrefixIncrement,
                    (int)UnaryOperatorKind.DecimalPrefixIncrement,
                    (int)UnaryOperatorKind.LiftedIntPrefixIncrement,
                    (int)UnaryOperatorKind.LiftedDecimalPrefixIncrement,
                ]),
                GetSignaturesFromUnaryOperatorKinds([
                    (int)UnaryOperatorKind.IntPostfixDecrement,
                    (int)UnaryOperatorKind.DecimalPostfixDecrement,
                    (int)UnaryOperatorKind.LiftedIntPostfixDecrement,
                    (int)UnaryOperatorKind.LiftedDecimalPostfixDecrement,
                ]),
                GetSignaturesFromUnaryOperatorKinds([
                    (int)UnaryOperatorKind.IntPrefixDecrement,
                    (int)UnaryOperatorKind.DecimalPrefixDecrement,
                    (int)UnaryOperatorKind.LiftedIntPrefixDecrement,
                    (int)UnaryOperatorKind.LiftedDecimalPrefixDecrement,
                ]),
                GetSignaturesFromUnaryOperatorKinds([
                    (int)UnaryOperatorKind.IntUnaryPlus,
                    (int)UnaryOperatorKind.DecimalUnaryPlus,
                    (int)UnaryOperatorKind.LiftedIntUnaryPlus,
                    (int)UnaryOperatorKind.LiftedDecimalUnaryPlus,
                ]),
                GetSignaturesFromUnaryOperatorKinds([
                    (int)UnaryOperatorKind.IntUnaryMinus,
                    (int)UnaryOperatorKind.DecimalUnaryMinus,
                    (int)UnaryOperatorKind.LiftedIntUnaryMinus,
                    (int)UnaryOperatorKind.LiftedDecimalUnaryMinus,
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
                    (int)BinaryOperatorKind.DecimalMultiplication,
                    (int)BinaryOperatorKind.LiftedIntMultiplication,
                    (int)BinaryOperatorKind.LiftedDecimalMultiplication,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntAddition,
                    (int)BinaryOperatorKind.DecimalAddition,
                    (int)BinaryOperatorKind.StringConcatenation,
                    (int)BinaryOperatorKind.LiftedIntAddition,
                    (int)BinaryOperatorKind.LiftedDecimalAddition,
                    (int)BinaryOperatorKind.LiftedStringConcatenation,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntSubtraction,
                    (int)BinaryOperatorKind.DecimalSubtraction,
                    (int)BinaryOperatorKind.LiftedIntSubtraction,
                    (int)BinaryOperatorKind.LiftedDecimalSubtraction,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntDivision,
                    (int)BinaryOperatorKind.DecimalDivision,
                    (int)BinaryOperatorKind.LiftedIntDivision,
                    (int)BinaryOperatorKind.LiftedDecimalDivision,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntModulo,
                    (int)BinaryOperatorKind.DecimalModulo,
                    (int)BinaryOperatorKind.LiftedIntModulo,
                    (int)BinaryOperatorKind.LiftedDecimalModulo,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntLeftShift,
                    (int)BinaryOperatorKind.LiftedIntLeftShift,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntRightShift,
                    (int)BinaryOperatorKind.LiftedIntRightShift,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntEqual,
                    (int)BinaryOperatorKind.DecimalEqual,
                    (int)BinaryOperatorKind.BoolEqual,
                    (int)BinaryOperatorKind.ObjectEqual,
                    (int)BinaryOperatorKind.StringEqual,
                    (int)BinaryOperatorKind.CharEqual,
                    (int)BinaryOperatorKind.TypeEqual,
                    (int)BinaryOperatorKind.LiftedIntEqual,
                    (int)BinaryOperatorKind.LiftedDecimalEqual,
                    (int)BinaryOperatorKind.LiftedBoolEqual,
                    (int)BinaryOperatorKind.LiftedObjectEqual,
                    (int)BinaryOperatorKind.LiftedStringEqual,
                    (int)BinaryOperatorKind.LiftedCharEqual,
                    (int)BinaryOperatorKind.LiftedTypeEqual,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntNotEqual,
                    (int)BinaryOperatorKind.DecimalNotEqual,
                    (int)BinaryOperatorKind.BoolNotEqual,
                    (int)BinaryOperatorKind.ObjectNotEqual,
                    (int)BinaryOperatorKind.StringNotEqual,
                    (int)BinaryOperatorKind.CharNotEqual,
                    (int)BinaryOperatorKind.TypeNotEqual,
                    (int)BinaryOperatorKind.LiftedIntNotEqual,
                    (int)BinaryOperatorKind.LiftedDecimalNotEqual,
                    (int)BinaryOperatorKind.LiftedBoolNotEqual,
                    (int)BinaryOperatorKind.LiftedObjectNotEqual,
                    (int)BinaryOperatorKind.LiftedStringNotEqual,
                    (int)BinaryOperatorKind.LiftedCharNotEqual,
                    (int)BinaryOperatorKind.LiftedTypeNotEqual,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntGreaterThan,
                    (int)BinaryOperatorKind.DecimalGreaterThan,
                    (int)BinaryOperatorKind.LiftedIntGreaterThan,
                    (int)BinaryOperatorKind.LiftedDecimalGreaterThan,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntLessThan,
                    (int)BinaryOperatorKind.DecimalLessThan,
                    (int)BinaryOperatorKind.LiftedIntLessThan,
                    (int)BinaryOperatorKind.LiftedDecimalLessThan,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntGreaterThanOrEqual,
                    (int)BinaryOperatorKind.DecimalGreaterThanOrEqual,
                    (int)BinaryOperatorKind.LiftedIntGreaterThanOrEqual,
                    (int)BinaryOperatorKind.LiftedDecimalGreaterThanOrEqual,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntLessThanOrEqual,
                    (int)BinaryOperatorKind.DecimalLessThanOrEqual,
                    (int)BinaryOperatorKind.LiftedIntLessThanOrEqual,
                    (int)BinaryOperatorKind.LiftedDecimalLessThanOrEqual,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntUnsignedRightShift,
                    (int)BinaryOperatorKind.LiftedIntUnsignedRightShift,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntAnd,
                    (int)BinaryOperatorKind.BoolAnd,
                    (int)BinaryOperatorKind.LiftedIntAnd,
                    (int)BinaryOperatorKind.LiftedBoolAnd,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntOr,
                    (int)BinaryOperatorKind.BoolOr,
                    (int)BinaryOperatorKind.LiftedIntOr,
                    (int)BinaryOperatorKind.LiftedBoolOr,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntXor,
                    (int)BinaryOperatorKind.BoolXor,
                    (int)BinaryOperatorKind.LiftedIntXor,
                    (int)BinaryOperatorKind.LiftedBoolXor,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.IntPower,
                    (int)BinaryOperatorKind.DecimalPower,
                    (int)BinaryOperatorKind.LiftedIntPower,
                    (int)BinaryOperatorKind.LiftedDecimalPower,
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

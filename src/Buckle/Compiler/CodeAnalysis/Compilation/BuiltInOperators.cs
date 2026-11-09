using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal sealed class BuiltInOperators {
    private ImmutableArray<UnaryOperatorSignature>[] _builtInUnaryOperators;
    private ImmutableArray<BinaryOperatorSignature>[][] _builtInBinaryOperators;

    private readonly Compilation _compilation;

    internal BuiltInOperators(Compilation compilation) {
        _compilation = compilation;
    }

    internal void GetAllBuiltInBinaryOperators(
        BinaryOperatorKind kind,
        ArrayBuilder<BinaryOperatorSignature> operators) {
        EnsureBuiltInBinaryOperators();
        GetBinaryOperators(kind, operators);
    }

    internal void GetAllBuiltInUnaryOperators(
        UnaryOperatorKind kind,
        ArrayBuilder<UnaryOperatorSignature> operators) {
        EnsureBuiltInUnaryOperators();
        GetUnaryOperators(kind, operators);
    }

    internal BinaryOperatorSignature GetSignature(BinaryOperatorKind kind) {
        var left = TypeFromKind(kind);

        switch (kind.Operator()) {
            case BinaryOperatorKind.Multiplication:
            case BinaryOperatorKind.Division:
            case BinaryOperatorKind.Subtraction:
            case BinaryOperatorKind.Modulo:
            case BinaryOperatorKind.And:
            case BinaryOperatorKind.Or:
            case BinaryOperatorKind.Xor:
            case BinaryOperatorKind.Min:
            case BinaryOperatorKind.Max:
                return new BinaryOperatorSignature(kind, left, left, left);
            case BinaryOperatorKind.Addition:
                return new BinaryOperatorSignature(kind, left, TypeFromKind(kind), TypeFromKind(kind));
            case BinaryOperatorKind.LeftShift:
            case BinaryOperatorKind.RightShift:
            case BinaryOperatorKind.UnsignedRightShift:
                var rightType = _compilation.GetSpecialType(SpecialType.Int);

                if (kind.IsLifted())
                    rightType = _compilation.corLibrary.GetOrCreateNullableType(rightType);

                return new BinaryOperatorSignature(kind, left, rightType, left);
            case BinaryOperatorKind.Equal:
            case BinaryOperatorKind.NotEqual:
            case BinaryOperatorKind.GreaterThan:
            case BinaryOperatorKind.LessThan:
            case BinaryOperatorKind.GreaterThanOrEqual:
            case BinaryOperatorKind.LessThanOrEqual:
                return new BinaryOperatorSignature(
                    kind,
                    left,
                    left,
                    kind.IsLifted()
                        ? _compilation.corLibrary.GetNullableType(SpecialType.Bool)
                        : _compilation.GetSpecialType(SpecialType.Bool)
                );
        }

        return new BinaryOperatorSignature(kind, left, TypeFromKind(kind), TypeFromKind(kind));
    }

    internal UnaryOperatorSignature GetSignature(UnaryOperatorKind kind) {
        var opType = kind.OperandTypes() switch {
            UnaryOperatorKind.Int8 => _compilation.GetSpecialType(SpecialType.Int8),
            UnaryOperatorKind.Int16 => _compilation.GetSpecialType(SpecialType.Int16),
            UnaryOperatorKind.Int32 => _compilation.GetSpecialType(SpecialType.Int32),
            UnaryOperatorKind.UInt8 => _compilation.GetSpecialType(SpecialType.UInt8),
            UnaryOperatorKind.UInt16 => _compilation.GetSpecialType(SpecialType.UInt16),
            UnaryOperatorKind.UInt32 => _compilation.GetSpecialType(SpecialType.UInt32),
            // UnaryOperatorKind.Int => _compilation.GetSpecialType(SpecialType.Int64),
            UnaryOperatorKind.Int64 => _compilation.GetSpecialType(SpecialType.Int),
            UnaryOperatorKind.UInt64 => _compilation.GetSpecialType(SpecialType.UInt64),
            UnaryOperatorKind.Char => _compilation.GetSpecialType(SpecialType.Char),
            UnaryOperatorKind.Float32 => _compilation.GetSpecialType(SpecialType.Float32),
            // UnaryOperatorKind.Float64 => _compilation.GetSpecialType(SpecialType.Float64),
            UnaryOperatorKind.Float64 => _compilation.GetSpecialType(SpecialType.Decimal),
            UnaryOperatorKind.Bool => _compilation.GetSpecialType(SpecialType.Bool),
            _ => throw ExceptionUtilities.UnexpectedValue(kind.OperandTypes()),
        };

        if (kind.IsLifted())
            opType = _compilation.corLibrary.GetOrCreateNullableType(opType);

        return new UnaryOperatorSignature(kind, opType, opType);
    }

    internal TypeSymbol TypeFromKind(BinaryOperatorKind kind) {
        var type = kind.OperandTypes() switch {
            BinaryOperatorKind.Int8 => _compilation.GetSpecialType(SpecialType.Int8),
            BinaryOperatorKind.Int16 => _compilation.GetSpecialType(SpecialType.Int16),
            BinaryOperatorKind.Int32 => _compilation.GetSpecialType(SpecialType.Int32),
            BinaryOperatorKind.UInt8 => _compilation.GetSpecialType(SpecialType.UInt8),
            BinaryOperatorKind.UInt16 => _compilation.GetSpecialType(SpecialType.UInt16),
            BinaryOperatorKind.UInt32 => _compilation.GetSpecialType(SpecialType.UInt32),
            // BinaryOperatorKind.Int => _compilation.GetSpecialType(SpecialType.Int64),
            BinaryOperatorKind.Int64 => _compilation.GetSpecialType(SpecialType.Int),
            BinaryOperatorKind.UInt64 => _compilation.GetSpecialType(SpecialType.UInt64),
            BinaryOperatorKind.Float32 => _compilation.GetSpecialType(SpecialType.Float32),
            // BinaryOperatorKind.Float64 => _compilation.GetSpecialType(SpecialType.Float64),
            BinaryOperatorKind.Float64 => _compilation.GetSpecialType(SpecialType.Decimal),
            BinaryOperatorKind.Bool => _compilation.GetSpecialType(SpecialType.Bool),
            BinaryOperatorKind.Object => _compilation.GetSpecialType(SpecialType.Object),
            BinaryOperatorKind.String => _compilation.GetSpecialType(SpecialType.String),
            BinaryOperatorKind.Char => _compilation.GetSpecialType(SpecialType.Char),
            BinaryOperatorKind.Type => _compilation.GetSpecialType(SpecialType.Type),
            BinaryOperatorKind.Any => _compilation.GetSpecialType(SpecialType.Any),
            _ => throw ExceptionUtilities.UnexpectedValue(kind.OperandTypes()),
        };

        if (kind.IsLifted())
            type = _compilation.corLibrary.GetOrCreateNullableType(type);

        return type;
    }

    #region Operators

    private void EnsureBuiltInUnaryOperators() {
        if (_builtInUnaryOperators is null) {
            var allOperators = new ImmutableArray<UnaryOperatorSignature>[] {
                GetSignaturesFromUnaryOperatorKinds([
                    (int)UnaryOperatorKind.Int8PostfixIncrement,
                    (int)UnaryOperatorKind.Int16PostfixIncrement,
                    (int)UnaryOperatorKind.Int32PostfixIncrement,
                    (int)UnaryOperatorKind.Int64PostfixIncrement,
                    (int)UnaryOperatorKind.UInt8PostfixIncrement,
                    (int)UnaryOperatorKind.UInt16PostfixIncrement,
                    (int)UnaryOperatorKind.UInt32PostfixIncrement,
                    (int)UnaryOperatorKind.UInt64PostfixIncrement,
                    (int)UnaryOperatorKind.Float32PostfixIncrement,
                    (int)UnaryOperatorKind.Float64PostfixIncrement,
                    (int)UnaryOperatorKind.LiftedInt8PostfixIncrement,
                    (int)UnaryOperatorKind.LiftedInt16PostfixIncrement,
                    (int)UnaryOperatorKind.LiftedInt32PostfixIncrement,
                    (int)UnaryOperatorKind.LiftedInt64PostfixIncrement,
                    (int)UnaryOperatorKind.LiftedUInt8PostfixIncrement,
                    (int)UnaryOperatorKind.LiftedUInt16PostfixIncrement,
                    (int)UnaryOperatorKind.LiftedUInt32PostfixIncrement,
                    (int)UnaryOperatorKind.LiftedUInt64PostfixIncrement,
                    (int)UnaryOperatorKind.LiftedFloat32PostfixIncrement,
                    (int)UnaryOperatorKind.LiftedFloat64PostfixIncrement,
                ]),
                GetSignaturesFromUnaryOperatorKinds([
                    (int)UnaryOperatorKind.Int8PrefixIncrement,
                    (int)UnaryOperatorKind.Int16PrefixIncrement,
                    (int)UnaryOperatorKind.Int32PrefixIncrement,
                    (int)UnaryOperatorKind.Int64PrefixIncrement,
                    (int)UnaryOperatorKind.UInt8PrefixIncrement,
                    (int)UnaryOperatorKind.UInt16PrefixIncrement,
                    (int)UnaryOperatorKind.UInt32PrefixIncrement,
                    (int)UnaryOperatorKind.UInt64PrefixIncrement,
                    (int)UnaryOperatorKind.Float32PrefixIncrement,
                    (int)UnaryOperatorKind.Float64PrefixIncrement,
                    (int)UnaryOperatorKind.LiftedInt8PrefixIncrement,
                    (int)UnaryOperatorKind.LiftedInt16PrefixIncrement,
                    (int)UnaryOperatorKind.LiftedInt32PrefixIncrement,
                    (int)UnaryOperatorKind.LiftedInt64PrefixIncrement,
                    (int)UnaryOperatorKind.LiftedUInt8PrefixIncrement,
                    (int)UnaryOperatorKind.LiftedUInt16PrefixIncrement,
                    (int)UnaryOperatorKind.LiftedUInt32PrefixIncrement,
                    (int)UnaryOperatorKind.LiftedUInt64PrefixIncrement,
                    (int)UnaryOperatorKind.LiftedFloat32PrefixIncrement,
                    (int)UnaryOperatorKind.LiftedFloat64PrefixIncrement,
                ]),
                GetSignaturesFromUnaryOperatorKinds([
                    (int)UnaryOperatorKind.Int8PostfixDecrement,
                    (int)UnaryOperatorKind.Int16PostfixDecrement,
                    (int)UnaryOperatorKind.Int32PostfixDecrement,
                    (int)UnaryOperatorKind.Int64PostfixDecrement,
                    (int)UnaryOperatorKind.UInt8PostfixDecrement,
                    (int)UnaryOperatorKind.UInt16PostfixDecrement,
                    (int)UnaryOperatorKind.UInt32PostfixDecrement,
                    (int)UnaryOperatorKind.UInt64PostfixDecrement,
                    (int)UnaryOperatorKind.Float32PostfixDecrement,
                    (int)UnaryOperatorKind.Float64PostfixDecrement,
                    (int)UnaryOperatorKind.LiftedInt8PostfixDecrement,
                    (int)UnaryOperatorKind.LiftedInt16PostfixDecrement,
                    (int)UnaryOperatorKind.LiftedInt32PostfixDecrement,
                    (int)UnaryOperatorKind.LiftedInt64PostfixDecrement,
                    (int)UnaryOperatorKind.LiftedUInt8PostfixDecrement,
                    (int)UnaryOperatorKind.LiftedUInt16PostfixDecrement,
                    (int)UnaryOperatorKind.LiftedUInt32PostfixDecrement,
                    (int)UnaryOperatorKind.LiftedUInt64PostfixDecrement,
                    (int)UnaryOperatorKind.LiftedFloat32PostfixDecrement,
                    (int)UnaryOperatorKind.LiftedFloat64PostfixDecrement,
                ]),
                GetSignaturesFromUnaryOperatorKinds([
                    (int)UnaryOperatorKind.Int8PrefixDecrement,
                    (int)UnaryOperatorKind.Int16PrefixDecrement,
                    (int)UnaryOperatorKind.Int32PrefixDecrement,
                    (int)UnaryOperatorKind.Int64PrefixDecrement,
                    (int)UnaryOperatorKind.UInt8PrefixDecrement,
                    (int)UnaryOperatorKind.UInt16PrefixDecrement,
                    (int)UnaryOperatorKind.UInt32PrefixDecrement,
                    (int)UnaryOperatorKind.UInt64PrefixDecrement,
                    (int)UnaryOperatorKind.Float32PrefixDecrement,
                    (int)UnaryOperatorKind.Float64PrefixDecrement,
                    (int)UnaryOperatorKind.LiftedInt8PrefixDecrement,
                    (int)UnaryOperatorKind.LiftedInt16PrefixDecrement,
                    (int)UnaryOperatorKind.LiftedInt32PrefixDecrement,
                    (int)UnaryOperatorKind.LiftedInt64PrefixDecrement,
                    (int)UnaryOperatorKind.LiftedUInt8PrefixDecrement,
                    (int)UnaryOperatorKind.LiftedUInt16PrefixDecrement,
                    (int)UnaryOperatorKind.LiftedUInt32PrefixDecrement,
                    (int)UnaryOperatorKind.LiftedUInt64PrefixDecrement,
                    (int)UnaryOperatorKind.LiftedFloat32PrefixDecrement,
                    (int)UnaryOperatorKind.LiftedFloat64PrefixDecrement,
                ]),
                GetSignaturesFromUnaryOperatorKinds([
                    (int)UnaryOperatorKind.Int32UnaryPlus,
                    (int)UnaryOperatorKind.Int64UnaryPlus,
                    (int)UnaryOperatorKind.UInt32UnaryPlus,
                    (int)UnaryOperatorKind.UInt64UnaryPlus,
                    (int)UnaryOperatorKind.Float32UnaryPlus,
                    (int)UnaryOperatorKind.Float64UnaryPlus,
                    (int)UnaryOperatorKind.LiftedInt32UnaryPlus,
                    (int)UnaryOperatorKind.LiftedInt64UnaryPlus,
                    (int)UnaryOperatorKind.LiftedUInt32UnaryPlus,
                    (int)UnaryOperatorKind.LiftedUInt64UnaryPlus,
                    (int)UnaryOperatorKind.LiftedFloat32UnaryPlus,
                    (int)UnaryOperatorKind.LiftedFloat64UnaryPlus,
                ]),
                GetSignaturesFromUnaryOperatorKinds([
                    (int)UnaryOperatorKind.Int32UnaryMinus,
                    (int)UnaryOperatorKind.Int64UnaryMinus,
                    (int)UnaryOperatorKind.Float32UnaryMinus,
                    (int)UnaryOperatorKind.Float64UnaryMinus,
                    (int)UnaryOperatorKind.LiftedInt32UnaryMinus,
                    (int)UnaryOperatorKind.LiftedInt64UnaryMinus,
                    (int)UnaryOperatorKind.LiftedFloat32UnaryMinus,
                    (int)UnaryOperatorKind.LiftedFloat64UnaryMinus,
                ]),
                GetSignaturesFromUnaryOperatorKinds([
                    (int)UnaryOperatorKind.BoolLogicalNegation,
                    (int)UnaryOperatorKind.LiftedBoolLogicalNegation,
                ]),
                GetSignaturesFromUnaryOperatorKinds([
                    (int)UnaryOperatorKind.Int32BitwiseComplement,
                    (int)UnaryOperatorKind.Int64BitwiseComplement,
                    (int)UnaryOperatorKind.UInt32BitwiseComplement,
                    (int)UnaryOperatorKind.UInt64BitwiseComplement,
                    (int)UnaryOperatorKind.LiftedInt32BitwiseComplement,
                    (int)UnaryOperatorKind.LiftedInt64BitwiseComplement,
                    (int)UnaryOperatorKind.LiftedUInt32BitwiseComplement,
                    (int)UnaryOperatorKind.LiftedUInt64BitwiseComplement,
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
                [GetSignature(BinaryOperatorKind.BoolConditionalAnd)], //and
                [GetSignature(BinaryOperatorKind.BoolConditionalOr)], //or
                [], //xor
                [], //power
                [], //min
                [], //max
            };

            var nonConditionalOperators = new ImmutableArray<BinaryOperatorSignature>[] {
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.Int32Multiplication,
                    (int)BinaryOperatorKind.Int64Multiplication,
                    (int)BinaryOperatorKind.UInt32Multiplication,
                    (int)BinaryOperatorKind.UInt64Multiplication,
                    (int)BinaryOperatorKind.Float32Multiplication,
                    (int)BinaryOperatorKind.Float64Multiplication,
                    (int)BinaryOperatorKind.LiftedInt32Multiplication,
                    (int)BinaryOperatorKind.LiftedInt64Multiplication,
                    (int)BinaryOperatorKind.LiftedUInt32Multiplication,
                    (int)BinaryOperatorKind.LiftedUInt64Multiplication,
                    (int)BinaryOperatorKind.LiftedFloat32Multiplication,
                    (int)BinaryOperatorKind.LiftedFloat64Multiplication,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.Int32Addition,
                    (int)BinaryOperatorKind.Int64Addition,
                    (int)BinaryOperatorKind.UInt32Addition,
                    (int)BinaryOperatorKind.UInt64Addition,
                    (int)BinaryOperatorKind.Float32Addition,
                    (int)BinaryOperatorKind.Float64Addition,
                    (int)BinaryOperatorKind.StringConcatenation,
                    (int)BinaryOperatorKind.LiftedInt32Addition,
                    (int)BinaryOperatorKind.LiftedInt64Addition,
                    (int)BinaryOperatorKind.LiftedUInt32Addition,
                    (int)BinaryOperatorKind.LiftedUInt64Addition,
                    (int)BinaryOperatorKind.LiftedFloat32Addition,
                    (int)BinaryOperatorKind.LiftedFloat64Addition,
                    (int)BinaryOperatorKind.LiftedStringConcatenation,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.Int32Subtraction,
                    (int)BinaryOperatorKind.Int64Subtraction,
                    (int)BinaryOperatorKind.UInt32Subtraction,
                    (int)BinaryOperatorKind.UInt64Subtraction,
                    (int)BinaryOperatorKind.Float32Subtraction,
                    (int)BinaryOperatorKind.Float64Subtraction,
                    (int)BinaryOperatorKind.LiftedInt32Subtraction,
                    (int)BinaryOperatorKind.LiftedInt64Subtraction,
                    (int)BinaryOperatorKind.LiftedUInt32Subtraction,
                    (int)BinaryOperatorKind.LiftedUInt64Subtraction,
                    (int)BinaryOperatorKind.LiftedFloat32Subtraction,
                    (int)BinaryOperatorKind.LiftedFloat64Subtraction,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.Int32Division,
                    (int)BinaryOperatorKind.Int64Division,
                    (int)BinaryOperatorKind.UInt32Division,
                    (int)BinaryOperatorKind.UInt64Division,
                    (int)BinaryOperatorKind.Float32Division,
                    (int)BinaryOperatorKind.Float64Division,
                    (int)BinaryOperatorKind.LiftedInt32Division,
                    (int)BinaryOperatorKind.LiftedInt64Division,
                    (int)BinaryOperatorKind.LiftedUInt32Division,
                    (int)BinaryOperatorKind.LiftedUInt64Division,
                    (int)BinaryOperatorKind.LiftedFloat32Division,
                    (int)BinaryOperatorKind.LiftedFloat64Division,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.Int32Modulo,
                    (int)BinaryOperatorKind.Int64Modulo,
                    (int)BinaryOperatorKind.UInt32Modulo,
                    (int)BinaryOperatorKind.UInt64Modulo,
                    (int)BinaryOperatorKind.Float32Modulo,
                    (int)BinaryOperatorKind.Float64Modulo,
                    (int)BinaryOperatorKind.LiftedInt32Modulo,
                    (int)BinaryOperatorKind.LiftedInt64Modulo,
                    (int)BinaryOperatorKind.LiftedUInt32Modulo,
                    (int)BinaryOperatorKind.LiftedUInt64Modulo,
                    (int)BinaryOperatorKind.LiftedFloat32Modulo,
                    (int)BinaryOperatorKind.LiftedFloat64Modulo,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.Int32LeftShift,
                    (int)BinaryOperatorKind.Int64LeftShift,
                    (int)BinaryOperatorKind.UInt32LeftShift,
                    (int)BinaryOperatorKind.UInt64LeftShift,
                    (int)BinaryOperatorKind.LiftedInt32LeftShift,
                    (int)BinaryOperatorKind.LiftedInt64LeftShift,
                    (int)BinaryOperatorKind.LiftedUInt32LeftShift,
                    (int)BinaryOperatorKind.LiftedUInt64LeftShift,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.Int32RightShift,
                    (int)BinaryOperatorKind.Int64RightShift,
                    (int)BinaryOperatorKind.UInt32RightShift,
                    (int)BinaryOperatorKind.UInt64RightShift,
                    (int)BinaryOperatorKind.LiftedInt32RightShift,
                    (int)BinaryOperatorKind.LiftedInt64RightShift,
                    (int)BinaryOperatorKind.LiftedUInt32RightShift,
                    (int)BinaryOperatorKind.LiftedUInt64RightShift,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.Int32Equal,
                    (int)BinaryOperatorKind.Int64Equal,
                    (int)BinaryOperatorKind.UInt32Equal,
                    (int)BinaryOperatorKind.UInt64Equal,
                    (int)BinaryOperatorKind.Float32Equal,
                    (int)BinaryOperatorKind.Float64Equal,
                    (int)BinaryOperatorKind.BoolEqual,
                    (int)BinaryOperatorKind.ObjectEqual,
                    (int)BinaryOperatorKind.StringEqual,
                    (int)BinaryOperatorKind.CharEqual,
                    (int)BinaryOperatorKind.TypeEqual,
                    (int)BinaryOperatorKind.LiftedInt32Equal,
                    (int)BinaryOperatorKind.LiftedInt64Equal,
                    (int)BinaryOperatorKind.LiftedUInt32Equal,
                    (int)BinaryOperatorKind.LiftedUInt64Equal,
                    (int)BinaryOperatorKind.LiftedFloat32Equal,
                    (int)BinaryOperatorKind.LiftedFloat64Equal,
                    (int)BinaryOperatorKind.LiftedBoolEqual,
                    (int)BinaryOperatorKind.LiftedObjectEqual,
                    (int)BinaryOperatorKind.LiftedStringEqual,
                    (int)BinaryOperatorKind.LiftedCharEqual,
                    (int)BinaryOperatorKind.LiftedTypeEqual,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.Int32NotEqual,
                    (int)BinaryOperatorKind.Int64NotEqual,
                    (int)BinaryOperatorKind.UInt32NotEqual,
                    (int)BinaryOperatorKind.UInt64NotEqual,
                    (int)BinaryOperatorKind.Float32NotEqual,
                    (int)BinaryOperatorKind.Float64NotEqual,
                    (int)BinaryOperatorKind.BoolNotEqual,
                    (int)BinaryOperatorKind.ObjectNotEqual,
                    (int)BinaryOperatorKind.StringNotEqual,
                    (int)BinaryOperatorKind.CharNotEqual,
                    (int)BinaryOperatorKind.TypeNotEqual,
                    (int)BinaryOperatorKind.LiftedInt32NotEqual,
                    (int)BinaryOperatorKind.LiftedInt64NotEqual,
                    (int)BinaryOperatorKind.LiftedUInt32NotEqual,
                    (int)BinaryOperatorKind.LiftedUInt64NotEqual,
                    (int)BinaryOperatorKind.LiftedFloat32NotEqual,
                    (int)BinaryOperatorKind.LiftedFloat64NotEqual,
                    (int)BinaryOperatorKind.LiftedBoolNotEqual,
                    (int)BinaryOperatorKind.LiftedObjectNotEqual,
                    (int)BinaryOperatorKind.LiftedStringNotEqual,
                    (int)BinaryOperatorKind.LiftedCharNotEqual,
                    (int)BinaryOperatorKind.LiftedTypeNotEqual,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.Int32GreaterThan,
                    (int)BinaryOperatorKind.Int64GreaterThan,
                    (int)BinaryOperatorKind.UInt32GreaterThan,
                    (int)BinaryOperatorKind.UInt64GreaterThan,
                    (int)BinaryOperatorKind.Float32GreaterThan,
                    (int)BinaryOperatorKind.Float64GreaterThan,
                    (int)BinaryOperatorKind.LiftedInt32GreaterThan,
                    (int)BinaryOperatorKind.LiftedInt64GreaterThan,
                    (int)BinaryOperatorKind.LiftedUInt32GreaterThan,
                    (int)BinaryOperatorKind.LiftedUInt64GreaterThan,
                    (int)BinaryOperatorKind.LiftedFloat32GreaterThan,
                    (int)BinaryOperatorKind.LiftedFloat64GreaterThan,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.Int32LessThan,
                    (int)BinaryOperatorKind.Int64LessThan,
                    (int)BinaryOperatorKind.UInt32LessThan,
                    (int)BinaryOperatorKind.UInt64LessThan,
                    (int)BinaryOperatorKind.Float32LessThan,
                    (int)BinaryOperatorKind.Float64LessThan,
                    (int)BinaryOperatorKind.LiftedInt32LessThan,
                    (int)BinaryOperatorKind.LiftedInt64LessThan,
                    (int)BinaryOperatorKind.LiftedUInt32LessThan,
                    (int)BinaryOperatorKind.LiftedUInt64LessThan,
                    (int)BinaryOperatorKind.LiftedFloat32LessThan,
                    (int)BinaryOperatorKind.LiftedFloat64LessThan,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.Int32GreaterThanOrEqual,
                    (int)BinaryOperatorKind.Int64GreaterThanOrEqual,
                    (int)BinaryOperatorKind.UInt32GreaterThanOrEqual,
                    (int)BinaryOperatorKind.UInt64GreaterThanOrEqual,
                    (int)BinaryOperatorKind.Float32GreaterThanOrEqual,
                    (int)BinaryOperatorKind.Float64GreaterThanOrEqual,
                    (int)BinaryOperatorKind.LiftedInt32GreaterThanOrEqual,
                    (int)BinaryOperatorKind.LiftedInt64GreaterThanOrEqual,
                    (int)BinaryOperatorKind.LiftedUInt32GreaterThanOrEqual,
                    (int)BinaryOperatorKind.LiftedUInt64GreaterThanOrEqual,
                    (int)BinaryOperatorKind.LiftedFloat32GreaterThanOrEqual,
                    (int)BinaryOperatorKind.LiftedFloat64GreaterThanOrEqual,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.Int32LessThanOrEqual,
                    (int)BinaryOperatorKind.Int64LessThanOrEqual,
                    (int)BinaryOperatorKind.UInt32LessThanOrEqual,
                    (int)BinaryOperatorKind.UInt64LessThanOrEqual,
                    (int)BinaryOperatorKind.Float32LessThanOrEqual,
                    (int)BinaryOperatorKind.Float64LessThanOrEqual,
                    (int)BinaryOperatorKind.LiftedInt32LessThanOrEqual,
                    (int)BinaryOperatorKind.LiftedInt64LessThanOrEqual,
                    (int)BinaryOperatorKind.LiftedUInt32LessThanOrEqual,
                    (int)BinaryOperatorKind.LiftedUInt64LessThanOrEqual,
                    (int)BinaryOperatorKind.LiftedFloat32LessThanOrEqual,
                    (int)BinaryOperatorKind.LiftedFloat64LessThanOrEqual,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.Int32UnsignedRightShift,
                    (int)BinaryOperatorKind.Int64UnsignedRightShift,
                    (int)BinaryOperatorKind.UInt32UnsignedRightShift,
                    (int)BinaryOperatorKind.UInt64UnsignedRightShift,
                    (int)BinaryOperatorKind.LiftedInt32UnsignedRightShift,
                    (int)BinaryOperatorKind.LiftedInt64UnsignedRightShift,
                    (int)BinaryOperatorKind.LiftedUInt32UnsignedRightShift,
                    (int)BinaryOperatorKind.LiftedUInt64UnsignedRightShift,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.Int32And,
                    (int)BinaryOperatorKind.Int64And,
                    (int)BinaryOperatorKind.UInt32And,
                    (int)BinaryOperatorKind.UInt64And,
                    (int)BinaryOperatorKind.BoolAnd,
                    (int)BinaryOperatorKind.LiftedInt32And,
                    (int)BinaryOperatorKind.LiftedInt64And,
                    (int)BinaryOperatorKind.LiftedUInt32And,
                    (int)BinaryOperatorKind.LiftedUInt64And,
                    (int)BinaryOperatorKind.LiftedBoolAnd,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.Int32Or,
                    (int)BinaryOperatorKind.Int64Or,
                    (int)BinaryOperatorKind.UInt32Or,
                    (int)BinaryOperatorKind.UInt64Or,
                    (int)BinaryOperatorKind.BoolOr,
                    (int)BinaryOperatorKind.LiftedInt32Or,
                    (int)BinaryOperatorKind.LiftedInt64Or,
                    (int)BinaryOperatorKind.LiftedUInt32Or,
                    (int)BinaryOperatorKind.LiftedUInt64Or,
                    (int)BinaryOperatorKind.LiftedBoolOr,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.Int32Xor,
                    (int)BinaryOperatorKind.Int64Xor,
                    (int)BinaryOperatorKind.UInt32Xor,
                    (int)BinaryOperatorKind.UInt64Xor,
                    (int)BinaryOperatorKind.BoolXor,
                    (int)BinaryOperatorKind.LiftedInt32Xor,
                    (int)BinaryOperatorKind.LiftedInt64Xor,
                    (int)BinaryOperatorKind.LiftedUInt32Xor,
                    (int)BinaryOperatorKind.LiftedUInt64Xor,
                    (int)BinaryOperatorKind.LiftedBoolXor,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.Int64Power,
                    (int)BinaryOperatorKind.UInt64Power,
                    (int)BinaryOperatorKind.Float32Power,
                    (int)BinaryOperatorKind.Float64Power,
                    (int)BinaryOperatorKind.LiftedInt64Power,
                    (int)BinaryOperatorKind.LiftedUInt64Power,
                    (int)BinaryOperatorKind.LiftedFloat32Power,
                    (int)BinaryOperatorKind.LiftedFloat64Power,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.Int32Min,
                    (int)BinaryOperatorKind.Int64Min,
                    (int)BinaryOperatorKind.UInt32Min,
                    (int)BinaryOperatorKind.UInt64Min,
                    (int)BinaryOperatorKind.Float32Min,
                    (int)BinaryOperatorKind.Float64Min,
                    (int)BinaryOperatorKind.LiftedInt32Min,
                    (int)BinaryOperatorKind.LiftedInt64Min,
                    (int)BinaryOperatorKind.LiftedUInt32Min,
                    (int)BinaryOperatorKind.LiftedUInt64Min,
                    (int)BinaryOperatorKind.LiftedFloat32Min,
                    (int)BinaryOperatorKind.LiftedFloat64Min,
                ]),
                GetSignaturesFromBinaryOperatorKinds([
                    (int)BinaryOperatorKind.Int32Max,
                    (int)BinaryOperatorKind.Int64Max,
                    (int)BinaryOperatorKind.UInt32Max,
                    (int)BinaryOperatorKind.UInt64Max,
                    (int)BinaryOperatorKind.Float32Max,
                    (int)BinaryOperatorKind.Float64Max,
                    (int)BinaryOperatorKind.LiftedInt32Max,
                    (int)BinaryOperatorKind.LiftedInt64Max,
                    (int)BinaryOperatorKind.LiftedUInt32Max,
                    (int)BinaryOperatorKind.LiftedUInt64Max,
                    (int)BinaryOperatorKind.LiftedFloat32Max,
                    (int)BinaryOperatorKind.LiftedFloat64Max,
                ]),
            };

            var allOperators = new[] { nonConditionalOperators, conditionalOperators };
            Interlocked.CompareExchange(ref _builtInBinaryOperators, allOperators, null);
        }
    }

    private ImmutableArray<BinaryOperatorSignature> GetSignaturesFromBinaryOperatorKinds(int[] operatorKinds) {
        var builder = ArrayBuilder<BinaryOperatorSignature>.GetInstance();

        foreach (var kind in operatorKinds)
            builder.Add(GetSignature((BinaryOperatorKind)kind));

        return builder.ToImmutableAndFree();
    }

    private ImmutableArray<UnaryOperatorSignature> GetSignaturesFromUnaryOperatorKinds(int[] operatorKinds) {
        var builder = ArrayBuilder<UnaryOperatorSignature>.GetInstance();

        foreach (var kind in operatorKinds)
            builder.Add(GetSignature((UnaryOperatorKind)kind));

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

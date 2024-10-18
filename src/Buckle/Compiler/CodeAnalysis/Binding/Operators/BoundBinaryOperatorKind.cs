
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// All binary operator types.
/// </summary>
internal enum BoundBinaryOperatorKind : byte {
    Addition,
    Subtraction,
    Multiplication,
    Division,
    Power,
    LogicalAnd,
    LogicalOr,
    LogicalXor,
    LeftShift,
    RightShift,
    UnsignedRightShift,
    ConditionalAnd,
    ConditionalOr,
    EqualityEquals,
    EqualityNotEquals,
    LessThan,
    GreaterThan,
    LessOrEqual,
    GreatOrEqual,
    Is,
    Isnt,
    As,
    Modulo,
    NullCoalescing,
}

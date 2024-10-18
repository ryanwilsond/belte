
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// All unary operator types.
/// </summary>
internal enum BoundUnaryOperatorKind : byte {
    NumericalIdentity,
    NumericalNegation,
    BooleanNegation,
    BitwiseCompliment,
}

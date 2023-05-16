
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// All unary operator types.
/// </summary>
internal enum BoundUnaryOperatorKind {
    NumericalIdentity,
    NumericalNegation,
    BooleanNegation,
    BitwiseCompliment,
}

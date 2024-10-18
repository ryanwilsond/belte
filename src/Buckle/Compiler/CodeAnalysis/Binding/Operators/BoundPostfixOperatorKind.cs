
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// All postfix operator types.
/// </summary>
internal enum BoundPostfixOperatorKind : byte {
    Increment,
    Decrement,
    NullAssert,
}

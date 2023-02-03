
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound break statement, bound from a <see cref="BreakStatement" />.
/// Only used when transpiling, as most lowering is skipped and gotos are not created.
/// </summary>
internal sealed class BoundBreakStatement : BoundStatement {
    internal override BoundNodeKind kind => BoundNodeKind.BreakStatement;
}

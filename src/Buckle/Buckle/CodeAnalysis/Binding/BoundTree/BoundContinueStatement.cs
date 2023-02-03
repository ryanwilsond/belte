
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound continue statement, bound from a <see cref="ContinueStatement" />.
/// Only used when transpiling, as most lowering is skipped and gotos are not created.
/// </summary>
internal sealed class BoundContinueStatement : BoundStatement {
    internal override BoundNodeKind kind => BoundNodeKind.ContinueStatement;
}

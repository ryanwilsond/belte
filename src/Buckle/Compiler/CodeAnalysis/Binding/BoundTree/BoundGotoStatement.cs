
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound goto statement, produced by the <see cref="Lowering.Lowerer" />. No
/// <see cref="Syntax.InternalSyntax.Parser" /> equivalent.<br/>
/// E.g.
/// <code>
/// goto label
/// </code>
/// </summary>
internal sealed class BoundGotoStatement : BoundStatement {
    internal BoundGotoStatement(BoundLabel label) {
        this.label = label;
    }

    internal BoundLabel label { get; }

    internal override BoundNodeKind kind => BoundNodeKind.GotoStatement;
}

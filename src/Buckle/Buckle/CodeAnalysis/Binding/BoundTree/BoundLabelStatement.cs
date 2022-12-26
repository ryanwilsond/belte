
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound label statement, produced by <see cref="Lowerer" />. No <see cref="Parser" /> equivalent.<br/>
/// E.g.
/// <code>
/// label1:
/// </code>
/// </summary>
internal sealed class BoundLabelStatement : BoundStatement {
    internal BoundLabelStatement(BoundLabel label) {
        this.label = label;
    }

    internal BoundLabel label { get; }

    internal override BoundNodeKind kind => BoundNodeKind.LabelStatement;
}

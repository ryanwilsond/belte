
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound conditional goto statement, produced by the <see cref="Lowering.Lowerer" />. No
/// <see cref="Syntax.InternalSyntax.LanguageParser" /> equivalent.<br/>
/// E.g.
/// <code>
/// goto label if condition
/// </code>
/// </summary>
internal sealed class BoundConditionalGotoStatement : BoundStatement {
    internal BoundConditionalGotoStatement(BoundLabel label, BoundExpression condition, bool jumpIfTrue = true) {
        this.label = label;
        this.condition = condition;
        this.jumpIfTrue = jumpIfTrue;
    }

    internal BoundLabel label { get; }

    internal BoundExpression condition { get; }

    internal bool jumpIfTrue { get; }

    internal override BoundNodeKind kind => BoundNodeKind.ConditionalGotoStatement;
}


namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A base type for bound loop types (for, do, do while).
/// Uses labels for gotos as the Lowerer rewrites all control of flow to gotos.
/// </summary>
internal abstract class BoundLoopStatement : BoundStatement {
    protected BoundLoopStatement(BoundLabel breakLabel, BoundLabel continueLabel) {
        this.breakLabel = breakLabel;
        this.continueLabel = continueLabel;
    }

    internal BoundLabel breakLabel { get; }

    internal BoundLabel continueLabel { get; }
}

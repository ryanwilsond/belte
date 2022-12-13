
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A base type for bound loop types (<see cref="BoundForStatement" />, <see cref="BoundWhiteStatement" />, <see cref="BoundDoWhileStatement" />).
/// Uses labels for gotos as the <see cref="Lowerer" /> rewrites all control of flow to gotos.
/// </summary>
internal abstract class BoundLoopStatement : BoundStatement {
    protected BoundLoopStatement(BoundLabel breakLabel, BoundLabel continueLabel) {
        this.breakLabel = breakLabel;
        this.continueLabel = continueLabel;
    }

    internal BoundLabel breakLabel { get; }

    internal BoundLabel continueLabel { get; }
}

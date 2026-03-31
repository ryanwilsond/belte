using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundDagEvaluation {
    private protected BoundDagEvaluation(BoundKind kind, SyntaxNode syntax, BoundDagTemp boundDagTemp, bool hasErrors)
      : base(kind, syntax, boundDagTemp, hasErrors) { }
}

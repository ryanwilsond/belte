using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundDecisionDagNode {
    private protected BoundDecisionDagNode(BoundKind kind, SyntaxNode syntax, bool hasErrors)
      : base(kind, syntax, hasErrors) { }
}

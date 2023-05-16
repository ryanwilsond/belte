
namespace Buckle.CodeAnalysis.Syntax;

internal partial struct SyntaxTreeDiagnosticEnumerator {
    private struct NodeIteration {
        internal readonly GreenNode Node;
        internal int DiagnosticIndex;
        internal int SlotIndex;

        internal NodeIteration(GreenNode node) {
            this.Node = node;
            this.SlotIndex = -1;
            this.DiagnosticIndex = -1;
        }
    }
}

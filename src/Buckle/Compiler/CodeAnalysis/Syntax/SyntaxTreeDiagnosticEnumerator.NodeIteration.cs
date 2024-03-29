
namespace Buckle.CodeAnalysis.Syntax;

internal partial struct SyntaxTreeDiagnosticEnumerator {
    private struct NodeIteration {
        internal readonly GreenNode node;
        internal int diagnosticIndex;
        internal int slotIndex;

        internal NodeIteration(GreenNode node) {
            this.node = node;
            slotIndex = -1;
            diagnosticIndex = -1;
        }
    }
}

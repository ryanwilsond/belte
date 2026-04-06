using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class FlowLowerer {
    private abstract partial class DecisionDagRewriter {
        private abstract partial class ValueDispatchNode {
            private protected virtual int height => 1;

            internal readonly SyntaxNode syntax;

            internal ValueDispatchNode(SyntaxNode syntax) => this.syntax = syntax;
        }
    }
}

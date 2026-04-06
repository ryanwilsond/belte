using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class FlowLowerer {
    private abstract partial class DecisionDagRewriter {
        private abstract partial class ValueDispatchNode {
            internal sealed class LeafDispatchNode : ValueDispatchNode {
                internal readonly LabelSymbol label;
                internal LeafDispatchNode(SyntaxNode syntax, LabelSymbol label) : base(syntax) => this.label = label;

                public override string ToString() {
                    return "Leaf";
                }
            }
        }
    }
}

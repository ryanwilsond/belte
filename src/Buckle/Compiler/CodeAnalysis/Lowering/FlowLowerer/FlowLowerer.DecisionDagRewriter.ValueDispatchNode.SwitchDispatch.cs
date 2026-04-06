using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class FlowLowerer {
    private abstract partial class DecisionDagRewriter {
        private abstract partial class ValueDispatchNode {
            internal sealed class SwitchDispatch : ValueDispatchNode {
                internal readonly ImmutableArray<(ConstantValue value, LabelSymbol label)> cases;
                internal readonly LabelSymbol otherwise;
                internal SwitchDispatch(
                    SyntaxNode syntax,
                    ImmutableArray<(ConstantValue value, LabelSymbol label)> dispatches,
                    LabelSymbol otherwise) : base(syntax) {
                    cases = dispatches;
                    this.otherwise = otherwise;
                }

                public override string ToString() {
                    return "[" + string.Join(",", cases.Select(c => c.value)) + "]";
                }
            }
        }
    }
}

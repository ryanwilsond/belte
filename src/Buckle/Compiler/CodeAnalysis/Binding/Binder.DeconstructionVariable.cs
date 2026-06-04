using System.Diagnostics;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal partial class Binder {
    [DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
    internal sealed class DeconstructionVariable {
        internal readonly BoundExpression single;
        internal readonly ArrayBuilder2<DeconstructionVariable> nestedVariables;
        internal readonly BelteSyntaxNode syntax;

        internal DeconstructionVariable(BoundExpression variable, SyntaxNode syntax) {
            single = variable;
            nestedVariables = null;
            this.syntax = (BelteSyntaxNode)syntax;
        }

        internal DeconstructionVariable(ArrayBuilder2<DeconstructionVariable> variables, SyntaxNode syntax) {
            single = null;
            nestedVariables = variables;
            this.syntax = (BelteSyntaxNode)syntax;
        }

        internal static void FreeDeconstructionVariables(ArrayBuilder2<DeconstructionVariable> variables) {
            variables.FreeAll(v => v.nestedVariables);
        }

        private string GetDebuggerDisplay() {
            if (single is not null)
                return single.ToString();

            return $"Nested variables ({nestedVariables.Count})";
        }
    }
}

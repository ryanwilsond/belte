using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

internal static partial class ConstraintsHelpers {
    private sealed class ConstraintDisplayVisitor : BoundTreeRewriterWithStackGuard {
        private readonly TemplateMap _map;

        internal ConstraintDisplayVisitor(TemplateMap map) {
            _map = map;
        }

        internal override BoundNode VisitTypeExpression(BoundTypeExpression node) {
            if (node.type is TemplateParameterSymbol templateParameter) {
                var substituted = _map.SubstituteTemplateParameter(templateParameter);

                if (substituted.isConstant && substituted.constant is TemplateConstantValue t)
                    return t.expression;
                else if (substituted.isType)
                    return new BoundTypeExpression(node.syntax, null, null, substituted.type.type);
            }

            return node;
        }
    }
}

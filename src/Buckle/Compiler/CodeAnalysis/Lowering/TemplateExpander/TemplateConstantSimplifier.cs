using System.Diagnostics;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class TemplateConstantSimplifier : BoundTreeRewriterWithStackGuard {
    private readonly TemplateMap _templateMap;

    private TemplateConstantSimplifier(TemplateMap templateMap) {
        _templateMap = templateMap;
    }

    internal static BoundExpression Simplify(BoundExpression expression, TemplateMap templateMap) {
        // First rewrite the expression to replace template parameters with values
        // Then walk the expression to try and fold it

        var templateConstantSimplifier = new TemplateConstantSimplifier(templateMap);
        var newExpression = (BoundExpression)templateConstantSimplifier.Visit(expression);

        // TODO Use these diagnostics?
        return FoldExpression(newExpression, templateMap, BelteDiagnosticQueue.Discarded);
    }

    private static BoundExpression FoldExpression(
        BoundExpression expression,
        TemplateMap templateMap,
        BelteDiagnosticQueue diagnostics) {
        if (expression.constantValue is not null)
            return expression;

        switch (expression.kind) {
            case BoundKind.UnaryOperator:
                var unary = (BoundUnaryOperator)expression;

                return Rewrite(
                    ConstantFolding.FoldUnary(
                        FoldExpression(unary.operand, templateMap, diagnostics),
                        unary.operatorKind,
                        unary.Type()
                    ),
                    expression
                );
            case BoundKind.BinaryOperator:
                var binary = (BoundBinaryOperator)expression;

                return Rewrite(
                    ConstantFolding.FoldBinary(
                        FoldExpression(binary.left, templateMap, diagnostics).constantValue,
                        binary.left.type,
                        FoldExpression(binary.right, templateMap, diagnostics).constantValue,
                        binary.right.type,
                        binary.operatorKind,
                        binary.left.Type(),
                        binary.syntax.location,
                        diagnostics
                    ),
                    expression
                );
            case BoundKind.IsOperator:
                var isOperator = (BoundIsOperator)expression;

                return Rewrite(
                    ConstantFolding.FoldIs(
                        FoldExpression(isOperator.left, templateMap, diagnostics).constantValue,
                        FoldExpression(isOperator.right, templateMap, diagnostics).constantValue,
                        isOperator.isNot
                    ),
                    expression
                );
            case BoundKind.NullCoalescingOperator:
                var nullCoalescing = (BoundNullCoalescingOperator)expression;

                return Rewrite(
                    ConstantFolding.FoldNullCoalescing(
                        FoldExpression(nullCoalescing.left, templateMap, diagnostics).constantValue,
                        FoldExpression(nullCoalescing.right, templateMap, diagnostics).constantValue,
                        nullCoalescing.isPropagation,
                        nullCoalescing.Type()
                    ),
                    expression
                );
            case BoundKind.NullAssertOperator:
                var nullAssert = (BoundNullAssertOperator)expression;

                return Rewrite(
                    ConstantFolding.FoldNullAssert(
                        FoldExpression(nullAssert.operand, templateMap, diagnostics).constantValue
                    ),
                    expression
                );
            case BoundKind.CastExpression:
                var cast = (BoundCastExpression)expression;

                return Rewrite(
                    ConstantFolding.FoldCast(
                        FoldExpression(cast.operand, templateMap, diagnostics).constantValue,
                        expression.syntax.location,
                        cast.operand.type,
                        new TypeWithAnnotations(cast.type),
                        diagnostics
                    ),
                    expression
                );
            case BoundKind.ConditionalOperator:
                var conditional = (BoundConditionalOperator)expression;

                return Rewrite(
                    ConstantFolding.FoldConditional(
                        FoldExpression(conditional.condition, templateMap, diagnostics).constantValue,
                        FoldExpression(conditional.trueExpression, templateMap, diagnostics).constantValue,
                        FoldExpression(conditional.falseExpression, templateMap, diagnostics).constantValue,
                        conditional.Type()
                    ),
                    expression
                );
            case BoundKind.TypeExpression:
                var templateParameter = (TemplateParameterSymbol)expression.type;
                var typeOrConstant = templateMap.SubstituteTemplateParameter(templateParameter);

                if (typeOrConstant.isConstant && typeOrConstant.constant is TemplateConstantValue t)
                    return t.expression;
                else if (typeOrConstant.isConstant)
                    return new BoundLiteralExpression(expression.syntax, typeOrConstant.constant, expression.type);
                else
                    return new BoundTypeExpression(expression.syntax, null, null, typeOrConstant.type.type);
            default:
                return expression;
        }

        static BoundExpression Rewrite(ConstantValue constantValue, BoundExpression fallback) {
            if (constantValue is null)
                return fallback;

            return new BoundLiteralExpression(fallback.syntax, constantValue, fallback.type);
        }
    }

    internal override BoundNode VisitTypeExpression(BoundTypeExpression node) {
        if (node.type is TemplateParameterSymbol templateParameter) {
            var typeOrConstant = _templateMap.SubstituteTemplateParameter(templateParameter);

            if (typeOrConstant.isConstant) {
                Debug.Assert(templateParameter.underlyingType.specialType != SpecialType.Type);

                if (typeOrConstant.constant is TemplateConstantValue t)
                    return t.expression;

                return new BoundLiteralExpression(
                    node.syntax,
                    typeOrConstant.constant,
                    templateParameter.underlyingType.type
                );
            } else {
                return node.Update(null, null, typeOrConstant.type.type);
            }
        }

        return base.VisitTypeExpression(node);
    }
}

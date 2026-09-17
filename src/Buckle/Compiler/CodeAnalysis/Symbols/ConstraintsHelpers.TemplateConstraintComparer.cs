using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

internal static partial class ConstraintsHelpers {
    internal sealed class TemplateConstraintComparer {
        private readonly TemplateMap _templateMap;

        internal TemplateConstraintComparer(TemplateMap templateMap) {
            _templateMap = templateMap;
        }

        internal static bool ExpressionsEqual(BoundExpression left, BoundExpression right) {
            var comparer = new TemplateConstraintComparer(null);
            return comparer.Equals(left, right);
        }

        internal bool Equals(BoundExpression constraint, BoundExpression impliedConstraint) {
            return CompareExpression(constraint, impliedConstraint);
        }

        private bool CompareExpression(BoundExpression given, BoundExpression implied) {
            if (given.kind != implied.kind)
                return false;

            if (given.constantValue is not null && implied.constantValue is not null)
                return given.constantValue.Equals(implied.constantValue);

            switch (given.kind) {
                case BoundKind.UnaryOperator:
                    var unaryGiven = (BoundUnaryOperator)given;
                    var unaryImplied = (BoundUnaryOperator)implied;

                    if (unaryGiven.operatorKind != unaryImplied.operatorKind)
                        return false;

                    return CompareExpression(unaryGiven.operand, unaryImplied.operand);
                case BoundKind.BinaryOperator:
                    var binaryGiven = (BoundBinaryOperator)given;
                    var binaryImplied = (BoundBinaryOperator)implied;

                    if (binaryGiven.operatorKind != binaryImplied.operatorKind)
                        return false;

                    return CompareExpression(binaryGiven.left, binaryImplied.left) &&
                        CompareExpression(binaryGiven.right, binaryImplied.right);
                case BoundKind.IsOperator:
                    var isGiven = (BoundIsOperator)given;
                    var isImplied = (BoundIsOperator)implied;

                    if (isGiven.isNot != isImplied.isNot)
                        return false;

                    return CompareExpression(isGiven.left, isImplied.left) &&
                        CompareExpression(isGiven.right, isImplied.right);
                case BoundKind.NullCoalescingOperator:
                    var nullCoalescingGiven = (BoundNullCoalescingOperator)given;
                    var nullCoalescingImplied = (BoundNullCoalescingOperator)implied;

                    if (nullCoalescingGiven.isPropagation != nullCoalescingImplied.isPropagation)
                        return false;

                    return CompareExpression(nullCoalescingGiven.left, nullCoalescingImplied.left) &&
                        CompareExpression(nullCoalescingGiven.right, nullCoalescingImplied.right);
                case BoundKind.NullAssertOperator:
                    var nullAssertGiven = (BoundNullAssertOperator)given;
                    var nullAssertImplied = (BoundNullAssertOperator)implied;
                    return CompareExpression(nullAssertGiven.operand, nullAssertImplied.operand);
                case BoundKind.CastExpression:
                    var castGiven = (BoundCastExpression)given;
                    var castImplied = (BoundCastExpression)implied;

                    // TODO Should we instead compare the conversion itself?
                    if (!castGiven.type.Equals(castImplied.type, TypeCompareKind.ConsiderEverything))
                        return false;

                    return CompareExpression(castGiven.operand, castImplied.operand);
                case BoundKind.ConditionalOperator:
                    var conditionalGiven = (BoundConditionalOperator)given;
                    var conditionalImplied = (BoundConditionalOperator)implied;

                    if (conditionalGiven.isRef != conditionalImplied.isRef)
                        return false;

                    return CompareExpression(conditionalGiven.condition, conditionalImplied.condition) &&
                        CompareExpression(conditionalGiven.trueExpression, conditionalImplied.trueExpression) &&
                        CompareExpression(conditionalGiven.falseExpression, conditionalImplied.falseExpression);
                case BoundKind.TypeExpression:
                    var templateGiven = (TemplateParameterSymbol)given.type;
                    var templateImplied = (TemplateParameterSymbol)implied.type;

                    if (_templateMap is not null) {
                        return _templateMap.SubstituteTemplateParameter(templateGiven)
                            .IsSameAs(new TypeOrConstant(templateImplied));
                    } else {
                        return templateGiven.Equals(templateImplied);
                    }
                default:
                    return false;
            }
        }
    }
}

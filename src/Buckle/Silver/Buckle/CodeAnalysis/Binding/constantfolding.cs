
namespace Buckle.CodeAnalysis.Binding {
    internal static class ConstantFolding {
        public static BoundConstant ComputeConstant(
            BoundExpression left, BoundBinaryOperator op, BoundExpression right) {
            var leftConstant = left.constantValue;
            var rightConstant = right.constantValue;

            if (op.opType == BoundBinaryOperatorType.LogicalAnd) {
                if (leftConstant != null && !(bool)leftConstant.value)
                    return new BoundConstant(false);
            }

            if (leftConstant == null || rightConstant == null)
                return null;

            // * compute
        }

        public static BoundConstant ComputeConstant(BoundUnaryOperator op, BoundExpression operand) {
            if (operand.constantValue != null && operand.constantValue.value is int value) {
                switch (op.opType) {
                    case BoundUnaryOperatorType.NumericalIdentity:
                        return new BoundConstant((int)operand.constantValue.value);
                    case BoundUnaryOperatorType.NumericalNegation:
                        return new BoundConstant(-(int)operand.constantValue.value);
                    case BoundUnaryOperatorType.BooleanNegation:
                        return new BoundConstant(!(bool)operand.constantValue.value);
                    case BoundUnaryOperatorType.BitwiseCompliment:
                        return new BoundConstant(~(int)operand.constantValue.value);
                }
            }

            return null;
        }
    }
}

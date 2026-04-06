using System;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class FlowLowerer {
    private abstract partial class DecisionDagRewriter {
        private abstract partial class ValueDispatchNode {
            internal sealed class RelationalDispatch : ValueDispatchNode {
                private int _height;

                private protected override int height => _height;

                internal readonly ConstantValue value;
                internal readonly BinaryOperatorKind @operator;

                private ValueDispatchNode left { get; set; }

                private ValueDispatchNode right { get; set; }

                private RelationalDispatch(
                    SyntaxNode syntax,
                    ConstantValue value,
                    BinaryOperatorKind op,
                    ValueDispatchNode left,
                    ValueDispatchNode right) : base(syntax) {
                    this.value = value;
                    @operator = op;
                    WithLeftAndRight(left, right);
                }

                internal ValueDispatchNode whenTrue => IsReversed(@operator) ? right : left;

                internal ValueDispatchNode whenFalse => IsReversed(@operator) ? left : right;

                public override string ToString() {
                    return $"RelationalDispatch.{_height}({left} {@operator.Operator()} {value} {right})";
                }

                private static bool IsReversed(BinaryOperatorKind op) {
                    return op.Operator() switch {
                        BinaryOperatorKind.GreaterThan => true,
                        BinaryOperatorKind.GreaterThanOrEqual => true,
                        _ => false
                    };
                }

                private RelationalDispatch WithLeftAndRight(ValueDispatchNode left, ValueDispatchNode right) {
                    var l = left.height;
                    var r = right.height;
                    this.left = left;
                    this.right = right;
                    _height = Math.Max(l, r) + 1;
                    return this;
                }

                internal RelationalDispatch WithTrueAndFalseChildren(ValueDispatchNode whenTrue, ValueDispatchNode whenFalse) {
                    if (whenTrue == this.whenTrue && whenFalse == this.whenFalse)
                        return this;

                    var (left, right) = IsReversed(@operator) ? (whenFalse, whenTrue) : (whenTrue, whenFalse);
                    return WithLeftAndRight(left, right);
                }

                internal static ValueDispatchNode CreateBalanced(
                    SyntaxNode syntax,
                    ConstantValue value,
                    BinaryOperatorKind op,
                    ValueDispatchNode whenTrue,
                    ValueDispatchNode whenFalse) {
                    var (left, right) = IsReversed(op) ? (whenFalse, whenTrue) : (whenTrue, whenFalse);
                    return CreateBalancedCore(syntax, value, op, left: left, right: right);
                }

                private static ValueDispatchNode CreateBalancedCore(
                    SyntaxNode syntax,
                    ConstantValue value,
                    BinaryOperatorKind op,
                    ValueDispatchNode left,
                    ValueDispatchNode right) {
                    if (left.height > (right.height + 1)) {
                        var l = (RelationalDispatch)left;
                        var newRight = CreateBalancedCore(syntax, value, op, left: l.right, right: right);
                        (syntax, value, op, left, right) = (l.syntax, l.value, l.@operator, l.left, newRight);
                    } else if (right.height > (left.height + 1)) {
                        var r = (RelationalDispatch)right;
                        var newLeft = CreateBalancedCore(syntax, value, op, left: left, right: r.left);
                        (syntax, value, op, left, right) = (r.syntax, r.value, r.@operator, newLeft, r.right);
                    }

                    if (left.height == right.height + 2) {
                        var leftDispatch = (RelationalDispatch)left;
                        if (leftDispatch.left.height == right.height) {
                            var x = leftDispatch;
                            var A = x.left;
                            var y = (RelationalDispatch)x.right;
                            var B = y.left;
                            var C = y.right;
                            var D = right;
                            return y.WithLeftAndRight(x.WithLeftAndRight(A, B), new RelationalDispatch(syntax, value, op, C, D));
                        } else {
                            var y = leftDispatch;
                            var x = y.left;
                            var C = y.right;
                            var D = right;
                            return y.WithLeftAndRight(x, new RelationalDispatch(syntax, value, op, C, D));
                        }
                    } else if (right.height == left.height + 2) {
                        var rightDispatch = (RelationalDispatch)right;

                        if (rightDispatch.right.height == left.height) {
                            var A = left;
                            var z = rightDispatch;
                            var y = (RelationalDispatch)z.left;
                            var B = y.left;
                            var C = y.right;
                            var D = z.right;
                            return y.WithLeftAndRight(new RelationalDispatch(syntax, value, op, A, B), z.WithLeftAndRight(C, D));
                        } else {
                            var A = left;
                            var y = rightDispatch;
                            var B = y.left;
                            var z = y.right;
                            return y.WithLeftAndRight(new RelationalDispatch(syntax, value, op, A, B), z);
                        }
                    }

                    return new RelationalDispatch(syntax, value, op, left: left, right: right);
                }
            }
        }
    }
}

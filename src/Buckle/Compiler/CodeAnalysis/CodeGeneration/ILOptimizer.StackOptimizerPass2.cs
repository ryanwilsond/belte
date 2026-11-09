using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal sealed partial class ILOptimizer {
    internal sealed class StackOptimizerPass2 : BoundTreeRewriterWithStackGuard {
        private readonly Dictionary<DataContainerSymbol, LocalDefUseInfo> _info;

        private int _nodeCounter;

        private StackOptimizerPass2(Dictionary<DataContainerSymbol, LocalDefUseInfo> info) {
            _info = info;
        }

        internal static BoundBlockStatement Rewrite(
            BoundBlockStatement src,
            Dictionary<DataContainerSymbol, LocalDefUseInfo> info) {
            var scheduler = new StackOptimizerPass2(info);
            return (BoundBlockStatement)scheduler.Visit(src);
        }

        internal override BoundNode Visit(BoundNode node) {
            BoundNode result;

            if (node is BoundExpression asExpression && asExpression.constantValue is not null)
                result = node;
            else
                result = base.Visit(node);

            _nodeCounter++;
            return result;
        }

        internal override BoundNode VisitBinaryOperator(BoundBinaryOperator node) {
            var child = node.left;

            if (child.kind != BoundKind.BinaryOperator || child.constantValue is not null)
                return base.VisitBinaryOperator(node);

            var stack = ArrayBuilder<BoundBinaryOperator>.GetInstance();
            stack.Push(node);

            var binary = (BoundBinaryOperator)child;

            while (true) {
                stack.Push(binary);
                child = binary.left;

                if (child.kind != BoundKind.BinaryOperator || child.constantValue is not null)
                    break;

                binary = (BoundBinaryOperator)child;
            }

            var left = (BoundExpression)Visit(child);

            while (true) {
                binary = stack.Pop();
                var right = (BoundExpression)Visit(binary.right);
                var type = VisitType(binary.type);

                left = binary.Update(
                    left,
                    right,
                    binary.operatorKind,
                    binary.method,
                    binary.constantValue,
                    type
                );

                if (stack.Count == 0)
                    break;

                _nodeCounter++;
            }

            Debug.Assert((object)binary == node);
            stack.Free();

            return left;
        }

        private static bool IsLastAccess(LocalDefUseInfo locInfo, int counter) {
            return locInfo.localDefs.Any((d) => counter == d.start && counter == d.end);
        }

        internal override BoundNode VisitDataContainerExpression(BoundDataContainerExpression node) {
            if (!_info.TryGetValue(node.dataContainer, out var locInfo))
                return base.VisitDataContainerExpression(node);

            if (!IsLastAccess(locInfo, _nodeCounter))
                return new BoundDup(node.syntax, node.dataContainer.refKind, node.type);

            return base.VisitDataContainerExpression(node);
        }

        internal override BoundNode VisitObjectCreationExpression(BoundObjectCreationExpression node) {
            var arguments = VisitList(node.arguments);
            var type = VisitType(node.type);

            return node.Update(
                node.constructor,
                arguments,
                node.argumentRefKinds,
                node.argsToParams,
                node.defaultArguments,
                node.wasTargetTyped,
                type
            );
        }

        internal override BoundNode VisitLocalDeclarationStatement(BoundLocalDeclarationStatement node) {
            if (node.declaration.initializer is not null) {
                var local = node.declaration.dataContainer;
                var initializer = node.declaration.initializer;

                var assignment = BoundFactory.Assignment(
                    null,
                    new BoundDataContainerExpression(null, local, null, local.type),
                    initializer,
                    local.isRef,
                    local.type
                );

                var rewritten = Visit(assignment);

                if (rewritten is BoundAssignmentOperator rewrittenAssignment) {
                    if ((object)rewrittenAssignment != assignment) {
                        return new BoundLocalDeclarationStatement(
                            node.syntax,
                            new BoundDataContainerDeclaration(
                                node.declaration.syntax,
                                local,
                                rewrittenAssignment.right
                            )
                        );
                    }

                    return node;
                } else {
                    Debug.Assert(rewritten.kind == assignment.right.kind);
                    return new BoundExpressionStatement(node.syntax, (BoundExpression)rewritten);
                }
            }

            // Pretend this node doesn't exist
            _nodeCounter--;
            return node;
        }

        internal override BoundNode VisitAssignmentOperator(BoundAssignmentOperator node) {
            var dataContainer = (node.left as BoundDataContainerExpression)?.dataContainer ??
                (node.left as BoundStackSlotExpression)?.symbol as DataContainerSymbol;

            if (dataContainer is null || !_info.TryGetValue(dataContainer, out var locInfo))
                return base.VisitAssignmentOperator(node);

            var isIndirectLocalStore = dataContainer.refKind != RefKind.None && !node.isRef;

            if (isIndirectLocalStore)
                return base.VisitAssignmentOperator(node);

            _nodeCounter++;

            var right = (BoundExpression)Visit(node.right);

            // TODO Use ArrayBuilder2 instead of Linq?
            Debug.Assert(locInfo.localDefs.Any((d) => _nodeCounter == d.start && _nodeCounter <= d.end));
            var isLast = IsLastAccess(locInfo, _nodeCounter);

            if (isLast) {
                if (node.isRef &&
                    // TODO CompilerGenerated check?
                    // !node.wasCompilerGenerated &&
                    dataContainer.refKind == RefKind.Ref &&
                    right is BoundArrayAccessExpression arrayAccess &&
                    !arrayAccess.type.isValueType) {
                    // TODO
                    throw ExceptionUtilities.Unreachable();
                    // return new BoundRefArrayAccess(arrayAccess.Syntax, arrayAccess);
                }

                return right;
            } else {
                return node.Update(node.left, right, node.isRef, node.type);
            }
        }

        internal override BoundNode VisitCallExpression(BoundCallExpression node) {
            if (node.receiver is BoundCallExpression receiver1) {
                var calls = ArrayBuilder<BoundCallExpression>.GetInstance();

                calls.Push(node);

                node = receiver1;

                while (node.receiver is BoundCallExpression receiver2) {
                    calls.Push(node);
                    node = receiver2;
                }

                var rewrittenReceiver = VisitReceiver(node);

                while (true) {
                    rewrittenReceiver = VisitArgumentsAndUpdateCall(node, rewrittenReceiver);

                    if (!calls.TryPop(out node))
                        break;

                    _nodeCounter++;
                }

                calls.Free();

                return rewrittenReceiver;
            } else {
                var rewrittenReceiver = VisitReceiver(node);
                return VisitArgumentsAndUpdateCall(node, rewrittenReceiver);
            }

            BoundExpression VisitReceiver(BoundCallExpression node) {
                var receiverOpt = node.receiver;

                if (node.method.requiresInstanceReceiver) {
                    receiverOpt = (BoundExpression)Visit(receiverOpt);
                } else {
                    _nodeCounter++;

                    if (receiverOpt is
                        BoundTypeExpression { alias: null, type.typeKind: TypeKind.TemplateParameter } typeExpression) {
                        receiverOpt = typeExpression.Update(
                            typeWithAnnotations: typeExpression.typeWithAnnotations,
                            alias: null,
                            type: VisitType(typeExpression.type)
                        );
                    } else if (receiverOpt is not null) {
                        throw ExceptionUtilities.Unreachable();
                    }
                }

                return receiverOpt;
            }

            BoundExpression VisitArgumentsAndUpdateCall(BoundCallExpression node, BoundExpression receiverOpt) {
                var arguments = VisitList(node.arguments);
                var type = VisitType(node.type);

                return node.Update(
                    receiverOpt,
                    node.method,
                    arguments,
                    node.argumentRefKinds,
                    node.defaultArguments,
                    node.resultKind,
                    type
                );
            }
        }
    }
}

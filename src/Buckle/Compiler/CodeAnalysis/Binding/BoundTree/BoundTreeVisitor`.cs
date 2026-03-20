
namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class BoundTreeVisitor<A, R> {
    private protected BoundTreeVisitor() { }

    internal virtual R Visit(BoundNode node, A arg) {
        if (node is null)
            return default;

        return node.kind switch {
            BoundKind.NamespaceExpression => VisitNamespaceExpression(node as BoundNamespaceExpression, arg),
            BoundKind.TypeExpression => VisitTypeExpression(node as BoundTypeExpression, arg),
            BoundKind.UnaryOperator => VisitUnaryOperator(node as BoundUnaryOperator, arg),
            BoundKind.IncrementOperator => VisitIncrementOperator(node as BoundIncrementOperator, arg),
            BoundKind.BinaryOperator => VisitBinaryOperator(node as BoundBinaryOperator, arg),
            BoundKind.CompoundAssignmentOperator => VisitCompoundAssignmentOperator(node as BoundCompoundAssignmentOperator, arg),
            BoundKind.AssignmentOperator => VisitAssignmentOperator(node as BoundAssignmentOperator, arg),
            BoundKind.NullCoalescingOperator => VisitNullCoalescingOperator(node as BoundNullCoalescingOperator, arg),
            BoundKind.ConditionalOperator => VisitConditionalOperator(node as BoundConditionalOperator, arg),
            BoundKind.ArrayAccessExpression => VisitArrayAccessExpression(node as BoundArrayAccessExpression, arg),
            BoundKind.TypeOfExpression => VisitTypeOfExpression(node as BoundTypeOfExpression, arg),
            BoundKind.IsOperator => VisitIsOperator(node as BoundIsOperator, arg),
            BoundKind.AsOperator => VisitAsOperator(node as BoundAsOperator, arg),
            BoundKind.CastExpression => VisitCastExpression(node as BoundCastExpression, arg),
            BoundKind.BlockStatement => VisitBlockStatement(node as BoundBlockStatement, arg),
            BoundKind.LocalDeclarationStatement => VisitLocalDeclarationStatement(node as BoundLocalDeclarationStatement, arg),
            BoundKind.NopStatement => VisitNopStatement(node as BoundNopStatement, arg),
            BoundKind.ReturnStatement => VisitReturnStatement(node as BoundReturnStatement, arg),
            BoundKind.ThrowExpression => VisitThrowExpression(node as BoundThrowExpression, arg),
            BoundKind.ExpressionStatement => VisitExpressionStatement(node as BoundExpressionStatement, arg),
            BoundKind.BreakStatement => VisitBreakStatement(node as BoundBreakStatement, arg),
            BoundKind.ContinueStatement => VisitContinueStatement(node as BoundContinueStatement, arg),
            BoundKind.IfStatement => VisitIfStatement(node as BoundIfStatement, arg),
            BoundKind.TryStatement => VisitTryStatement(node as BoundTryStatement, arg),
            BoundKind.LiteralExpression => VisitLiteralExpression(node as BoundLiteralExpression, arg),
            BoundKind.ThisExpression => VisitThisExpression(node as BoundThisExpression, arg),
            BoundKind.DataContainerExpression => VisitDataContainerExpression(node as BoundDataContainerExpression, arg),
            BoundKind.ParameterExpression => VisitParameterExpression(node as BoundParameterExpression, arg),
            BoundKind.LabelStatement => VisitLabelStatement(node as BoundLabelStatement, arg),
            BoundKind.GotoStatement => VisitGotoStatement(node as BoundGotoStatement, arg),
            BoundKind.ConditionalGotoStatement => VisitConditionalGotoStatement(node as BoundConditionalGotoStatement, arg),
            BoundKind.CallExpression => VisitCallExpression(node as BoundCallExpression, arg),
            BoundKind.ObjectCreationExpression => VisitObjectCreationExpression(node as BoundObjectCreationExpression, arg),
            BoundKind.FieldAccessExpression => VisitFieldAccessExpression(node as BoundFieldAccessExpression, arg),
            _ => VisitInternal(node, arg),
        };
    }

    internal virtual R DefaultVisit(BoundNode node, A arg) {
        return default;
    }
}

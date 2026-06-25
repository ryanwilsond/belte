using Buckle.CodeAnalysis.Binding;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Lowering;

// TODO Many more warnings we could check for here
internal sealed class DiagnosticPass : BoundTreeWalkerWithStackGuard {
    private readonly BelteDiagnosticQueue _diagnostics;

    private DiagnosticPass(BelteDiagnosticQueue diagnostics) {
        _diagnostics = diagnostics;
    }

    internal static void ReportDiagnostics(BoundNode node, BelteDiagnosticQueue diagnostics) {
        try {
            var diagnosticPass = new DiagnosticPass(diagnostics);
            diagnosticPass.Visit(node);
        } catch (CancelledByStackGuardException ex) {
            ex.AddAnError(diagnostics);
        }
    }

    internal override BoundNode VisitAssignmentOperator(BoundAssignmentOperator node) {
        CheckForAssignmentToSelf(node);
        return base.VisitAssignmentOperator(node);
    }

    private bool CheckForAssignmentToSelf(BoundAssignmentOperator node) {
        if (!node.hasAnyErrors && IsSameLocalOrField(node.left, node.right)) {
            _diagnostics.Push(Warning.AssignmentToSelf(node.syntax.location));
            return true;
        }

        return false;
    }

    private static BoundExpression StripImplicitCasts(BoundExpression expr) {
        var current = expr;

        while (true) {
            if (current is not BoundCastExpression conversion || !conversion.conversion.kind.IsImplicitCast())
                return current;

            current = conversion.operand;
        }
    }

    private static bool IsSameLocalOrField(BoundExpression expr1, BoundExpression expr2) {
        if (expr1 is null && expr2 is null)
            return true;

        if (expr1 is null || expr2 is null)
            return false;

        if (expr1.hasAnyErrors || expr2.hasAnyErrors)
            return false;

        expr1 = StripImplicitCasts(expr1);
        expr2 = StripImplicitCasts(expr2);

        if (expr1.kind != expr2.kind)
            return false;

        switch (expr1.kind) {
            case BoundKind.DataContainerExpression:
                var local1 = (BoundDataContainerExpression)expr1;
                var local2 = (BoundDataContainerExpression)expr2;
                return local1.dataContainer == local2.dataContainer;
            case BoundKind.FieldAccessExpression:
                var field1 = (BoundFieldAccessExpression)expr1;
                var field2 = (BoundFieldAccessExpression)expr2;
                return field1.field == field2.field &&
                    (field1.field.isStatic || IsSameLocalOrField(field1.receiver, field2.receiver));
            case BoundKind.ParameterExpression:
                var param1 = (BoundParameterExpression)expr1;
                var param2 = (BoundParameterExpression)expr2;
                return param1.parameter == param2.parameter;
            case BoundKind.ThisExpression:
                return true;
            default:
                return false;
        }
    }
}

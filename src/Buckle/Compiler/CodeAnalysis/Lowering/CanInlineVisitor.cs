using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class CanInlineVisitor : BoundTreeWalkerWithStackGuard {
    private bool _accessesPrivateMembers;
    private Symbol _firstPrivateMember;

    private CanInlineVisitor() { }

    internal static bool MethodBodyIsInlineable(
        MethodSymbol method,
        BoundBlockStatement body,
        out InlineInformation inlineInformation) {
        // TODO We are extremely conservative here for now

        if (body.statements.Length > 1) {
            inlineInformation = default;
            return false;
        }

        var visitor = new CanInlineVisitor();
        visitor.Visit(body);

        inlineInformation = new InlineInformation(
            true,
            visitor._accessesPrivateMembers,
            visitor._firstPrivateMember,
            body
        );

        return true;
    }

    internal override BoundNode DefaultVisit(BoundNode node) {
        if (_accessesPrivateMembers)
            return null;

        return base.DefaultVisit(node);
    }

    internal override BoundNode VisitFieldAccessExpression(BoundFieldAccessExpression node) {
        if (node.field.declaredAccessibility == Accessibility.Private) {
            _accessesPrivateMembers = true;
            _firstPrivateMember ??= node.field;
            return null;
        }

        return base.VisitFieldAccessExpression(node);
    }

    internal override BoundNode VisitCallExpression(BoundCallExpression node) {
        if (node.method.declaredAccessibility == Accessibility.Private) {
            _accessesPrivateMembers = true;
            _firstPrivateMember ??= node.method;
            return null;
        }

        return base.VisitCallExpression(node);
    }
}

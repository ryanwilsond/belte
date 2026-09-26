using System.Collections.Concurrent;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class MethodInliner : BoundTreeRewriterWithStackGuard {
    private readonly MethodSymbol _method;
    private readonly ConcurrentDictionary<MethodSymbol, CanInlineVisitor.InlineInformation> _inlineInformation;

    private MethodInliner(
        MethodSymbol method,
        ConcurrentDictionary<MethodSymbol, CanInlineVisitor.InlineInformation> inlineInformation) {
        _method = method;
        _inlineInformation = inlineInformation;
    }

    internal static void Visit(
        MethodSymbol method,
        BoundBlockStatement body,
        ConcurrentDictionary<MethodSymbol, CanInlineVisitor.InlineInformation> inlineInformation,
        ConcurrentDictionary<MethodSymbol, BoundBlockStatement> methodBodyMap) {
        var inliner = new MethodInliner(method, inlineInformation);

        var newBody = (BoundBlockStatement)inliner.Visit(body);

        if (newBody != body)
            methodBodyMap[method] = newBody;
    }

    internal override BoundNode VisitCallExpression(BoundCallExpression node) {
        var receiver = (BoundExpression)Visit(node.receiver);
        var arguments = VisitList(node.arguments);
        var type = VisitType(node.type);

        if (_inlineInformation.TryGetValue(node.method, out var inlineInformation)) {
            if (inlineInformation.accessesPrivateMembers &&
                !AccessCheck.IsSymbolAccessible(inlineInformation.firstPrivateMember, _method)) {
                goto Fail;
            }

            if (!node.argumentRefKinds.IsDefaultOrEmpty)
                goto Fail;

            // TODO Insert body and adjust receivers, arguments, etc
        }

Fail:
        return node.Update(
            receiver,
            node.method,
            arguments,
            node.argumentRefKinds,
            node.defaultArguments,
            node.resultKind,
            type
        );
    }
}

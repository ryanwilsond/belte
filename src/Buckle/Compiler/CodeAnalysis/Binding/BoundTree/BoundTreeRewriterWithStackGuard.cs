
namespace Buckle.CodeAnalysis.Binding;

internal abstract class BoundTreeRewriterWithStackGuard : BoundTreeRewriter {
    private int _recursionDepthInternal;

    private protected BoundTreeRewriterWithStackGuard() { }

    private protected BoundTreeRewriterWithStackGuard(int recursionDepth) {
        _recursionDepthInternal = recursionDepth;
    }

    private protected int _recursionDepth => _recursionDepthInternal;

    internal override BoundNode Visit(BoundNode node) {
        if (node is BoundExpression expression)
            return VisitExpressionWithStackGuard(ref _recursionDepthInternal, expression);

        return base.Visit(node);
    }

    private protected BoundExpression VisitExpressionWithStackGuard(BoundExpression node) {
        return VisitExpressionWithStackGuard(ref _recursionDepthInternal, node);
    }

    private protected sealed override BoundExpression VisitExpressionWithoutStackGuard(BoundExpression node) {
        return (BoundExpression)base.Visit(node);
    }
}

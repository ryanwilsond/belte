namespace Buckle.CodeAnalysis.Binding;

internal abstract class BoundTreeWalkerWithStackGuard : BoundTreeWalker
{
    private protected int _recursionDepth;

    private protected BoundTreeWalkerWithStackGuard() { }

    private protected BoundTreeWalkerWithStackGuard(int recursionDepth)
    {
        _recursionDepth = recursionDepth;
    }

    internal override BoundNode Visit(BoundNode node)
    {
        if (node is BoundExpression expression)
        {
            return VisitExpressionWithStackGuard(ref _recursionDepth, expression);
        }

        return base.Visit(node);
    }

    private protected BoundExpression VisitExpressionWithStackGuard(BoundExpression node)
    {
        return VisitExpressionWithStackGuard(ref _recursionDepth, node);
    }

    private protected sealed override BoundExpression VisitExpressionWithoutStackGuard(BoundExpression node)
    {
        return (BoundExpression)base.Visit(node);
    }
}

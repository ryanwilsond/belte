using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundDagTemp {
    internal bool isOriginalInput => source is null;

    internal static BoundDagTemp ForOriginalInput(SyntaxNode syntax, TypeSymbol type) {
        return new BoundDagTemp(syntax, type, source: null, 0);
    }

    internal static BoundDagTemp ForOriginalInput(BoundExpression expr) {
        return new BoundDagTemp(expr.syntax, expr.type, source: null, 0);
    }
}

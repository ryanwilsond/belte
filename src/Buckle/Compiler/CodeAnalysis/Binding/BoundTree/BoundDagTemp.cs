using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundDagTemp {
    internal bool isOriginalInput => source is null;

    internal static BoundDagTemp ForOriginalInput(SyntaxNode syntax, TypeSymbol type) {
        return new BoundDagTemp(syntax, type, source: null, 0);
    }

    internal static BoundDagTemp ForOriginalInput(BoundExpression expr) {
        return new BoundDagTemp(expr.syntax, expr.type, source: null, 0);
    }

    public override bool Equals(object? obj) {
        return obj is BoundDagTemp other && Equals(other);
    }

    internal bool Equals(BoundDagTemp other) {
        return type.Equals(other.type, TypeCompareKind.AllIgnoreOptions) &&
            Equals(source, other.source) &&
            index == other.index;
    }

    internal bool IsEquivalentTo(BoundDagTemp other) {
        return type.Equals(other.type, TypeCompareKind.AllIgnoreOptions) && index == other.index;
    }

    public override int GetHashCode() {
        return Hash.Combine(type.GetHashCode(), Hash.Combine(source?.GetHashCode() ?? 0, index));
    }
}

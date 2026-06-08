using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceTokenSymbol : TokenSymbol {
    private readonly SyntaxToken _identifierToken;
    private string _lazyName;

    internal SourceTokenSymbol(Symbol containingSymbol, SyntaxToken identifierToken, Binder scopeBinder) {
        _identifierToken = identifierToken;
        this.containingSymbol = containingSymbol;
        this.scopeBinder = scopeBinder;
    }

    public override string name => _lazyName ??= MakeTokenName();

    private string MakeTokenName() {
        if (_identifierToken.kind != SyntaxKind.None)
            return _identifierToken.text;

        return "";
    }

    internal override ImmutableArray<TextLocation> locations
        => _identifierToken.parent is null
            ? []
            : [_identifierToken.location];

    internal override TextLocation location => locations.FirstOrDefault();

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences {
        get {
            BelteSyntaxNode node = null;

            if (_identifierToken.parent is not null)
                node = _identifierToken.parent.FirstAncestorOrSelf<ReversibleExpressionSyntax>();

            return node is null ? [] : [new SyntaxReference(node)];
        }
    }

    internal Binder scopeBinder { get; }

    internal override SyntaxReference syntaxReference => declaringSyntaxReferences.FirstOrDefault();

    internal override Symbol containingSymbol { get; }

    internal override SyntaxToken identifierToken => _identifierToken;

    internal override bool Equals(Symbol obj, TypeCompareKind compareKind) {
        if (obj == (object)this)
            return true;

        return obj is SourceTokenSymbol symbol
            && symbol._identifierToken.kind != SyntaxKind.None
            && symbol._identifierToken.Equals(_identifierToken)
            && symbol.containingSymbol.Equals(containingSymbol, compareKind);
    }

    public override int GetHashCode() {
        return _identifierToken.GetHashCode();
    }
}

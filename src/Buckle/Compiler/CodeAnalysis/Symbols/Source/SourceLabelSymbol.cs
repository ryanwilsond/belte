using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceLabelSymbol : LabelSymbol {
    private readonly MethodSymbol _containingMethod;
    private readonly SyntaxNodeOrToken _identifierNodeOrToken;
    private string _lazyName;

    internal SourceLabelSymbol(
        MethodSymbol containingMethod,
        SyntaxNodeOrToken identifierNodeOrToken,
        ConstantValue switchCaseLabelConstant = null) {
        _containingMethod = containingMethod;
        _identifierNodeOrToken = identifierNodeOrToken;
        this.switchCaseLabelConstant = switchCaseLabelConstant;
    }

    internal SourceLabelSymbol(
        MethodSymbol containingMethod,
        ConstantValue switchCaseLabelConstant) {
        _containingMethod = containingMethod;
        _identifierNodeOrToken = default(SyntaxToken);
        this.switchCaseLabelConstant = switchCaseLabelConstant;
    }

    public override string name => _lazyName ??= MakeLabelName();

    private string MakeLabelName() {
        var node = _identifierNodeOrToken.AsNode();

        if (node is not null) {
            if (node.kind == SyntaxKind.DefaultSwitchLabel)
                return ((DefaultSwitchLabelSyntax)node).keyword.ToString();

            return node.ToString();
        }

        var tk = _identifierNodeOrToken.AsToken();

        if (tk.kind != SyntaxKind.None)
            return tk.text;

        return switchCaseLabelConstant?.ToString() ?? "";
    }

    internal override ImmutableArray<TextLocation> locations
        => _identifierNodeOrToken.isToken && _identifierNodeOrToken.parent is null
            ? []
            : [_identifierNodeOrToken.location];

    internal override TextLocation location => locations.FirstOrDefault();

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences {
        get {
            BelteSyntaxNode node = null;

            if (_identifierNodeOrToken.isToken) {
                if (_identifierNodeOrToken.parent is not null)
                    // node = _identifierNodeOrToken.parent.FirstAncestorOrSelf<LabeledStatementSyntax>();
                    node = null;
            } else {
                node = _identifierNodeOrToken.AsNode().FirstAncestorOrSelf<SwitchLabelSyntax>();
            }

            return node is null ? [] : [new SyntaxReference(node)];
        }
    }

    internal override SyntaxReference syntaxReference => declaringSyntaxReferences.FirstOrDefault();

    internal override MethodSymbol containingMethod => _containingMethod;

    internal override Symbol containingSymbol => _containingMethod;

    internal override SyntaxNodeOrToken identifierNodeOrToken => _identifierNodeOrToken;

    internal ConstantValue switchCaseLabelConstant { get; }

    internal override bool Equals(Symbol obj, TypeCompareKind compareKind) {
        if (obj == (object)this)
            return true;

        return obj is SourceLabelSymbol symbol
            && symbol._identifierNodeOrToken.kind != SyntaxKind.None
            && symbol._identifierNodeOrToken.Equals(_identifierNodeOrToken)
            && symbol._containingMethod.Equals(_containingMethod, compareKind);
    }

    public override int GetHashCode() {
        return _identifierNodeOrToken.GetHashCode();
    }
}

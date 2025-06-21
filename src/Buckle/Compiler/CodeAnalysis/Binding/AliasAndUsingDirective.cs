using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal readonly struct AliasAndUsingDirective {
    internal readonly AliasSymbol alias;
    internal readonly SyntaxReference usingDirectiveReference;

    internal AliasAndUsingDirective(AliasSymbol alias, UsingDirectiveSyntax usingDirective) {
        this.alias = alias;
        usingDirectiveReference = new SyntaxReference(usingDirective);
    }

    internal UsingDirectiveSyntax usingDirective => (UsingDirectiveSyntax)usingDirectiveReference?.node;
}

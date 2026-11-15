using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal readonly struct AliasAndForwardDirective {
    internal readonly AliasSymbol alias;
    internal readonly SyntaxReference forwardDirectiveReference;
    internal readonly string unresolvedTarget;
    internal readonly bool isUnresolvedType;

    internal AliasAndForwardDirective(AliasSymbol alias, ForwardDirectiveSyntax forwardDirective) {
        this.alias = alias;
        forwardDirectiveReference = new SyntaxReference(forwardDirective);
    }

    internal AliasAndForwardDirective(string unresolvedTarget, bool isType) {
        this.unresolvedTarget = unresolvedTarget;
        isUnresolvedType = isType;
    }

    internal ForwardDirectiveSyntax forwardDirective => forwardDirectiveReference?.node as ForwardDirectiveSyntax;
}

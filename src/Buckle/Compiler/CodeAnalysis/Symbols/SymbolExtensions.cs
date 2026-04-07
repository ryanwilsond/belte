using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal static class SymbolExtensions {
    internal static BelteSyntaxNode GetNonNullSyntaxNode(this Symbol symbol) {
        if (symbol is not null) {
            var reference = symbol.syntaxReference;

            if (reference is null && symbol.isImplicitlyDeclared) {
                var containingSymbol = symbol.containingSymbol;

                if (containingSymbol is not null)
                    reference = containingSymbol.syntaxReference;
            }

            if (reference is not null)
                return (BelteSyntaxNode)reference.node;
        }

        return SyntaxTree.Dummy.GetRoot();
    }

    internal static bool IsTypeOrTypeAlias(this Symbol symbol) {
        switch (symbol.kind) {
            case SymbolKind.ArrayType:
            case SymbolKind.ErrorType:
            case SymbolKind.NamedType:
            case SymbolKind.PointerType:
            case SymbolKind.FunctionPointerType:
            case SymbolKind.TemplateParameter:
                return true;
            case SymbolKind.Alias:
                return IsTypeOrTypeAlias(((AliasSymbol)symbol).target);
            default:
                return false;
        }
    }

    internal static Symbol ConstructedFrom(this Symbol symbol) {
        switch (symbol.kind) {
            case SymbolKind.NamedType:
            case SymbolKind.ErrorType:
                return ((NamedTypeSymbol)symbol).constructedFrom;
            case SymbolKind.Method:
                return ((MethodSymbol)symbol).constructedFrom;
            default:
                return symbol;
        }
    }

    internal static int GetArity(this Symbol symbol) {
        if (symbol is not null) {
            switch (symbol.kind) {
                case SymbolKind.NamedType:
                    return ((NamedTypeSymbol)symbol).arity;
                case SymbolKind.Method:
                    return ((MethodSymbol)symbol).arity;
            }
        }

        return 0;
    }
}

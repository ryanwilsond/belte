using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

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

    internal static Symbol SymbolAsMember(this Symbol s, NamedTypeSymbol newOwner) {
        return s.kind switch {
            SymbolKind.Field => ((FieldSymbol)s).AsMember(newOwner),
            SymbolKind.Method => ((MethodSymbol)s).AsMember(newOwner),
            SymbolKind.NamedType => ((NamedTypeSymbol)s).AsMember(newOwner),
            _ => throw ExceptionUtilities.UnexpectedValue(s.kind),
        };
    }

    internal static bool IsTypeOrTypeAlias(this Symbol symbol) {
        switch (symbol.kind) {
            case SymbolKind.ArrayType:
            case SymbolKind.ErrorType:
            case SymbolKind.NamedType:
            case SymbolKind.PointerType:
            case SymbolKind.FunctionPointerType:
            case SymbolKind.FunctionType:
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

    internal static ImmutableArray<ISymbol> GetPublicSymbols(this ImmutableArray<Symbol> symbols) {
        return GetPublicSymbols<ISymbol, Symbol>(symbols);
    }

    private static ImmutableArray<TISymbol> GetPublicSymbols<TISymbol, TSymbol>(this ImmutableArray<TSymbol> symbols)
        where TISymbol : class, ISymbol where TSymbol : TISymbol {
        if (symbols.IsDefault)
            return default;

        return symbols.SelectAsArray(p => p.GetPublicSymbol<TISymbol, TSymbol>());
    }

    private static TISymbol GetPublicSymbol<TISymbol, TSymbol>(this TSymbol symbol)
        where TISymbol : class, ISymbol where TSymbol : TISymbol {
        return symbol;
    }

    internal static bool ContainsTupleNames(this Symbol member) {
        switch (member.kind) {
            case SymbolKind.Method:
                var method = (MethodSymbol)member;
                return method.returnType.ContainsTupleNames() ||
                    method.parameters.Any(static p => p.type.ContainsTupleNames());
            default:
                throw ExceptionUtilities.UnexpectedValue(member.kind);
        }
    }

    internal static Dictionary<TemplateParameterSymbol, int> MakeAdjustedTemplateParameterOrdinalsIfNeeded<TMember>(
        this TMember member, ImmutableArray<TemplateParameterSymbol> originalTypeParameters)
        where TMember : Symbol {
        if (member is MethodSymbol method) {
            Dictionary<TemplateParameterSymbol, int> ordinals = null;

            // TODO Extension methods
            // if (method.IsExtensionBlockMember() && method.Arity > 0 && method.ContainingType.Arity > 0) {
            //     Debug.Assert(originalTypeParameters.Length == method.Arity + method.ContainingType.Arity);

            //     // Since we're concatenating type parameters from the extension and from the method together
            //     // we need to control the ordinals that are used
            //     ordinals = new Dictionary<TypeParameterSymbol, int>(ReferenceEqualityComparer.Instance);
            //     for (int i = 0; i < originalTypeParameters.Length; i++) {
            //         ordinals.Add(originalTypeParameters[i], i);
            //     }
            // }

            return ordinals;
        }

        if (member is PropertySymbol)
            return null;

        throw ExceptionUtilities.UnexpectedValue(member);
    }
}

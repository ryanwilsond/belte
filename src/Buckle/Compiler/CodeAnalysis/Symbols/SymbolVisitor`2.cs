
namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SymbolVisitor<TArgument, TResult> {
    internal virtual TResult Visit(Symbol symbol, TArgument argument = default) {
        if (symbol is null)
            return default;

        return symbol.Accept(this, argument);
    }

    internal virtual TResult DefaultVisit(Symbol symbol, TArgument argument) {
        return default;
    }

    internal virtual TResult VisitNamespace(NamespaceSymbol symbol, TArgument argument) {
        return DefaultVisit(symbol, argument);
    }

    internal virtual TResult VisitNamedType(NamedTypeSymbol symbol, TArgument argument) {
        return DefaultVisit(symbol, argument);
    }

    internal virtual TResult VisitArrayType(ArrayTypeSymbol symbol, TArgument argument) {
        return DefaultVisit(symbol, argument);
    }

    internal virtual TResult VisitErrorType(ErrorTypeSymbol symbol, TArgument argument) {
        return DefaultVisit(symbol, argument);
    }

    internal virtual TResult VisitTemplateParameter(TemplateParameterSymbol symbol, TArgument argument) {
        return DefaultVisit(symbol, argument);
    }

    internal virtual TResult VisitMethod(MethodSymbol symbol, TArgument argument) {
        return DefaultVisit(symbol, argument);
    }

    internal virtual TResult VisitField(FieldSymbol symbol, TArgument argument) {
        return DefaultVisit(symbol, argument);
    }

    internal virtual TResult VisitParameter(ParameterSymbol symbol, TArgument argument) {
        return DefaultVisit(symbol, argument);
    }

    internal virtual TResult VisitDataContainer(DataContainerSymbol symbol, TArgument argument) {
        return DefaultVisit(symbol, argument);
    }

    internal virtual TResult VisitLabel(LabelSymbol symbol, TArgument argument) {
        return DefaultVisit(symbol, argument);
    }

    internal virtual TResult VisitAlias(AliasSymbol symbol, TArgument argument) {
        return DefaultVisit(symbol, argument);
    }

    internal virtual TResult VisitAssembly(AssemblySymbol symbol, TArgument argument) {
        return DefaultVisit(symbol, argument);
    }
}

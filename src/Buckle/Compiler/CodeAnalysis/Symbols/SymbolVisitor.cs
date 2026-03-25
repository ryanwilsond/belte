
namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SymbolVisitor {
    internal virtual void Visit(Symbol symbol) {
        symbol?.Accept(this);
    }

    internal virtual void DefaultVisit(Symbol symbol) { }

    internal virtual void VisitNamespace(NamespaceSymbol symbol) {
        DefaultVisit(symbol);
    }

    internal virtual void VisitNamedType(NamedTypeSymbol symbol) {
        DefaultVisit(symbol);
    }

    internal virtual void VisitArrayType(ArrayTypeSymbol symbol) {
        DefaultVisit(symbol);
    }

    internal virtual void VisitErrorType(ErrorTypeSymbol symbol) {
        DefaultVisit(symbol);
    }

    internal virtual void VisitPointerType(PointerTypeSymbol symbol) {
        DefaultVisit(symbol);
    }

    internal virtual void VisitFunctionPointerType(FunctionPointerTypeSymbol symbol) {
        DefaultVisit(symbol);
    }

    internal virtual void VisitTemplateParameter(TemplateParameterSymbol symbol) {
        DefaultVisit(symbol);
    }

    internal virtual void VisitMethod(MethodSymbol symbol) {
        DefaultVisit(symbol);
    }

    internal virtual void VisitField(FieldSymbol symbol) {
        DefaultVisit(symbol);
    }

    internal virtual void VisitParameter(ParameterSymbol symbol) {
        DefaultVisit(symbol);
    }

    internal virtual void VisitDataContainer(DataContainerSymbol symbol) {
        DefaultVisit(symbol);
    }

    internal virtual void VisitLabel(LabelSymbol symbol) {
        DefaultVisit(symbol);
    }

    internal virtual void VisitAlias(AliasSymbol symbol) {
        DefaultVisit(symbol);
    }

    internal virtual void VisitAssembly(AssemblySymbol symbol) {
        DefaultVisit(symbol);
    }

    internal virtual void VisitModule(ModuleSymbol symbol) {
        DefaultVisit(symbol);
    }
}

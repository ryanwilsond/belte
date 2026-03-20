using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal readonly struct NamespaceOrTypeOrAliasSymbolWithAnnotations {
    private readonly Symbol _symbol;

    private NamespaceOrTypeOrAliasSymbolWithAnnotations(TypeWithAnnotations typeWithAnnotations) {
        this.typeWithAnnotations = typeWithAnnotations;
    }

    private NamespaceOrTypeOrAliasSymbolWithAnnotations(Symbol symbol, bool isNullable) {
        _symbol = symbol;
        this.isNullable = isNullable;
    }

    internal TypeWithAnnotations typeWithAnnotations { get; }

    internal Symbol symbol => _symbol ?? typeWithAnnotations.type;

    internal bool isType => typeWithAnnotations is not null;

    internal bool isAlias => _symbol?.kind == SymbolKind.Alias;

    internal NamespaceOrTypeSymbol namespaceOrTypeSymbol => symbol as NamespaceOrTypeSymbol;

    internal bool isDefault => !typeWithAnnotations.hasType && _symbol is null;

    internal bool isNullable { get; }

    internal static NamespaceOrTypeOrAliasSymbolWithAnnotations CreateUnannotated(
        bool isNullable,
        Symbol symbol) {
        if (symbol is null)
            return default;

        return symbol is not TypeSymbol type
            ? new NamespaceOrTypeOrAliasSymbolWithAnnotations(symbol, isNullable)
            : new NamespaceOrTypeOrAliasSymbolWithAnnotations(new TypeWithAnnotations(type, isNullable));
    }

    public static implicit operator NamespaceOrTypeOrAliasSymbolWithAnnotations(
        TypeWithAnnotations typeWithAnnotations) {
        return new NamespaceOrTypeOrAliasSymbolWithAnnotations(typeWithAnnotations);
    }
}

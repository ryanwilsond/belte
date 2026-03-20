using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class ExtendedErrorTypeSymbol : ErrorTypeSymbol {
    internal readonly bool variableUsedBeforeDeclaration;
    private readonly ImmutableArray<Symbol> _candidateSymbols;

    internal ExtendedErrorTypeSymbol(
        Compilation compilation,
        string name,
        int arity,
        BelteDiagnostic error,
        bool unreported = false,
        bool variableUsedBeforeDeclaration = false)
        : this(compilation.globalNamespaceInternal, name, arity, error, unreported, variableUsedBeforeDeclaration) {
    }

    internal ExtendedErrorTypeSymbol(
        NamespaceOrTypeSymbol containingSymbol,
        string name,
        int arity,
        BelteDiagnostic error,
        bool unreported = false,
        bool variableUsedBeforeDeclaration = false) {
        this.name = name;
        this.error = error;
        this.containingSymbol = containingSymbol;
        this.arity = arity;
        this.unreported = unreported;
        this.variableUsedBeforeDeclaration = variableUsedBeforeDeclaration;
        resultKind = LookupResultKind.Empty;
    }

    internal ExtendedErrorTypeSymbol(
        NamespaceOrTypeSymbol guessSymbol,
        LookupResultKind resultKind,
        BelteDiagnostic error,
        bool unreported = false)
        : this(guessSymbol.ContainingNamespaceOrType(), guessSymbol, resultKind, error, unreported) {
    }

    internal ExtendedErrorTypeSymbol(
        NamespaceOrTypeSymbol containingSymbol,
        Symbol guessSymbol,
        LookupResultKind resultKind,
        BelteDiagnostic error,
        bool unreported = false)
        : this(containingSymbol, [guessSymbol], resultKind, error, GetArity(guessSymbol), unreported) {
    }

    internal ExtendedErrorTypeSymbol(
        NamespaceOrTypeSymbol containingSymbol,
        ImmutableArray<Symbol> candidateSymbols,
        LookupResultKind resultKind,
        BelteDiagnostic error,
        int arity,
        bool unreported = false)
        : this(containingSymbol, candidateSymbols[0].name, arity, error, unreported) {
        _candidateSymbols = UnwrapErrorCandidates(candidateSymbols);
        this.resultKind = resultKind;
    }

    private static ImmutableArray<Symbol> UnwrapErrorCandidates(ImmutableArray<Symbol> candidateSymbols) {
        var candidate = candidateSymbols.IsEmpty ? null : candidateSymbols[0] as ErrorTypeSymbol;
        return (candidate is not null && !candidate.candidateSymbols.IsEmpty)
            ? candidate.candidateSymbols
            : candidateSymbols;
    }

    public override string name { get; }

    public override int arity { get; }

    internal override BelteDiagnostic error { get; }

    internal override LookupResultKind resultKind { get; }

    internal override ImmutableArray<Symbol> candidateSymbols => _candidateSymbols.NullToEmpty();

    internal override bool unreported { get; }

    internal override bool mangleName => arity > 0;

    internal override Symbol containingSymbol { get; }

    internal override NamedTypeSymbol originalDefinition => this;

    internal override NamedTypeSymbol constructedFrom => this;

    internal override NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved) {
        return null;
    }

    internal static TypeSymbol ExtractNonErrorType(TypeSymbol oldSymbol) {
        if (oldSymbol is null || oldSymbol.typeKind != TypeKind.Error)
            return oldSymbol;

        var oldError = oldSymbol.originalDefinition as ExtendedErrorTypeSymbol;

        if (oldError is not null &&
            !oldError._candidateSymbols.IsDefault &&
            oldError._candidateSymbols.Length == 1) {
            var type = oldError._candidateSymbols[0] as TypeSymbol;

            if (type is not null)
                return type.GetNonErrorGuess();
        }

        return null;
    }

    internal override bool Equals(TypeSymbol t2, TypeCompareKind comparison) {
        if (ReferenceEquals(this, t2))
            return true;

        if (t2 is not ExtendedErrorTypeSymbol other || unreported || other.unreported)
            return false;

        return (containingType is not null
            ? containingType.Equals(other.containingType, comparison)
            : containingSymbol is null
                ? other.containingSymbol is null
                : containingSymbol.Equals(other.containingSymbol)) && name == other.name && arity == other.arity;
    }

    public override int GetHashCode() {
        return Hash.Combine(
            arity,
            Hash.Combine(
                containingSymbol is not null ? containingSymbol.GetHashCode() : 0,
                name is not null ? name.GetHashCode() : 0
            )
        );
    }

    private static int GetArity(Symbol symbol) {
        return symbol.kind switch {
            SymbolKind.NamedType or SymbolKind.ErrorType => ((NamedTypeSymbol)symbol).arity,
            SymbolKind.Method => ((MethodSymbol)symbol).arity,
            _ => 0,
        };
    }
}

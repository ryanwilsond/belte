using System;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

public readonly struct SymbolInfo : IEquatable<SymbolInfo> {
    internal static readonly SymbolInfo None = default;

    private readonly ImmutableArray<ISymbol> _candidateSymbols;

    internal SymbolInfo(ISymbol symbol) : this(symbol, [], CandidateReason.None) { }

    internal SymbolInfo(ISymbol symbol, CandidateReason reason) : this(symbol, [], reason) { }

    internal SymbolInfo(ImmutableArray<ISymbol> candidateSymbols, CandidateReason candidateReason)
        : this(symbol: null, candidateSymbols, candidateReason) { }

    private SymbolInfo(ISymbol symbol, ImmutableArray<ISymbol> candidateSymbols, CandidateReason candidateReason) {
        this.symbol = symbol;
        _candidateSymbols = candidateSymbols;
        this.candidateReason = candidateReason;
    }

    public ISymbol symbol { get; }

    public ImmutableArray<ISymbol> candidateSymbols => _candidateSymbols.NullToEmpty();

    public CandidateReason candidateReason { get; }

    internal bool isEmpty => symbol is null && candidateSymbols.Length == 0;

    internal ImmutableArray<ISymbol> GetAllSymbols() {
        return symbol is null ? candidateSymbols : [symbol];
    }

    public override bool Equals(object? obj)
        => obj is SymbolInfo info && Equals(info);

    public bool Equals(SymbolInfo other) {
        return candidateReason == other.candidateReason &&
            Equals(symbol, other.symbol) &&
            candidateSymbols.SequenceEqual(other.candidateSymbols);
    }

    public override int GetHashCode() {
        return Hash.Combine(symbol, Hash.Combine(Hash.CombineValues(candidateSymbols, 4), (int)candidateReason));
    }
}

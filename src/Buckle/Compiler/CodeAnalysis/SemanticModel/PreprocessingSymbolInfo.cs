using System;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

public readonly struct PreprocessingSymbolInfo : IEquatable<PreprocessingSymbolInfo> {
    internal static readonly PreprocessingSymbolInfo None = new PreprocessingSymbolInfo(null, false);

    public IPreprocessingSymbol symbol { get; }

    public bool isDefined { get; }

    internal PreprocessingSymbolInfo(IPreprocessingSymbol symbol, bool isDefined) {
        this.symbol = symbol;
        this.isDefined = isDefined;
    }

    public bool Equals(PreprocessingSymbolInfo other) {
        return Equals(symbol, other.symbol)
            && Equals(isDefined, other.isDefined);
    }

    public override bool Equals(object obj) {
        return obj is PreprocessingSymbolInfo p && Equals(p);
    }

    public override int GetHashCode() {
        return Hash.Combine(isDefined, Hash.Combine(symbol, 0));
    }
}

using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Evaluating;

public sealed class EvaluatorContext {
    internal readonly CompilationOptions options;

    private readonly Dictionary<string, (DataContainerSymbol, EvaluatorObject)> _symbols;

    public EvaluatorContext(CompilationOptions options) {
        _symbols = new Dictionary<string, (DataContainerSymbol, EvaluatorObject)>(32);
        this.options = options;
    }

    public IEnumerable<IDataContainerSymbol> GetTrackedSymbols() {
        return _symbols.Values.Select(pair => pair.Item1);
    }

    public Dictionary<ISymbol, EvaluatorObject> GetTrackedSymbolsAndObjects() {
        return _symbols.Values.ToDictionary(pair => (ISymbol)pair.Item1, pair => pair.Item2);
    }

    internal bool TryGetSymbol(DataContainerSymbol symbol, out EvaluatorObject value) {
        var succeeded = _symbols.TryGetValue(symbol.name, out var pair);
        value = pair.Item2;
        return succeeded;
    }

    internal void AddOrUpdateSymbol(DataContainerSymbol symbol, EvaluatorObject value) {
        _symbols[symbol.name] = (symbol, value);
    }

    public override string ToString() {
        return $"EvaluatorContext [ Tracking {_symbols.Count} symbols ]";
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;
using Shared;

namespace Buckle.CodeAnalysis.Evaluating;

public sealed class EvaluatorContext : IDisposable {
    internal readonly CompilationOptions options;

    internal Thread graphicsThread;
    internal GraphicsHandler graphicsHandler;
    internal ValueWrapper<bool> maintainThread = false;
    internal ValueWrapper<bool> createWindow = true;

    private Dictionary<string, (DataContainerSymbol, EvaluatorObject)> _symbols;

    public EvaluatorContext(CompilationOptions options) {
        _symbols = new Dictionary<string, (DataContainerSymbol, EvaluatorObject)>(32);
        this.options = options;
    }

    public void Dispose() {
        maintainThread = false;
        createWindow = false;
        graphicsHandler?.Exit();
        graphicsThread?.Join();
        graphicsHandler = null;
        graphicsThread = null;
    }

    internal void WaitForCompletion() {
        maintainThread = false;
        createWindow = false;
        graphicsThread?.Join();
    }

    public IEnumerable<IDataContainerSymbol> GetTrackedSymbols() {
        return _symbols.Values.Select(pair => pair.Item1);
    }

    public Dictionary<ISymbol, EvaluatorObject> GetTrackedSymbolsAndObjects() {
        return _symbols.Values.ToDictionary(pair => (ISymbol)pair.Item1, pair => pair.Item2);
    }

    public void Reset() {
        if (graphicsHandler is not null) {
            createWindow = false;
            graphicsHandler.Exit();
        }

        _symbols = new Dictionary<string, (DataContainerSymbol, EvaluatorObject)>(32);
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

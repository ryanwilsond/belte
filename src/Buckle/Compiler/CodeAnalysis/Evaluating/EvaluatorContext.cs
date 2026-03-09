using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Lowering;
using Buckle.CodeAnalysis.Symbols;
using Shared;

namespace Buckle.CodeAnalysis.Evaluating;

public sealed class EvaluatorContext : IDisposable {
    internal readonly CompilationOptions options;
    internal readonly Heap heap;

    internal Thread graphicsThread;
    internal GraphicsHandler graphicsHandler;
    internal ValueWrapper<bool> maintainThread = false;
    internal ValueWrapper<bool> createWindow = true;
    internal EvaluatorValue[] globalSlots;

    private Dictionary<string, (DataContainerSymbol, int)> _globals;
    private int _bumpPointer;

    public EvaluatorContext(CompilationOptions options) {
        _globals = new Dictionary<string, (DataContainerSymbol, int)>(32);
        globalSlots = new EvaluatorValue[32];
        heap = new Heap();
        this.options = options;
    }

    internal ImmutableDictionary<NamedTypeSymbol, EvaluatorSlotManager> typeLayouts { get; set; }

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

        if (graphicsThread is null)
            return;

        graphicsThread?.Join();
    }

    public Dictionary<ISymbol, EvaluatorValue> GetTrackedGlobalObjects() {
        return _globals.Values.ToDictionary(pair => (ISymbol)pair.Item1, pair => globalSlots[pair.Item2]);
    }

    public void Reset() {
        if (graphicsHandler is not null) {
            createWindow = false;
            graphicsHandler.Exit();
        }

        _globals = new Dictionary<string, (DataContainerSymbol, int)>(32);
        globalSlots = new EvaluatorValue[32];
        heap.FreeAll();
    }

    internal bool TryGetGlobal(DataContainerSymbol symbol, out EvaluatorValue value) {
        var succeeded = _globals.TryGetValue(symbol.name, out var pair);
        value = globalSlots[pair.Item2];
        return succeeded;
    }

    internal int GetSlotOfGlobal(DataContainerSymbol symbol) {
        return _globals[symbol.name].Item2;
    }

    internal void AddOrUpdateGlobal(DataContainerSymbol symbol, EvaluatorValue value) {
        if (_globals.TryGetValue(symbol.name, out var pair)) {
            globalSlots[pair.Item2] = value;
            _globals[symbol.name] = (symbol, pair.Item2);
        } else {
            var index = _bumpPointer++;
            EnsureCapacity(_bumpPointer);
            globalSlots[index] = value;
            _globals.Add(symbol.name, (symbol, index));
        }
    }

    private void EnsureCapacity(int required) {
        if (required <= globalSlots.Length)
            return;

        var newCapacity = Math.Max(required, globalSlots.Length * 2);
        Array.Resize(ref globalSlots, newCapacity);
    }

    public override string ToString() {
        return $"EvaluatorContext [ Tracking {_globals.Count} symbols ]";
    }
}

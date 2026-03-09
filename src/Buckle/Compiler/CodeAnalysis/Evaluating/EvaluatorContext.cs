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

    private Dictionary<string, (DataContainerSymbol, EvaluatorValue)> _globals;

    public EvaluatorContext(CompilationOptions options) {
        _globals = new Dictionary<string, (DataContainerSymbol, EvaluatorValue)>(32);
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
        return _globals.Values.ToDictionary(pair => (ISymbol)pair.Item1, pair => pair.Item2);
    }

    public void Reset() {
        if (graphicsHandler is not null) {
            createWindow = false;
            graphicsHandler.Exit();
        }

        _globals = new Dictionary<string, (DataContainerSymbol, EvaluatorValue)>(32);
        heap.FreeAll();
    }

    public override string ToString() {
        return $"EvaluatorContext [ Tracking {_globals.Count} symbols ]";
    }
}

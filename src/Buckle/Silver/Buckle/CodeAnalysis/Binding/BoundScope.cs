using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.Diagnostics;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class BoundScope {
    private Dictionary<string, Symbol> symbols_;
    public BoundScope parent;

    public BoundScope(BoundScope parent_) {
        parent = parent_;
    }

    public bool TryDeclareFunction(FunctionSymbol symbol) => TryDeclareSymbol(symbol);
    public bool TryDeclareVariable(VariableSymbol symbol) => TryDeclareSymbol(symbol);

    internal bool TryDeclareSymbol<TSymbol>(TSymbol symbol) where TSymbol : Symbol {
        if (symbols_ == null)
            symbols_ = new Dictionary<string, Symbol>();
        else if (symbols_.ContainsKey(symbol.name))
            return false;

        symbols_.Add(symbol.name, symbol);
        return true;
    }

    internal Symbol LookupSymbol(string name) {
        if (symbols_ != null && symbols_.TryGetValue(name, out var symbol))
            return symbol;

        return parent?.LookupSymbol(name);
    }

    public bool TryModifySymbol(string name, Symbol newSymbol) {
        var symbol = LookupSymbol(name);

        if (symbol == null)
            return false;

        symbols_[name] = newSymbol;
        return true;
    }

    public ImmutableArray<VariableSymbol> GetDeclaredVariables() => GetDeclaredSymbols<VariableSymbol>();
    public ImmutableArray<FunctionSymbol> GetDeclaredFunctions() => GetDeclaredSymbols<FunctionSymbol>();

    private ImmutableArray<TSymbol> GetDeclaredSymbols<TSymbol>() where TSymbol : Symbol {
        if (symbols_ == null)
            return ImmutableArray<TSymbol>.Empty;

        return symbols_.Values.OfType<TSymbol>().ToImmutableArray();
    }
}

internal sealed class BoundGlobalScope {
    public ImmutableArray<(FunctionSymbol function, BoundBlockStatement body)> functionBodies { get; }
    public BoundGlobalScope previous { get; }
    public BelteDiagnosticQueue diagnostics { get; }
    public FunctionSymbol mainFunction { get; }
    public FunctionSymbol scriptFunction { get; }
    public ImmutableArray<FunctionSymbol> functions { get; }
    public ImmutableArray<VariableSymbol> variables { get; }
    public ImmutableArray<BoundStatement> statements { get; }

    public BoundGlobalScope(
        ImmutableArray<(FunctionSymbol function, BoundBlockStatement body)> functionBodies_,
        BoundGlobalScope previous_, BelteDiagnosticQueue diagnostics_, FunctionSymbol mainFunction_,
        FunctionSymbol scriptFunction_, ImmutableArray<FunctionSymbol> functions_,
        ImmutableArray<VariableSymbol> variables_, ImmutableArray<BoundStatement> statements_) {
        functionBodies = functionBodies_;
        previous = previous_;
        diagnostics = new BelteDiagnosticQueue();
        diagnostics.Move(diagnostics_);
        mainFunction = mainFunction_;
        scriptFunction = scriptFunction_;
        functions = functions_;
        variables = variables_;
        statements = statements_;
    }
}

internal sealed class BoundProgram {
    public BoundProgram previous { get; }
    public BelteDiagnosticQueue diagnostics { get; }
    public FunctionSymbol mainFunction { get; }
    public FunctionSymbol scriptFunction { get; }
    public ImmutableDictionary<FunctionSymbol, BoundBlockStatement> functionBodies { get; }

    public BoundProgram(
        BoundProgram previous_, BelteDiagnosticQueue diagnostics_,
        FunctionSymbol mainFunction_,
        FunctionSymbol scriptFunction_,
        ImmutableDictionary<FunctionSymbol, BoundBlockStatement> functionBodies_) {
        previous = previous_;
        diagnostics = diagnostics_;
        mainFunction = mainFunction_;
        scriptFunction = scriptFunction_;
        functionBodies = functionBodies_;
    }
}

using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.Diagnostics;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class BoundScope {
    private Dictionary<string, Symbol> symbols_;
    internal BoundScope parent;

    internal BoundScope(BoundScope parent_) {
        parent = parent_;
    }

    internal bool TryDeclareFunction(FunctionSymbol symbol) => TryDeclareSymbol(symbol);
    internal bool TryDeclareVariable(VariableSymbol symbol) => TryDeclareSymbol(symbol);

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

    internal bool TryModifySymbol(string name, Symbol newSymbol) {
        var symbol = LookupSymbol(name);

        if (symbol == null)
            return false;

        symbols_[name] = newSymbol;
        return true;
    }

    internal ImmutableArray<VariableSymbol> GetDeclaredVariables() => GetDeclaredSymbols<VariableSymbol>();
    internal ImmutableArray<FunctionSymbol> GetDeclaredFunctions() => GetDeclaredSymbols<FunctionSymbol>();

    private ImmutableArray<TSymbol> GetDeclaredSymbols<TSymbol>() where TSymbol : Symbol {
        if (symbols_ == null)
            return ImmutableArray<TSymbol>.Empty;

        return symbols_.Values.OfType<TSymbol>().ToImmutableArray();
    }
}

internal sealed class BoundGlobalScope {
    internal ImmutableArray<(FunctionSymbol function, BoundBlockStatement body)> functionBodies { get; }
    internal BoundGlobalScope previous { get; }
    internal BelteDiagnosticQueue diagnostics { get; }
    internal FunctionSymbol mainFunction { get; }
    internal FunctionSymbol scriptFunction { get; }
    internal ImmutableArray<FunctionSymbol> functions { get; }
    internal ImmutableArray<VariableSymbol> variables { get; }
    internal ImmutableArray<BoundStatement> statements { get; }

    internal BoundGlobalScope(
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
    internal BoundProgram previous { get; }
    internal BelteDiagnosticQueue diagnostics { get; }
    internal FunctionSymbol mainFunction { get; }
    internal FunctionSymbol scriptFunction { get; }
    internal ImmutableDictionary<FunctionSymbol, BoundBlockStatement> functionBodies { get; }

    internal BoundProgram(
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

using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.Diagnostics;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class BoundScope {
    private List<Symbol> symbols_;
    internal BoundScope parent;

    internal BoundScope(BoundScope parent_) {
        parent = parent_;
    }

    internal bool TryDeclareFunction(FunctionSymbol symbol) => TryDeclareSymbol(symbol);
    internal bool TryDeclareVariable(VariableSymbol symbol) => TryDeclareSymbol(symbol);

    internal bool TryDeclareSymbol<TSymbol>(TSymbol symbol) where TSymbol : Symbol {
        if (symbols_ == null) {
            symbols_ = new List<Symbol>();
        } else if (Contains(symbol.name)) {
            if (symbol is FunctionSymbol fs) {
                foreach (var s in symbols_)
                    if (FunctionsMatch(s as FunctionSymbol, fs))
                        return false;
            } else {
                return false;
            }
        }

        symbols_.Add(symbol);
        return true;
    }

    private bool FunctionsMatch(FunctionSymbol a, FunctionSymbol b) {
        if (a.name != b.name)
            return false;

        if (a.parameters.Length != b.parameters.Length)
            return false;

        for (int i=0; i<a.parameters.Length; i++)
            if (!BoundTypeClause.Equals(a.parameters[i].typeClause, b.parameters[i].typeClause))
                return false;

        return true;
    }

    internal Symbol LookupSymbol(string name) {
        // Use LookupOverloads for functions
        if (symbols_ != null)
            foreach (var symbol in symbols_)
                if (symbol.name == name)
                    return symbol;

        return parent?.LookupSymbol(name);
    }

    internal bool TryModifySymbol(string name, Symbol newSymbol) {
        // Does not work with overloads
        var symbol = LookupSymbol(name);

        if (symbol == null)
            return false;

        for (int i=0; i<symbols_.Count; i++) {
            if (symbols_[i].name == name) {
                symbols_[i] = newSymbol;
                break;
            }
        }

        return true;
    }

    internal ImmutableArray<VariableSymbol> GetDeclaredVariables() => GetDeclaredSymbols<VariableSymbol>();
    internal ImmutableArray<FunctionSymbol> GetDeclaredFunctions() => GetDeclaredSymbols<FunctionSymbol>();

    private bool Contains(string name) {
        foreach (var symbol in symbols_)
            if (symbol.name == name)
                return true;

        return false;
    }

    private ImmutableArray<TSymbol> GetDeclaredSymbols<TSymbol>() where TSymbol : Symbol {
        if (symbols_ == null)
            return ImmutableArray<TSymbol>.Empty;

        return symbols_.OfType<TSymbol>().ToImmutableArray();
    }

    internal void CopyInlines(BoundScope scope) {
        foreach (var inline in scope.GetDeclaredFunctions().Where(i => i.name.StartsWith("<$Inline")))
            // Ignore failures, do not override higher level symbols
            TryDeclareFunction(inline);
    }

    internal ImmutableArray<Symbol> LookupOverloads(
        string name, ImmutableArray<Symbol>? current = null) {
        var overloads = ImmutableArray.CreateBuilder<Symbol>();

        if (symbols_ != null) {
            foreach (var symbol in symbols_) {
                if (symbol is Symbol s && symbol.name == name) {
                    if (current != null) {
                        var skip = false;

                        foreach (var cs in current.Value) {
                            if (s is FunctionSymbol fs && cs is FunctionSymbol fcs && FunctionsMatch(fs, fcs)) {
                                skip = true;
                                break;
                            }
                        }

                        if (skip)
                            continue;
                    }

                    overloads.Add(s);
                }
            }
        }

        if (parent != null)
            overloads.AddRange(parent?.LookupOverloads(name, overloads.ToImmutable()));

        return overloads.ToImmutable();
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

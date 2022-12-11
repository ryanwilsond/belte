using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.Diagnostics;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A scope of code.
/// </summary>
internal sealed class BoundScope {
    private List<Symbol> symbols_;
    private BoundScope parent_;

    /// <summary>
    /// Creates a new scope with an optional parent.
    /// </summary>
    /// <param name="parent">Enclosing scope</param>
    internal BoundScope(BoundScope parent) {
        this.parent_ = parent;
    }

    internal BoundScope parent {
        get {
            return parent_;
        } set {
            parent_ = value;
        }
    }

    /// <summary>
    /// Attempts to declare a function.
    /// </summary>
    /// <param name="symbol">Function to declare</param>
    /// <returns>If the function was successfully added to the scope</returns>
    internal bool TryDeclareFunction(FunctionSymbol symbol) => TryDeclareSymbol(symbol);

    /// <summary>
    /// Attempts to declare a variable.
    /// </summary>
    /// <param name="symbol">Variable to declare</param>
    /// <returns>If the variable was successfully added to the scope</returns>
    internal bool TryDeclareVariable(VariableSymbol symbol) => TryDeclareSymbol(symbol);

    /// <summary>
    /// Gets all declared variables in this scope (not any parent scopes).
    /// </summary>
    /// <returns>All declared variables</returns>
    internal ImmutableArray<VariableSymbol> GetDeclaredVariables() => GetDeclaredSymbols<VariableSymbol>();

    /// <summary>
    /// Gets all declared function in this scope (not any parent scopes).
    /// </summary>
    /// <returns>All declared functions</returns>
    internal ImmutableArray<FunctionSymbol> GetDeclaredFunctions() => GetDeclaredSymbols<FunctionSymbol>();

    /// <summary>
    /// Attempts to find a symbol based on name (including parent scopes).
    /// Because it only searches for one, use LookupOverloads for function symbols.
    /// </summary>
    /// <param name="name">Name of symbol</param>
    /// <returns>Symbol if found, null otherwise</returns>
    internal Symbol LookupSymbol(string name) {
        // Use LookupOverloads for functions
        if (symbols_ != null)
            foreach (var symbol in symbols_)
                if (symbol.name == name)
                    return symbol;

        return parent?.LookupSymbol(name);
    }

    /// <summary>
    /// Attempts to modify an already declared symbol.
    /// Does not work with overloads, only modifies the first one. However the order is not constant.
    /// Thus only use with functions with guaranteed no overloads, or variable symbols.
    /// </summary>
    /// <param name="name">Name of symbol</param>
    /// <param name="newSymbol">New symbol data to replace old the symbol</param>
    /// <returns>If the symbol was found and successfully updated</returns>
    internal bool TryModifySymbol(string name, Symbol newSymbol) {
        // Does not work with overloads
        // TODO Need to allow overloads, as someone may try to define overloads for a nested function
        var symbol = LookupSymbol(name);

        if (symbol == null)
            return false;

        var succeeded = false;
        ref BoundScope parentRef = ref parent_;
        ref List<Symbol> symbols = ref symbols_;

        while (true) {
            if (symbols != null) {
                for (int i=0; i<symbols.Count; i++) {
                    if (symbols[i].name == name) {
                        symbols[i] = newSymbol;
                        succeeded = true;
                        break;
                    }
                }
            }

            if (parentRef == null || succeeded)
                break;
            else {
                symbols = ref parentRef.symbols_;
                parentRef = ref parentRef.parent_;
            }
        }

        return succeeded;
    }

    /// <summary>
    /// Copies all inlines from a scope into this scope.
    /// Does not shadow, instead skips already declared functions in higher scopes.
    /// </summary>
    /// <param name="scope">Scope to copy inlines from (not all functions)</param>
    internal void CopyInlines(BoundScope scope) {
        foreach (var inline in scope.GetDeclaredFunctions().Where(i => i.name.StartsWith("<$Inline")))
            // Ignore failures, do not override higher level symbols
            TryDeclareFunction(inline);
    }

    /// <summary>
    /// Finds all overloads of a function by name.
    /// Technically searches for all symbols, but this function is intended to be used for functions.
    /// </summary>
    /// <param name="name">Name of function</param>
    /// <param name="strictName">Scope specific name (for inlines), searches for this first</param>
    /// <returns>All found overloads (including from parent scopes)</returns>
    internal ImmutableArray<Symbol> LookupOverloads(string name, string strictName) {
        var symbols = LookupOverloadsInternal(strictName, strict: true);

        if (symbols.Length > 0)
            return symbols;

        return LookupOverloadsInternal(name);
    }

    private ImmutableArray<Symbol> LookupOverloadsInternal(
        string name, bool strict = false, ImmutableArray<Symbol>? current_ = null) {
        var overloads = ImmutableArray.CreateBuilder<Symbol>();

        if (symbols_ != null) {
            foreach (var symbol in symbols_) {
                // If it is a nested function, the name will be something like <funcName::name>$
                if (symbol is Symbol s &&
                    (symbol.name == name || (strict == false && symbol.name.EndsWith($"::{name}>$")))) {
                    if (current_ != null) {
                        var skip = false;

                        foreach (var cs in current_.Value) {
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

        if (parent != null) {
            overloads.AddRange(parent?.LookupOverloadsInternal(
                name,
                strict: strict,
                current_: current_ == null
                    ? overloads.ToImmutable()
                    : overloads.ToImmutable().AddRange(current_.Value)));
        }

        return overloads.ToImmutable();
    }

    private bool TryDeclareSymbol<TSymbol>(TSymbol symbol) where TSymbol : Symbol {
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
}

/// <summary>
/// A bound global scope, stores top level symbols.
/// </summary>
internal sealed class BoundGlobalScope {
    /// <param name="previous">Previous global scope (if applicable)</param>
    internal BoundGlobalScope(
        ImmutableArray<(FunctionSymbol function, BoundBlockStatement body)> functionBodies,
        BoundGlobalScope previous, BelteDiagnosticQueue diagnostics, FunctionSymbol mainFunction,
        FunctionSymbol scriptFunction, ImmutableArray<FunctionSymbol> functions,
        ImmutableArray<VariableSymbol> variables, ImmutableArray<BoundStatement> statements) {
        this.functionBodies = functionBodies;
        this.previous = previous;
        this.diagnostics = new BelteDiagnosticQueue();
        this.diagnostics.Move(diagnostics);
        this.mainFunction = mainFunction;
        this.scriptFunction = scriptFunction;
        this.functions = functions;
        this.variables = variables;
        this.statements = statements;
    }

    internal ImmutableArray<(FunctionSymbol function, BoundBlockStatement body)> functionBodies { get; }

    /// <summary>
    /// Previous global scope (if applicable).
    /// </summary>
    internal BoundGlobalScope previous { get; }

    internal BelteDiagnosticQueue diagnostics { get; }

    internal FunctionSymbol mainFunction { get; }

    internal FunctionSymbol scriptFunction { get; }

    internal ImmutableArray<FunctionSymbol> functions { get; }

    internal ImmutableArray<VariableSymbol> variables { get; }

    internal ImmutableArray<BoundStatement> statements { get; }
}

/// <summary>
/// Bound program.
/// </summary>
internal sealed class BoundProgram {
    /// <param name="previous">Previous bound program (if applicable)</param>
    internal BoundProgram(
        BoundProgram previous, BelteDiagnosticQueue diagnostics,
        FunctionSymbol mainFunction,
        FunctionSymbol scriptFunction,
        ImmutableDictionary<FunctionSymbol, BoundBlockStatement> functionBodies) {
        this.previous = previous;
        this.diagnostics = diagnostics;
        this.mainFunction = mainFunction;
        this.scriptFunction = scriptFunction;
        this.functionBodies = functionBodies;
    }

    /// <summary>
    /// Previous bound program (if applicable).
    /// </summary>
    internal BoundProgram previous { get; }

    internal BelteDiagnosticQueue diagnostics { get; }

    internal FunctionSymbol mainFunction { get; }

    internal FunctionSymbol scriptFunction { get; }

    internal ImmutableDictionary<FunctionSymbol, BoundBlockStatement> functionBodies { get; }
}

using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A scope of code.
/// </summary>
internal sealed class BoundScope {
    private List<Symbol> _symbols;
    private BoundScope _parent;
    private bool _isBlock;

    /// <summary>
    /// Creates a new scope with an optional parent.
    /// </summary>
    /// <param name="parent">Enclosing scope.</param>
    internal BoundScope(BoundScope parent, bool isBlock = false) {
        this._parent = parent;
        this._isBlock = isBlock;
    }

    internal BoundScope parent {
        get {
            return _parent;
        } set {
            _parent = value;
        }
    }

    /// <summary>
    /// Attempts to declare a function.
    /// </summary>
    /// <param name="symbol"><see cref="FunctionSymbol" /> to declare.</param>
    /// <returns>If the function was successfully added to the scope.</returns>
    internal bool TryDeclareFunction(FunctionSymbol symbol) => TryDeclareSymbol(symbol);

    /// <summary>
    /// Attempts to declare a variable.
    /// </summary>
    /// <param name="symbol"><see cref="VariableSymbol" /> to declare.</param>
    /// <returns>If the variable was successfully added to the scope.</returns>
    internal bool TryDeclareVariable(VariableSymbol symbol) => TryDeclareSymbol(symbol);

    /// <summary>
    /// Attempts to declare a type.
    /// </summary>
    /// <param name="symbol"><see cref="StructSymbol" /> to declare.</param>
    /// <returns>If the type was successfully added to the scope.</returns>
    internal bool TryDeclareType(TypeSymbol symbol) => TryDeclareSymbol(symbol);

    /// <summary>
    /// Gets all declared variables in this scope (not any parent scopes).
    /// </summary>
    /// <returns>All declared variables.</returns>
    internal ImmutableArray<VariableSymbol> GetDeclaredVariables() => GetDeclaredSymbols<VariableSymbol>();

    /// <summary>
    /// Gets all declared functions in this scope (not any parent scopes).
    /// </summary>
    /// <returns>All declared functions.</returns>
    internal ImmutableArray<FunctionSymbol> GetDeclaredFunctions() => GetDeclaredSymbols<FunctionSymbol>();

    /// <summary>
    /// Gets all declared types in this scope (not any parent scopes).
    /// </summary>
    /// <returns>All declared types.</returns>
    internal ImmutableArray<TypeSymbol> GetDeclaredTypes() => GetDeclaredSymbols<TypeSymbol>();

    /// <summary>
    /// Attempts to find a <see cref="Symbol" /> based on the name (including parent scopes).
    /// Because it only searches for one, use <see cref="BoundScope.LookupOverloads" /> for function symbols.
    /// Can restrict to a specific child class of <see cref="Symbol" />.
    /// </summary>
    /// <param name="name">Name of <see cref="Symbol" /> to search for.</param>
    /// <typeparam name="T">Type of <see cref="Symbol" /> to search for.</typeparam>
    /// <returns><see cref="Symbol" /> if found, null otherwise.</returns>
    internal T LookupSymbol<T>(string name) where T : Symbol {
        if (_symbols != null)
            foreach (var symbol in _symbols)
                if (symbol.name == name && symbol is T)
                    return symbol as T;

        return parent?.LookupSymbol<T>(name);
    }

    /// <summary>
    /// Attempts to find a <see cref="Symbol" /> based on name (including parent scopes).
    /// Because it only searches for one, use <see cref="BoundScope.LookupOverloads" /> for function symbols.
    /// </summary>
    /// <param name="name">Name of <see cref="Symbol" />.</param>
    /// <returns><see cref="Symbol" /> if found, null otherwise.</returns>
    internal Symbol LookupSymbol(string name) => LookupSymbol<Symbol>(name);

    /// <summary>
    /// Attempts to modify an already declared <see cref="Symbol" />.
    /// Does not work with overloads, only modifies the first one. However the order is not constant.
    /// Thus only use with FunctionSymbols with guaranteed no overloads, or VariableSymbols.
    /// </summary>
    /// <param name="name">Name of <see cref="Symbol" />.</param>
    /// <param name="newSymbol">New symbol data to replace old the <see cref="Symbol" />.</param>
    /// <returns>If the <see cref="Symbol" /> was found and successfully updated.</returns>
    internal bool TryModifySymbol(string name, Symbol newSymbol) {
        // Does not work with overloads
        var symbol = LookupSymbol(name);

        if (symbol == null)
            return false;

        var succeeded = false;
        ref BoundScope parentRef = ref _parent;
        ref List<Symbol> symbols = ref _symbols;

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
                symbols = ref parentRef._symbols;
                parentRef = ref parentRef._parent;
            }
        }

        return succeeded;
    }

    /// <summary>
    /// Copies all inlines from another <see cref="BoundScope" /> into this.
    /// Does not shadow, instead skips already declared functions in higher scopes.
    /// </summary>
    /// <param name="scope"><see cref="BoundScope" /> to copy inlines from (not all functions).</param>
    internal void CopyInlines(BoundScope scope) {
        foreach (var inline in scope.GetDeclaredFunctions().Where(i => i.name.Contains(">g__$Inline")))
            // Ignore failures, do not override higher level symbols
            TryDeclareFunction(inline);
    }

    /// <summary>
    /// Finds all overloads of a <see cref="FunctionSymbol" /> by name.
    /// Technically searches for all symbols, but this function is intended to be used for functions.
    /// </summary>
    /// <param name="name">Name of <see cref="FunctionSymbol" />.</param>
    /// <param name="strictName">Scope specific name (for inlines), searches for this first.</param>
    /// <returns>All found overloads (including from parent scopes).</returns>
    internal ImmutableArray<Symbol> LookupOverloads(string name, string strictName) {
        var symbols = LookupOverloadsInternal(strictName, strict: true);

        if (symbols.Length > 0)
            return symbols;

        return LookupOverloadsInternal(name);
    }

    private ImmutableArray<Symbol> LookupOverloadsInternal(
        string name, bool strict = false, ImmutableArray<Symbol>? _current = null) {
        var overloads = ImmutableArray.CreateBuilder<Symbol>();

        if (_symbols != null) {
            foreach (var symbol in _symbols) {
                // If it is a nested function, the name will be something like <funcName>g__name
                if (symbol is Symbol s &&
                    (symbol.name == name || (!strict && symbol.name.Contains($">g__{name}")))) {
                    if (_current != null) {
                        var skip = false;

                        foreach (var cs in _current.Value) {
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
                _current: _current == null
                    ? overloads.ToImmutable()
                    : overloads.ToImmutable().AddRange(_current.Value)));
        }

        return overloads.ToImmutable();
    }

    private bool TryDeclareSymbol<TSymbol>(TSymbol symbol) where TSymbol : Symbol {
        if (_symbols == null)
            _symbols = new List<Symbol>();

        if (Contains(symbol.name)) {
            if (symbol is FunctionSymbol fs) {
                foreach (var s in _symbols)
                    if (FunctionsMatch(s as FunctionSymbol, fs))
                        return false;
            } else {
                return false;
            }
        }

        _symbols.Add(symbol);
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
        if (_symbols != null) {
            foreach (var symbol in _symbols)
                if (symbol.name == name)
                    return true;
        }

        return _isBlock ? (parent == null ? false : parent.Contains(name)) : false;
    }

    private ImmutableArray<TSymbol> GetDeclaredSymbols<TSymbol>() where TSymbol : Symbol {
        if (_symbols == null)
            return ImmutableArray<TSymbol>.Empty;

        return _symbols.OfType<TSymbol>().ToImmutableArray();
    }
}

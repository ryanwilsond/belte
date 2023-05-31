using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A scope of code.
/// </summary>
internal sealed class BoundScope {
    private List<Symbol> _symbols;
    private List<Symbol> _assignedSymbols;
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
        }
        set {
            _parent = value;
        }
    }

    /// <summary>
    /// Attempts to declare a method.
    /// </summary>
    /// <param name="symbol"><see cref="MethodSymbol" /> to declare.</param>
    /// <returns>If the method was successfully added to the scope.</returns>
    internal bool TryDeclareMethod(MethodSymbol symbol) => TryDeclareSymbol(symbol);

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
    /// Gets all declared methods in this scope (not any parent scopes).
    /// </summary>
    /// <returns>All declared methods.</returns>
    internal ImmutableArray<MethodSymbol> GetDeclaredMethods() => GetDeclaredSymbols<MethodSymbol>();

    /// <summary>
    /// Gets all declared types in this scope (not any parent scopes).
    /// </summary>
    /// <returns>All declared types.</returns>
    internal ImmutableArray<TypeSymbol> GetDeclaredTypes() => GetDeclaredSymbols<TypeSymbol>();

    /// <summary>
    /// Gets all variables that were assigned to in this scope (not any parent scopes).
    /// </summary>
    /// <returns>All assigned variables.</returns>
    internal ImmutableArray<VariableSymbol> GetAssignedVariables() => GetAssignedSymbols<VariableSymbol>();

    /// <summary>
    /// Attempts to find a <see cref="Symbol" /> based on the name (including parent scopes).
    /// Because it only searches for one, use <see cref="BoundScope.LookupOverloads" /> for method symbols.
    /// Can restrict to a specific child class of <see cref="Symbol" />.
    /// </summary>
    /// <param name="name">Name of <see cref="Symbol" /> to search for.</param>
    /// <typeparam name="T">Type of <see cref="Symbol" /> to search for.</typeparam>
    /// <returns><see cref="Symbol" /> if found, null otherwise.</returns>
    internal T LookupSymbol<T>(string name) where T : Symbol {
        if (_symbols != null) {
            foreach (var symbol in _symbols) {
                if (symbol.name == name && symbol is T)
                    return symbol as T;
            }
        }

        return parent?.LookupSymbol<T>(name);
    }

    /// <summary>
    /// Attempts to find a <see cref="Symbol" /> based on name (including parent scopes).
    /// Because it only searches for one, use <see cref="BoundScope.LookupOverloads" /> for method symbols.
    /// </summary>
    /// <param name="name">Name of <see cref="Symbol" />.</param>
    /// <returns><see cref="Symbol" /> if found, null otherwise.</returns>
    internal Symbol LookupSymbol(string name) => LookupSymbol<Symbol>(name);

    /// <summary>
    /// Attempts to find a <see cref="TypeSymbol" /> with the given name and arity. If none exist, null is returned.
    /// </summary>
    internal TypeSymbol LookupTypeSymbol(string name, int arity) {
        if (_symbols != null) {
            foreach (var symbol in _symbols) {
                if (symbol.name == name && symbol is TypeSymbol ts && ts.arity == arity)
                    return symbol as TypeSymbol;
            }
        }

        return parent?.LookupTypeSymbol(name, arity);
    }

    /// <summary>
    /// Attempts to replace an already declared <see cref="Symbol" />.
    /// </summary>
    /// <param name="currentSymbol">The <see cref="Symbol" /> currently in the scope to replace.</param>
    /// <param name="newSymbol">New symbol to replace old the <see cref="Symbol" />.</param>
    /// <returns>If the <see cref="Symbol" /> was found and successfully replaced.</returns>
    internal bool TryReplaceSymbol(Symbol currentSymbol, Symbol newSymbol) {
        if (currentSymbol is null)
            return false;

        var succeeded = false;
        ref var parentRef = ref _parent;
        ref var symbols = ref _symbols;

        while (true) {
            if (symbols != null) {
                for (var i = 0; i < symbols.Count; i++) {
                    if (symbols[i] == currentSymbol) {
                        symbols[i] = newSymbol;
                        succeeded = true;
                        break;
                    }
                }
            }

            if (parentRef is null || succeeded) {
                break;
            } else {
                symbols = ref parentRef._symbols;
                parentRef = ref parentRef._parent;
            }
        }

        return succeeded;
    }

    /// <summary>
    /// Note the assignment of a <see cref="Symbol" />. This does not actually change anything about the scope, rather
    /// this acts as readonly tracking data for some <see cref="Binder" /> components.
    /// </summary>
    /// <param name="symbol"><see cref="Symbol" /> that was assigned to.</param>
    internal void NoteAssignment(Symbol symbol) {
        if (_assignedSymbols is null)
            _assignedSymbols = new List<Symbol>();

        _assignedSymbols.Add(symbol);
    }

    /// <summary>
    /// Finds all overloads by name.
    /// Can technically searches for all symbols, but this method is intended to be used for methods.
    /// </summary>
    /// <param name="name">Name of <see cref="Symbol" />.</param>
    /// <param name="strictName">Scope specific name, searches for this first.</param>
    /// <typeparam name="T">Type of <see cref="Symbol" /> to look for while searching.</typeparam>
    /// <returns>All found overloads (including from parent scopes), allows shadowing.</returns>
    internal ImmutableArray<T> LookupOverloads<T>(string name, string strictName) where T : Symbol {
        var symbols = LookupOverloadsInternal<T>(strictName, strict: true);

        if (symbols.Length > 0)
            return symbols;

        return LookupOverloadsInternal<T>(name);
    }

    private ImmutableArray<T> LookupOverloadsInternal<T>(
        string name, bool strict = false, ImmutableArray<T>? _current = null) where T : Symbol {
        var overloads = ImmutableArray.CreateBuilder<T>();

        if (_symbols != null) {
            foreach (var symbol in _symbols) {
                // If it is a nested function, the name will be something like <funcName>g__name
                if (symbol is T s &&
                    (symbol.name == name || (!strict && symbol.name.Contains($">g__{name}")))) {
                    if (_current != null) {
                        var skip = false;

                        foreach (var cs in _current.Value) {
                            if (s is MethodSymbol fs && cs is MethodSymbol fcs && MethodsMatch(fs, fcs)) {
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
            overloads.AddRange(parent?.LookupOverloadsInternal<T>(
                name,
                strict: strict,
                _current: _current is null
                    ? overloads.ToImmutable()
                    : overloads.ToImmutable().AddRange(_current.Value))
            );
        }

        return overloads.ToImmutable();
    }

    private bool TryDeclareSymbol<T>(T symbol) where T : Symbol {
        if (_symbols is null)
            _symbols = new List<Symbol>();

        if (Contains(symbol.name)) {
            if (symbol is MethodSymbol fs) {
                foreach (var s in _symbols) {
                    // Doesn't check if they refer to the same thing, but if their signatures are the same
                    // If so, keeping both would make all calls ambiguous so it is not allowed
                    if (MethodsMatch(s as MethodSymbol, fs))
                        return false;
                }
            } else if (symbol is ClassSymbol cs) {
                foreach (var s in _symbols) {
                    if (s is ClassSymbol scs && scs.name == cs.name && scs.arity == cs.arity)
                        return false;
                }
            } else if (symbol is StructSymbol ss) {
                foreach (var s in _symbols) {
                    if (s is StructSymbol sss && sss.name == ss.name && sss.arity == ss.arity)
                        return false;
                }
            } else {
                return false;
            }
        }

        _symbols.Add(symbol);

        return true;
    }

    private bool MethodsMatch(MethodSymbol a, MethodSymbol b) {
        if (a.name != b.name)
            return false;

        if (a.parameters.Length != b.parameters.Length)
            return false;

        for (var i = 0; i < a.parameters.Length; i++) {
            if (!a.parameters[i].type.Equals(b.parameters[i].type))
                return false;
        }

        return true;
    }

    private bool Contains(string name) {
        if (_symbols != null) {
            foreach (var symbol in _symbols) {
                if (symbol.name == name)
                    return true;
            }
        }

        return _isBlock ? (parent is null ? false : parent.Contains(name)) : false;
    }

    private ImmutableArray<T> GetDeclaredSymbols<T>() where T : Symbol {
        if (_symbols is null)
            return ImmutableArray<T>.Empty;

        return _symbols.OfType<T>().ToImmutableArray();
    }

    private ImmutableArray<T> GetAssignedSymbols<T>() where T : Symbol {
        if (_assignedSymbols is null)
            return ImmutableArray<T>.Empty;

        return _assignedSymbols.OfType<T>().ToImmutableArray();
    }
}

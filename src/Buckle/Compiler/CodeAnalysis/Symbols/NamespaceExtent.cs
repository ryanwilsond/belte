using System;

namespace Buckle.CodeAnalysis.Symbols;

internal readonly struct NamespaceExtent : IEquatable<NamespaceExtent> {
    private readonly object _symbolOrCompilation;

    internal NamespaceExtent(AssemblySymbol assembly) {
        kind = NamespaceKind.Assembly;
        _symbolOrCompilation = assembly;
    }

    internal NamespaceExtent(Compilation compilation) {
        kind = NamespaceKind.Compilation;
        _symbolOrCompilation = compilation;
    }

    internal NamespaceKind kind { get; }

    internal AssemblySymbol assembly {
        get {
            if (kind == NamespaceKind.Assembly)
                return (AssemblySymbol)_symbolOrCompilation;

            throw new InvalidOperationException();
        }
    }

    internal Compilation compilation {
        get {
            if (kind == NamespaceKind.Compilation)
                return (Compilation)_symbolOrCompilation;

            throw new InvalidOperationException();
        }
    }

    public override bool Equals(object obj) {
        return obj is NamespaceExtent extent && Equals(extent);
    }

    public bool Equals(NamespaceExtent other) {
        return Equals(_symbolOrCompilation, other._symbolOrCompilation);
    }

    public override int GetHashCode() {
        return (_symbolOrCompilation is null) ? 0 : _symbolOrCompilation.GetHashCode();
    }
}

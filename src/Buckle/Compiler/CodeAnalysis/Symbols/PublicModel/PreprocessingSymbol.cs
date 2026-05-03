using System.Collections.Immutable;
using Buckle.CodeAnalysis.Display;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class PreprocessingSymbol : IPreprocessingSymbol {
    private readonly string _name;

    internal PreprocessingSymbol(string name) {
        _name = name;
    }

    public string name => _name;

    public string metadataName => _name;

    public SymbolKind kind => SymbolKind.Preprocessing;

    public ISymbol containingSymbol => null;

    public Compilation declaringCompilation => null;

    public sealed override int GetHashCode() {
        return _name.GetHashCode();
    }

    public override bool Equals(object obj) {
        if (ReferenceEquals(this, obj))
            return true;

        if (ReferenceEquals(obj, null))
            return false;

        return obj is PreprocessingSymbol other && _name.Equals(other._name);
    }

    public string ToDisplayString(SymbolDisplayFormat format = null) {
        return SymbolDisplay.ToDisplayString(this, format);
    }

    public ImmutableArray<DisplayTextSegment> ToDisplaySegments(SymbolDisplayFormat format = null) {
        return SymbolDisplay.ToDisplaySegments(this, format);
    }
}

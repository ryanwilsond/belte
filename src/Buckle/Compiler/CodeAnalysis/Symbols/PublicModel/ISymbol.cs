using System.Collections.Immutable;
using Buckle.CodeAnalysis.Display;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Represents a symbol (class, method, parameter, etc.) exposed by the compiler.
/// </summary>
public interface ISymbol {
    /// <summary>
    /// Name of the symbol.
    /// </summary>
    string name { get; }

    /// <summary>
    /// Name of the symbol including template suffix.
    /// </summary>
    string metadataName { get; }

    /// <summary>
    /// The type of symbol this is (see <see cref="SymbolKind" />).
    /// </summary>
    SymbolKind kind { get; }

    /// <summary>
    /// The symbol that this symbol is a member of, if applicable.
    /// </summary>
    ISymbol containingSymbol { get; }

    Compilation declaringCompilation { get; }

    string ToDisplayString(SymbolDisplayFormat format = null);

    ImmutableArray<DisplayTextSegment> ToDisplaySegments(SymbolDisplayFormat format = null);
}

using System.IO;
using Buckle.CodeAnalysis.Display;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A base symbol.
/// </summary>
internal abstract class Symbol {
    private protected Symbol(string name) {
        this.name = name;
    }

    /// <summary>
    /// Name of the symbol.
    /// </summary>
    internal string name { get; }

    /// <summary>
    /// The type of symbol this is (see <see cref="SymbolKind" />).
    /// </summary>
    internal abstract SymbolKind kind { get; }

    public override string ToString() {
        using (var writer = new StringWriter()) {
            this.WriteTo(writer);

            return writer.ToString();
        }
    }
}

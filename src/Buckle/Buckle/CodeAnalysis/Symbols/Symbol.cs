using System.IO;
using Buckle.IO;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Type of symbol.
/// </summary>
internal enum SymbolType {
    GlobalVariable,
    LocalVariable,
    Type,
    Function,
    Parameter,
    Field,
}

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
    /// The type of symbol this is (see <see cref="SymbolType" />).
    /// </summary>
    internal abstract SymbolType type { get; }

    public override string ToString() {
        using (var writer = new StringWriter()) {
            this.WriteTo(writer);
            return writer.ToString();
        }
    }
}

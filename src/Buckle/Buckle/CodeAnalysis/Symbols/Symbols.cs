using System.IO;
using Buckle.IO;
using Buckle.CodeAnalysis.Binding;

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
    /// The type of symbol this is (see SymbolType)
    /// </summary>
    internal abstract SymbolType type { get; }

    /// <summary>
    /// String representation of the symbol (see TextWriterExtensions)
    /// </summary>
    /// <returns>String representation</returns>
    public override string ToString() {
        using (var writer = new StringWriter()) {
            this.WriteTo(writer);
            return writer.ToString();
        }
    }
}

/// <summary>
/// A variable symbol. This can be any type of variable.
/// </summary>
internal abstract class VariableSymbol : Symbol {
    /// <summary>
    /// Creates a variable symbol.
    /// </summary>
    /// <param name="name">Name of the variable</param>
    /// <param name="typeClause">Type clause of the variable</param>
    /// <param name="constant">Constant value of the variable</param>
    internal VariableSymbol(string name, BoundTypeClause typeClause, BoundConstant constant)
        : base(name) {
        this.typeClause = typeClause;
        constantValue = typeClause.isConstant && !typeClause.isReference ? constant : null;
    }

    /// <summary>
    /// Type clause of the variable.
    /// </summary>
    internal BoundTypeClause typeClause { get; }

    /// <summary>
    /// Constant value of the variable (can be null).
    /// </summary>
    internal BoundConstant constantValue { get; }
}

/// <summary>
/// A global variable symbol. This is a variable declared in the global scope.
/// </summary>
internal sealed class GlobalVariableSymbol : VariableSymbol {
    /// <summary>
    /// Creates a global variable.
    /// </summary>
    /// <param name="name">Name of the variable</param>
    /// <param name="typeClause">Type clause of the variable</param>
    /// <param name="constant">Constant value of the variable</param>
    internal GlobalVariableSymbol(string name, BoundTypeClause typeClause, BoundConstant constant)
        : base(name, typeClause, constant) { }

    /// <summary>
    /// Type of symbol (see SymbolType).
    /// </summary>
    internal override SymbolType type => SymbolType.GlobalVariable;
}

/// <summary>
/// A local variable symbol. This is a variable declared anywhere except the global scope (thus being more local).
/// </summary>
internal class LocalVariableSymbol : VariableSymbol {
    /// <summary>
    /// Creates a local variable symbol.
    /// </summary>
    /// <param name="name">Name of the variable</param>
    /// <param name="typeClause">Type clause of the variable</param>
    /// <param name="constant">Constant value of the variable</param>
    internal LocalVariableSymbol(string name, BoundTypeClause typeClause, BoundConstant constant)
        : base(name, typeClause, constant) { }

    /// <summary>
    /// Type of symbol (see SymbolType).
    /// </summary>
    internal override SymbolType type => SymbolType.LocalVariable;
}

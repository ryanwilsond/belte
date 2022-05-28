using System.IO;
using Buckle.IO;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

internal enum SymbolType {
    GlobalVariable,
    LocalVariable,
    Type,
    Function,
    Parameter,
}

internal abstract class Symbol {
    public string name { get; }
    public abstract SymbolType type { get; }

    private protected Symbol(string name_) {
        name = name_;
    }

    public override string ToString() {
        using (var writer = new StringWriter()) {
            this.WriteTo(writer);
            return writer.ToString();
        }
    }
}

internal abstract class VariableSymbol : Symbol {
    public BoundTypeClause typeClause { get; }
    public BoundConstant constantValue { get; }

    internal VariableSymbol(string name, BoundTypeClause typeClause_, BoundConstant constant)
        : base(name) {
        typeClause = typeClause_;
        constantValue = typeClause.isConstant && !typeClause.isReference ? constant : null;
    }
}

internal sealed class GlobalVariableSymbol : VariableSymbol {
    public override SymbolType type => SymbolType.GlobalVariable;

    internal GlobalVariableSymbol(string name, BoundTypeClause typeClause, BoundConstant constant)
        : base(name, typeClause, constant) { }
}

internal class LocalVariableSymbol : VariableSymbol {
    public override SymbolType type => SymbolType.LocalVariable;

    internal LocalVariableSymbol(string name, BoundTypeClause typeClause, BoundConstant constant)
        : base(name, typeClause, constant) { }
}

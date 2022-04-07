using System;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols {

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

        public override string ToString() => name;
    }

    internal abstract class VariableSymbol : Symbol {
        public TypeSymbol lType { get; }
        public bool isReadOnly { get; }

        internal VariableSymbol(string name, bool isReadOnly_, TypeSymbol lType_) : base(name) {
            lType = lType_;
            isReadOnly = isReadOnly_;
        }
    }

    internal sealed class GlobalVariableSymbol : VariableSymbol {
        public override SymbolType type => SymbolType.GlobalVariable;

        internal GlobalVariableSymbol(string name, bool isReadOnly, TypeSymbol lType)
            : base(name, isReadOnly, lType) { }
    }

    internal class LocalVariableSymbol : VariableSymbol {
        public override SymbolType type => SymbolType.LocalVariable;

        internal LocalVariableSymbol(string name, bool isReadOnly, TypeSymbol lType)
            : base(name, isReadOnly, lType) { }
    }
}

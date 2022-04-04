using System;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols {

    internal enum SymbolType {
        Variable,
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

    internal class VariableSymbol : Symbol {
        public TypeSymbol lType { get; }
        public bool isReadOnly { get; }
        public override SymbolType type => SymbolType.Variable;

        internal VariableSymbol(string name, bool isReadOnly_, TypeSymbol lType_) : base(name) {
            lType = lType_;
            isReadOnly = isReadOnly_;
        }
    }
}

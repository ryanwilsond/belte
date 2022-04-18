using System.IO;
using Buckle.IO;
using Buckle.CodeAnalysis.Binding;

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

        public override string ToString() {
            using (var writer = new StringWriter()) {
                this.WriteTo(writer);
                return writer.ToString();
            }
        }
    }

    internal abstract class VariableSymbol : Symbol {
        public TypeSymbol lType { get; }
        public bool isReadOnly { get; }
        internal BoundConstant constantValue { get; }

        internal VariableSymbol(string name, bool isReadOnly_, TypeSymbol lType_, BoundConstant constant) : base(name) {
            lType = lType_;
            isReadOnly = isReadOnly_;
            constantValue = isReadOnly ? constant : null;
        }
    }

    internal sealed class GlobalVariableSymbol : VariableSymbol {
        public override SymbolType type => SymbolType.GlobalVariable;

        internal GlobalVariableSymbol(string name, bool isReadOnly, TypeSymbol lType, BoundConstant constant)
            : base(name, isReadOnly, lType, constant) { }
    }

    internal class LocalVariableSymbol : VariableSymbol {
        public override SymbolType type => SymbolType.LocalVariable;

        internal LocalVariableSymbol(string name, bool isReadOnly, TypeSymbol lType, BoundConstant constant)
            : base(name, isReadOnly, lType, constant) { }
    }
}

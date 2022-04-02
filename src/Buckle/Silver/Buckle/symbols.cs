using System;

namespace Buckle {
    internal sealed class VariableSymbol {
        public string name { get; }
        public Type lType { get; }
        public bool isReadOnly { get; }

        internal VariableSymbol(string name_, bool isReadOnly_, Type lType_) {
            name = name_;
            lType = lType_;
            isReadOnly = isReadOnly_;
        }

        public override string ToString() => name;
    }

    internal sealed class LabelSymbol {
        public string name { get; }

        internal LabelSymbol(string name_) {
            name = name_;
        }

        public override string ToString() => name;
    }
}

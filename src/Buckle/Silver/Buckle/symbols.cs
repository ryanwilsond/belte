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
    }
}

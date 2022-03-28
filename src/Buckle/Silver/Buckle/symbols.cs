using System;

namespace Buckle {
    internal sealed class VariableSymbol {
        public string name { get; }
        public Type ltype { get; }

        internal VariableSymbol(string name_, Type ltype_) {
            name = name_;
            ltype = ltype_;
        }
    }
}

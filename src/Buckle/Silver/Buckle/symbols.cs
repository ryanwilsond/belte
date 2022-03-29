using System;

namespace Buckle {
    internal sealed class VariableSymbol {
        public string name { get; }
        public Type ltype { get; }
        public bool is_read_only { get; }

        internal VariableSymbol(string name_, bool is_read_only_, Type ltype_) {
            name = name_;
            ltype = ltype_;
            is_read_only = is_read_only_;
        }
    }
}

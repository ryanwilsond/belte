using System;

namespace Repl;

public abstract partial class Repl {
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    protected sealed class MetaCommandAttribute : Attribute {
        public MetaCommandAttribute(string name, string description) {
            this.name = name;
            this.description = description;
        }

        public string name { get; }
        public string description { get; }
    }
}

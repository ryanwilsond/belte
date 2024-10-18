using System;

namespace Repl;

public abstract partial class Repl {
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    private protected sealed class MetaCommandAttribute(string name, string description) : Attribute {
        public string name { get; } = name;
        public string description { get; } = description;
    }
}

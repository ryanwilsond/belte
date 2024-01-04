using System.Reflection;

namespace Repl;

public abstract partial class Repl {
    private sealed class MetaCommand(string name, string description, MethodInfo method) {
        public string name { get; } = name;
        public string description { get; set; } = description;
        public MethodInfo method { get; } = method;
    }
}

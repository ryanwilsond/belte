using System.Reflection;

namespace Repl;

public abstract partial class Repl {
    private sealed class MetaCommand {
        public MetaCommand(string name, string description, MethodInfo method) {
            this.name = name;
            this.method = method;
            this.description = description;
        }

        public string name { get; }
        public string description { get; set; }
        public MethodInfo method { get; }
    }
}

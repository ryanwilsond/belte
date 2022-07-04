
namespace Buckle.CodeAnalysis.Binding;

internal sealed class BoundLabel {
    internal string name { get; }

    internal BoundLabel(string name_) {
        name = name_;
    }

    public override string ToString() => name;
}

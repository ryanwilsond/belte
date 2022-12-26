
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A label used by goto statements.
/// </summary>
internal sealed class BoundLabel {
    internal BoundLabel(string name) {
        this.name = name;
    }

    internal string name { get; }

    public override string ToString() => name;
}

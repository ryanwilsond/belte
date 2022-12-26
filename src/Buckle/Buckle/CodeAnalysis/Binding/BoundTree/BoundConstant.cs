
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Bound constant.
/// </summary>
internal sealed class BoundConstant {
    internal BoundConstant(object value) {
        this.value = value;
    }

    internal object value { get; }
}

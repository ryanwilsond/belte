
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Extensions on the <see cref="BinderFlags" /> enum.
/// </summary>
internal static class BinderFlagsExtensions {
    /// <summary>
    /// If this includes a specific subset of flags.
    /// </summary>
    internal static bool Includes(this BinderFlags self, BinderFlags other) {
        return (self & other) == other;
    }
}

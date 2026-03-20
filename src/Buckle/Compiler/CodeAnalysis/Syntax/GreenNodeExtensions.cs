using Diagnostics;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Extensions on the <see cref="GreenNode" /> class.
/// </summary>
internal static class GreenNodeExtensions {
    /// <summary>
    /// Returns a new green node with the given diagnostics.
    /// </summary>
    internal static T WithDiagnosticsGreen<T>(this T node, Diagnostic[] diagnostics) where T : GreenNode {
        return (T)node.SetDiagnostics(diagnostics);
    }

    /// <summary>
    /// Returns a new green node without any current diagnostics, if any.
    /// </summary>
    internal static T WithoutDiagnosticsGreen<T>(this T node) where T : GreenNode {
        var current = node.GetDiagnostics();

        if (current is null || current.Length == 0)
            return node;

        return (T)node.SetDiagnostics(null);
    }
}

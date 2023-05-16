using Diagnostics;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Extensions on the <see cref="GreenNode" /> class.
/// </summary>
internal static class GreenNodeExtensions {
    /// <summary>
    /// Returns a new green tokens with the given diagnostics.
    /// </summary>
    internal static T WithDiagnosticsGreen<T>(this T node, Diagnostic[] diagnostics) where T : GreenNode {
        return (T)node.SetDiagnostics(diagnostics);
    }
}

using System.Collections.Generic;
using Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

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

    internal static T WithAnnotationsGreen<T>(this T node, IEnumerable<SyntaxAnnotation> annotations)
        where T : GreenNode {
        var newAnnotations = ArrayBuilder<SyntaxAnnotation>.GetInstance();

        foreach (var candidate in annotations) {
            if (!newAnnotations.Contains(candidate))
                newAnnotations.Add(candidate);
        }

        if (newAnnotations.Count == 0) {
            newAnnotations.Free();
            var existingAnnotations = node.GetAnnotations();

            if (existingAnnotations is null || existingAnnotations.Length == 0)
                return node;
            else
                return (T)node.SetAnnotations(null);
        } else {
            return (T)node.SetAnnotations(newAnnotations.ToArrayAndFree());
        }
    }

    internal static T WithAdditionalAnnotationsGreen<T>(
        this T node,
        IEnumerable<SyntaxAnnotation> annotations)
        where T : GreenNode {
        var existingAnnotations = node.GetAnnotations();

        if (annotations is null)
            return node;

        var newAnnotations = ArrayBuilder<SyntaxAnnotation>.GetInstance();
        newAnnotations.AddRange(existingAnnotations);

        foreach (var candidate in annotations) {
            if (!newAnnotations.Contains(candidate))
                newAnnotations.Add(candidate);
        }

        if (newAnnotations.Count == existingAnnotations.Length) {
            newAnnotations.Free();
            return node;
        } else {
            return (T)node.SetAnnotations(newAnnotations.ToArrayAndFree());
        }
    }
}

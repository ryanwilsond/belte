using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Authoring;

/// <summary>
/// Span of where a classification refers to.
/// </summary>
internal sealed class ClassifiedSpan {
    /// <summary>
    /// Creates a classified span.
    /// </summary>
    /// <param name="span">Span of where referring to</param>
    /// <param name="classification">Classification</param>
    internal ClassifiedSpan(TextSpan span, Classification classification) {
        this.span = span;
        this.classification = classification;
    }

    /// <summary>
    /// Span of where referring to.
    /// </summary>
    internal TextSpan span { get; }

    /// <summary>
    /// Classification.
    /// </summary>
    internal Classification classification { get; }
}

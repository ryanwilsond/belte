
namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// Represents a change to a span of text.
/// </summary>
internal sealed class TextChangeRange {
    /// <summary>
    /// Creates a <see cref="TextChangeRange" /> instance.
    /// </summary>
    internal TextChangeRange(TextSpan span, int newLength) {
        this.span = span;
        this.newLength = newLength;
    }

    /// <summary>
    /// The span of text before the edit which is being changed.
    /// </summary>
    internal TextSpan span { get; }

    /// <summary>
    /// Width of the span after the edit. A 0 here would represent a delete.
    /// </summary>
    internal int newLength { get; }
}

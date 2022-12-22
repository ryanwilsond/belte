using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Authoring;

/// <summary>
/// <see cref="TextSpan" /> of where a <see cref="Classification" /> refers to.
/// </summary>
internal sealed class ClassifiedSpan {
    /// <summary>
    /// Creates a <see cref="ClassifiedSpan" />.
    /// </summary>
    /// <param name="span"><see cref="TextSpan" /> of where referring to.</param>
    internal ClassifiedSpan(TextSpan span, Classification classification) {
        this.span = span;
        this.classification = classification;
    }

    /// <summary>
    /// <see cref="TextSpan" /> of where referring to.
    /// </summary>
    internal TextSpan span { get; }

    internal Classification classification { get; }
}

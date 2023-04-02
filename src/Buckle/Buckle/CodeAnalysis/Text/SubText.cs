
namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// A <see cref="SourceText" /> that represents a subrange of another <see cref="SourceText" />.
/// </summary>
internal sealed class SubText : SourceText {
    /// <summary>
    /// Creates an instance of <see cref="SubText" />.
    /// </summary>
    internal SubText(SourceText text, TextSpan span) : base(null, null) {
        underlyingText = text;
        underlyingSpan = span;
    }

    internal SourceText underlyingText { get; }

    internal TextSpan underlyingSpan { get; }

    public override int length => underlyingSpan.length;

    public override char this[int index] => underlyingText[underlyingSpan.start + index];
}

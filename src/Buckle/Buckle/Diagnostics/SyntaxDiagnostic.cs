using Diagnostics;

namespace Buckle.Diagnostics;

/// <summary>
/// Represents a <see cref="Diagnostic" /> with a relative position in a text.
/// </summary>
internal sealed class SyntaxDiagnostic : Diagnostic {
    /// <summary>
    /// Creates a new <see cref="SyntaxDiagnostic" />.
    /// </summary>
    /// <param name="info"><see cref="Diagnostic.info" />.</param>
    /// <param name="message"><see cref="Diagnostic.message" />.</param>
    /// <param name="suggestion"><see cref="Diagnostic.suggestion" />.</param>
    /// <param name="offset">
    /// Offset of this diagnostic from the beginning of the node that contains this diagnostic.
    /// </param>
    /// <param name="width">The width from the <param name="offset" /> that this diagnostic applies to.</param>
    internal SyntaxDiagnostic(DiagnosticInfo info, string message, string suggestion, int offset, int width)
        : base(info, message, suggestion) {
        this.offset = offset;
        this.width = width;
    }

    /// <summary>
    /// Creates a new <see cref="SyntaxDiagnostic" /> using an existing <see cref="Diagnostic" />.
    /// </summary>
    internal SyntaxDiagnostic(Diagnostic diagnostic, int offset, int width)
        : base(diagnostic.info, diagnostic.message, diagnostic.suggestion) {
        this.offset = offset;
        this.width = width;
    }

    /// <summary>
    /// The offset of where this diagnostic starts applying to from the start of the node.
    /// </summary>
    internal int offset { get; }

    /// <summary>
    /// The width starting from the offset that this diagnostic applies to.
    /// </summary>
    internal int width { get; }

    /// <summary>
    /// Updates the offset of this diagnostic.
    /// </summary>
    internal SyntaxDiagnostic WithOffset(int offset) {
        return new SyntaxDiagnostic(info, message, suggestion, offset, width);
    }
}

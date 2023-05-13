using Diagnostics;

namespace Buckle.Diagnostics;

internal sealed class SyntaxDiagnostic : Diagnostic {
    internal SyntaxDiagnostic(Diagnostic diagnostic, int offset, int width)
        : base(diagnostic.info, diagnostic.message, diagnostic.suggestion) {
        this.offset = offset;
        this.width = width;
    }

    internal SyntaxDiagnostic(DiagnosticInfo info, string message, string suggestion, int offset, int width)
        : base(info, message, suggestion) {
        this.offset = offset;
        this.width = width;
    }

    internal int offset { get; }

    internal int width { get; }

    internal SyntaxDiagnostic WithOffset(int offset) {
        return new SyntaxDiagnostic(info, message, suggestion, offset, width);
    }
}

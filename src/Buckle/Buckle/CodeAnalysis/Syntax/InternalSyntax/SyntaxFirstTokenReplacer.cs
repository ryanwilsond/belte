using Buckle.Diagnostics;
using Diagnostics;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed class SyntaxFirstTokenReplacer : SyntaxRewriter {
    private readonly SyntaxToken _oldToken;
    private readonly SyntaxToken _newToken;
    private readonly int _diagnosticOffsetDelta;
    private bool _foundOldToken;

    private SyntaxFirstTokenReplacer(SyntaxToken oldToken, SyntaxToken newToken, int diagnosticOffsetDelta) {
        _oldToken = oldToken;
        _newToken = newToken;
        _diagnosticOffsetDelta = diagnosticOffsetDelta;
        _foundOldToken = false;
    }

    internal static T Replace<T>(T root, SyntaxToken oldToken, SyntaxToken newToken, int diagnosticOffsetDelta)
        where T : BelteSyntaxNode {
        var replacer = new SyntaxFirstTokenReplacer(oldToken, newToken, diagnosticOffsetDelta);
        var newRoot = (T)replacer.Visit(root);
        return newRoot;
    }

    internal override BelteSyntaxNode Visit(BelteSyntaxNode node) {
        if (node != null) {
            if (!_foundOldToken) {
                var token = node as SyntaxToken;
                if (token != null) {
                    _foundOldToken = true;
                    return _newToken;
                }

                return UpdateDiagnosticOffset(base.Visit(node), _diagnosticOffsetDelta);
            }
        }

        return node;
    }

    private static T UpdateDiagnosticOffset<T>(T node, int diagnosticOffsetDelta) where T : BelteSyntaxNode {
        var oldDiagnostics = node.GetDiagnostics();

        if (oldDiagnostics == null || oldDiagnostics.Length == 0)
            return node;

        var numDiagnostics = oldDiagnostics.Length;
        var newDiagnostics = new Diagnostic[numDiagnostics];

        for (int i = 0; i < numDiagnostics; i++) {
            var oldDiagnostic = oldDiagnostics[i];
            var oldSyntaxDiagnostic = oldDiagnostic as SyntaxDiagnostic;

            newDiagnostics[i] = oldSyntaxDiagnostic == null ?
                oldDiagnostic :
                new SyntaxDiagnostic(
                    oldSyntaxDiagnostic,
                    oldSyntaxDiagnostic.offset + diagnosticOffsetDelta,
                    oldSyntaxDiagnostic.width
                );
        }

        return node.WithDiagnosticsGreen(newDiagnostics);
    }
}

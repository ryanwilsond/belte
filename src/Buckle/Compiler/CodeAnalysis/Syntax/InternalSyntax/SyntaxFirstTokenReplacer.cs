using Buckle.Diagnostics;
using Diagnostics;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Replaces the first <see cref="SyntaxToken" /> in a node.
/// </summary>
internal sealed class SyntaxFirstTokenReplacer : SyntaxRewriter {
    private readonly SyntaxToken _newToken;
    private readonly int _diagnosticOffsetDelta;
    private bool _foundOldToken;

    private SyntaxFirstTokenReplacer(SyntaxToken newToken, int diagnosticOffsetDelta) {
        _newToken = newToken;
        _diagnosticOffsetDelta = diagnosticOffsetDelta;
        _foundOldToken = false;
    }

    /// <summary>
    /// Replaces the first token contained within <param name="root" />.
    /// </summary>
    internal static T Replace<T>(T root, SyntaxToken newToken, int diagnosticOffsetDelta)
        where T : BelteSyntaxNode {
        var replacer = new SyntaxFirstTokenReplacer(newToken, diagnosticOffsetDelta);
        var newRoot = (T)replacer.Visit(root);
        return newRoot;
    }

    internal override BelteSyntaxNode Visit(BelteSyntaxNode node) {
        if (node is not null) {
            if (!_foundOldToken) {
                if (node is SyntaxToken) {
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

        if (oldDiagnostics is null || oldDiagnostics.Length == 0)
            return node;

        var numDiagnostics = oldDiagnostics.Length;
        var newDiagnostics = new Diagnostic[numDiagnostics];

        for (var i = 0; i < numDiagnostics; i++) {
            var oldDiagnostic = oldDiagnostics[i];

            newDiagnostics[i] = oldDiagnostic is not SyntaxDiagnostic oldSyntaxDiagnostic
                ? oldDiagnostic
                : new SyntaxDiagnostic(
                    oldSyntaxDiagnostic,
                    oldSyntaxDiagnostic.offset + diagnosticOffsetDelta,
                    oldSyntaxDiagnostic.width
                );
        }

        return node.WithDiagnosticsGreen(newDiagnostics);
    }
}

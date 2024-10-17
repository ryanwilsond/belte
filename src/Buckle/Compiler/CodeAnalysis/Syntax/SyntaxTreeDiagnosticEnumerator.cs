using System;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// An enumerator for diagnostics contained in a <see cref="SyntaxTree" />.
/// </summary>
internal partial struct SyntaxTreeDiagnosticEnumerator {
    private readonly SyntaxTree _syntaxTree;
    private NodeIterationStack _stack;
    private BelteDiagnostic _current;
    private int _position;
    private const int DefaultStackCapacity = 8;

    /// <summary>
    /// Creates a new <see cref="SyntaxTreeDiagnosticEnumerator" /> that will enumerate recursively through the
    /// given node, starting at the given position.
    /// </summary>
    /// <param name="syntaxTree">The <see cref="SyntaxTree" /> to be the source of the produced diagnostics.</param>
    internal SyntaxTreeDiagnosticEnumerator(SyntaxTree syntaxTree, GreenNode node, int position) {
        _syntaxTree = null;
        _current = null;
        _position = position;

        if (node is not null && node.containsDiagnostics) {
            _syntaxTree = syntaxTree;
            _stack = new NodeIterationStack(DefaultStackCapacity);
            _stack.PushNodeOrToken(node);
        } else {
            _stack = new NodeIterationStack();
        }
    }

    /// <summary>
    /// Moves the enumerator to the next diagnostic instance in the diagnostic list.
    /// </summary>
    /// <returns>Returns true if enumerator moved to the next diagnostic, false if the
    /// enumerator was at the end of the diagnostic list.</returns>
    public bool MoveNext() {
        while (_stack.Any()) {
            var diagIndex = _stack.top.diagnosticIndex;
            var node = _stack.top.node;
            var diags = node.GetDiagnostics();

            if (diagIndex < diags.Length - 1) {
                diagIndex++;
                var diagnostic = diags[diagIndex];

                if (diagnostic is SyntaxDiagnostic sd) {
                    var leadingWidthAlreadyCounted = node.isToken ? node.GetLeadingTriviaWidth() : 0;

                    var length = _syntaxTree.GetRoot().fullSpan.length;
                    var spanStart = Math.Min(_position - leadingWidthAlreadyCounted + sd.offset, length);
                    var spanWidth = Math.Min(spanStart + sd.width, length) - spanStart;

                    _current = new BelteDiagnostic(
                        sd.info,
                        new TextLocation(_syntaxTree.text, new TextSpan(spanStart, spanWidth)),
                        sd.message,
                        sd.suggestions
                    );
                } else {
                    _current = new BelteDiagnostic(diagnostic);
                }

                _stack.UpdateDiagnosticIndexForStackTop(diagIndex);
                return true;
            }

            var slotIndex = _stack.top.slotIndex;
tryAgain:
            if (slotIndex < node.slotCount - 1) {
                slotIndex++;
                var child = node.GetSlot(slotIndex);
                if (child is null) {
                    goto tryAgain;
                }

                if (!child.containsDiagnostics) {
                    _position += child.fullWidth;
                    goto tryAgain;
                }

                _stack.UpdateSlotIndexForStackTop(slotIndex);
                _stack.PushNodeOrToken(child);
            } else {
                if (node.slotCount == 0) {
                    _position += node.width;
                }

                _stack.Pop();
            }
        }

        return false;
    }

    /// <summary>
    /// The current diagnostic that the enumerator is pointing at.
    /// </summary>
    public BelteDiagnostic Current => _current;
}

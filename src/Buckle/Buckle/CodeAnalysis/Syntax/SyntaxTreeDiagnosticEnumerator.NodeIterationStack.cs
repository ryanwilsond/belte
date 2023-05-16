using System;

namespace Buckle.CodeAnalysis.Syntax;

internal partial struct SyntaxTreeDiagnosticEnumerator {
    private struct NodeIterationStack {
        private NodeIteration[] _stack;
        private int _count;

        internal NodeIterationStack(int capacity) {
            _stack = new NodeIteration[capacity];
            _count = 0;
        }

        internal void PushNodeOrToken(GreenNode node) {
            var token = node as Syntax.InternalSyntax.SyntaxToken;

            if (token != null)
                PushToken(token);
            else
                Push(node);
        }

        private void PushToken(Syntax.InternalSyntax.SyntaxToken token) {
            var trailing = token.GetTrailingTrivia();

            if (trailing != null)
                Push(trailing);

            Push(token);
            var leading = token.GetLeadingTrivia();

            if (leading != null)
                Push(leading);
        }

        private void Push(GreenNode node) {
            if (_count >= _stack.Length) {
                var tmp = new NodeIteration[_stack.Length * 2];
                Array.Copy(_stack, tmp, _stack.Length);
                _stack = tmp;
            }

            _stack[_count] = new NodeIteration(node);
            _count++;
        }

        internal void Pop() {
            _count--;
        }

        internal bool Any() {
            return _count > 0;
        }

        internal NodeIteration Top => this[_count - 1];

        internal NodeIteration this[int index] => _stack[index];

        internal void UpdateSlotIndexForStackTop(int slotIndex) {
            _stack[_count - 1].SlotIndex = slotIndex;
        }

        internal void UpdateDiagnosticIndexForStackTop(int diagnosticIndex) {
            _stack[_count - 1].DiagnosticIndex = diagnosticIndex;
        }
    }
}

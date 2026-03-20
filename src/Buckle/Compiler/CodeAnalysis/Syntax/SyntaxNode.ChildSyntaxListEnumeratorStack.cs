using System;
using Buckle.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Syntax;

public abstract partial class SyntaxNode {
    private struct ChildSyntaxListEnumeratorStack : IDisposable {
        private static readonly ObjectPool<ChildSyntaxList.Enumerator[]> StackPool
            = new ObjectPool<ChildSyntaxList.Enumerator[]>(() => new ChildSyntaxList.Enumerator[16]);

        private ChildSyntaxList.Enumerator[] _stack;
        private int _stackPtr;

        internal ChildSyntaxListEnumeratorStack(SyntaxNode startingNode, Func<SyntaxNode, bool> descendIntoChildren) {
            if (descendIntoChildren is null || descendIntoChildren(startingNode)) {
                _stack = StackPool.Allocate();
                _stackPtr = 0;
                _stack[0].InitializeFrom(startingNode);
            } else {
                _stack = null;
                _stackPtr = -1;
            }
        }

        internal readonly bool isNotEmpty { get { return _stackPtr >= 0; } }

        internal bool TryGetNextInSpan(in TextSpan span, out SyntaxNodeOrToken value) {
            while (_stack[_stackPtr].TryMoveNextAndGetCurrent(out value)) {
                if (IsInSpan(in span, value.fullSpan))
                    return true;
            }

            _stackPtr--;
            return false;
        }

        internal SyntaxNode? TryGetNextAsNodeInSpan(in TextSpan span) {
            SyntaxNode nodeValue;

            while ((nodeValue = _stack[_stackPtr].TryMoveNextAndGetCurrentAsNode()) is not null) {
                if (IsInSpan(in span, nodeValue.fullSpan))
                    return nodeValue;
            }

            _stackPtr--;
            return null;
        }

        internal void PushChildren(SyntaxNode node) {
            if (++_stackPtr >= _stack.Length)
                Array.Resize(ref _stack, checked(_stackPtr * 2));

            _stack[_stackPtr].InitializeFrom(node);
        }

        internal void PushChildren(SyntaxNode node, Func<SyntaxNode, bool> descendIntoChildren) {
            if (descendIntoChildren is null || descendIntoChildren(node))
                PushChildren(node);
        }

        public void Dispose() {
            if (_stack?.Length < 256) {
                Array.Clear(_stack, 0, _stack.Length);
                StackPool.Free(_stack);
            }
        }
    }
}

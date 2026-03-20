using System;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Syntax;

public abstract partial class SyntaxNode {
    private struct TriviaListEnumeratorStack : IDisposable {
        private static readonly ObjectPool<SyntaxTriviaList.Enumerator[]> StackPool
            = new ObjectPool<SyntaxTriviaList.Enumerator[]>(() => new SyntaxTriviaList.Enumerator[16]);

        private SyntaxTriviaList.Enumerator[] _stack;
        private int _stackPtr;

        internal bool TryGetNext(out SyntaxTrivia value) {
            if (_stack[_stackPtr].TryMoveNextAndGetCurrent(out value))
                return true;

            _stackPtr--;
            return false;
        }

        internal void PushLeadingTrivia(in SyntaxToken token) {
            Grow();
            _stack[_stackPtr].InitializeFromLeadingTrivia(token);
        }

        internal void PushTrailingTrivia(in SyntaxToken token) {
            Grow();
            _stack[_stackPtr].InitializeFromTrailingTrivia(token);
        }

        private void Grow() {
            if (_stack is null) {
                _stack = StackPool.Allocate();
                _stackPtr = -1;
            }

            if (++_stackPtr >= _stack.Length)
                Array.Resize(ref _stack, checked(_stackPtr * 2));
        }

        public void Dispose() {
            if (_stack?.Length < 256) {
                Array.Clear(_stack, 0, _stack.Length);
                StackPool.Free(_stack);
            }
        }
    }
}

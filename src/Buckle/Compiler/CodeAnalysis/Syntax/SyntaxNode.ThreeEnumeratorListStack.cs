using System;
using Buckle.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Syntax;

public abstract partial class SyntaxNode {
    private struct ThreeEnumeratorListStack : IDisposable {
        internal enum Which : byte {
            Node,
            Trivia,
            Token
        }

        private ChildSyntaxListEnumeratorStack _nodeStack;
        private TriviaListEnumeratorStack _triviaStack;
        private readonly ArrayBuilder<SyntaxNodeOrToken> _tokenStack;
        private readonly ArrayBuilder<Which> _discriminatorStack;

        internal ThreeEnumeratorListStack(SyntaxNode startingNode, Func<SyntaxNode, bool> descendIntoChildren) {
            _nodeStack = new ChildSyntaxListEnumeratorStack(startingNode, descendIntoChildren);
            _triviaStack = new TriviaListEnumeratorStack();

            if (_nodeStack.isNotEmpty) {
                _tokenStack = ArrayBuilder<SyntaxNodeOrToken>.GetInstance();
                _discriminatorStack = ArrayBuilder<Which>.GetInstance();
                _discriminatorStack.Push(Which.Node);
            } else {
                _tokenStack = null;
                _discriminatorStack = null;
            }
        }

        internal readonly bool isNotEmpty { get { return _discriminatorStack?.Count > 0; } }

        internal Which PeekNext() {
            return _discriminatorStack.Peek();
        }

        internal bool TryGetNextInSpan(in TextSpan span, out SyntaxNodeOrToken value) {
            if (_nodeStack.TryGetNextInSpan(in span, out value))
                return true;

            _discriminatorStack.Pop();
            return false;
        }

        internal bool TryGetNext(out SyntaxTrivia value) {
            if (_triviaStack.TryGetNext(out value))
                return true;

            _discriminatorStack.Pop();
            return false;
        }

        internal SyntaxNodeOrToken PopToken() {
            _discriminatorStack.Pop();
            return _tokenStack.Pop();
        }

        internal void PushChildren(SyntaxNode node, Func<SyntaxNode, bool> descendIntoChildren) {
            if (descendIntoChildren is null || descendIntoChildren(node)) {
                _nodeStack.PushChildren(node);
                _discriminatorStack.Push(Which.Node);
            }
        }

        internal void PushLeadingTrivia(in SyntaxToken token) {
            _triviaStack.PushLeadingTrivia(in token);
            _discriminatorStack.Push(Which.Trivia);
        }

        internal void PushTrailingTrivia(in SyntaxToken token) {
            _triviaStack.PushTrailingTrivia(in token);
            _discriminatorStack.Push(Which.Trivia);
        }

        internal void PushToken(in SyntaxNodeOrToken value) {
            _tokenStack.Push(value);
            _discriminatorStack.Push(Which.Token);
        }

        public void Dispose() {
            _nodeStack.Dispose();
            _triviaStack.Dispose();
            _tokenStack?.Free();
            _discriminatorStack?.Free();
        }
    }
}

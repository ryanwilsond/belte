using System;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.Tests.CodeAnalysis.Syntax;

internal sealed class AssertingEnumerator : IDisposable {
    private readonly IEnumerator<SyntaxNode> _enumerator;
    private bool _hasErrors;

    public AssertingEnumerator(SyntaxNode node) {
        _enumerator = Flatten(node).GetEnumerator();
    }

    private bool MarkFailed() {
        _hasErrors = true;
        return false;
    }

    public void Dispose() {
        if (!_hasErrors)
            Assert.False(_enumerator.MoveNext());

        _enumerator.Dispose();
    }

    private static IEnumerable<SyntaxNode> Flatten(SyntaxNode node) {
        var stack = new Stack<SyntaxNode>();
        stack.Push(node);

        while (stack.Count > 0) {
            var n = stack.Pop();
            yield return n;
            IEnumerable<SyntaxNode> nodeList = n.GetChildren();

            foreach (var child in nodeList.Reverse())
                stack.Push(child);
        }
    }

    public void AssertNode(SyntaxKind kind) {
        try {
            Assert.True(_enumerator.MoveNext());
            Assert.Equal(kind, _enumerator.Current.kind);
            Assert.IsNotType<SyntaxToken>(_enumerator.Current);
        } catch when (MarkFailed()) {
            throw;
        }
    }

    public void AssertToken(SyntaxKind kind, string text) {
        try {
            Assert.True(_enumerator.MoveNext());
            Assert.Equal(kind, _enumerator.Current.kind);
            var token = Assert.IsType<SyntaxToken>(_enumerator.Current);
            Assert.Equal(text, token.text);
        } catch when (MarkFailed()) {
            throw;
        }
    }
}

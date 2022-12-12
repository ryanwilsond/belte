using System;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.Tests.CodeAnalysis.Syntax;

internal sealed class AssertingEnumerator : IDisposable {
    private readonly IEnumerator<Node> _enumerator;
    private bool _hasErrors;

    public AssertingEnumerator(Node node) {
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

    private static IEnumerable<Node> Flatten(Node node) {
        var stack = new Stack<Node>();
        stack.Push(node);

        while (stack.Count > 0) {
            var n = stack.Pop();
            yield return n;
            IEnumerable<Node> nodeList = n.GetChildren();

            foreach (var child in nodeList.Reverse())
                stack.Push(child);
        }
    }

    public void AssertNode(SyntaxType type) {
        try {
            Assert.True(_enumerator.MoveNext());
            Assert.Equal(type, _enumerator.Current.type);
            Assert.IsNotType<Token>(_enumerator.Current);
        } catch when (MarkFailed()) {
            throw;
        }
    }

    public void AssertToken(SyntaxType type, string text) {
        try {
            Assert.True(_enumerator.MoveNext());
            Assert.Equal(type, _enumerator.Current.type);
            var token = Assert.IsType<Token>(_enumerator.Current);
            Assert.Equal(text, token.text);
        } catch when (MarkFailed()) {
            throw;
        }
    }
}

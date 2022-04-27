using System;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.Tests.CodeAnalysis.Syntax;

internal sealed class AssertingEnumerator : IDisposable {
    private readonly IEnumerator<Node> enumerator_;
    private bool hasErrors_;

    public AssertingEnumerator(Node node) {
        enumerator_ = Flatten(node).GetEnumerator();
    }

    private bool MarkFailed() {
        hasErrors_ = true;
        return false;
    }

    public void Dispose() {
        if (!hasErrors_)
            Assert.False(enumerator_.MoveNext());

        enumerator_.Dispose();
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
            Assert.True(enumerator_.MoveNext());
            Assert.Equal(type, enumerator_.Current.type);
            Assert.IsNotType<Token>(enumerator_.Current);
        } catch when (MarkFailed()) {
            throw;
        }
    }

    public void AssertToken(SyntaxType type, string text) {
        try {
            Assert.True(enumerator_.MoveNext());
            Assert.Equal(type, enumerator_.Current.type);
            var token = Assert.IsType<Token>(enumerator_.Current);
            Assert.Equal(text, token.text);
        } catch when (MarkFailed()) {
            throw;
        }
    }
}

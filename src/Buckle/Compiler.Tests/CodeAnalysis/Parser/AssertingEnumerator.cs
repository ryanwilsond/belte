using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Syntax;
using Xunit;

namespace Buckle.Tests.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Asserts SyntaxKinds over an enumerable of SyntaxNodes.
/// </summary>
internal sealed class AssertingEnumerator : IDisposable {
    private readonly IEnumerator<SyntaxNodeOrToken> _enumerator;
    private bool _hasErrors;

    internal AssertingEnumerator(SyntaxNode node) {
        _enumerator = Flatten(node).GetEnumerator();
    }

    public void Dispose() {
        if (!_hasErrors)
            Assert.False(_enumerator.MoveNext());

        _enumerator.Dispose();
    }

    /// <summary>
    /// Asserts the next element is a specific <see cref="SyntaxKind" />.
    /// </summary>
    /// <param name="kind"><see cref="SyntaxKind" /> to assert on.</param>
    internal void AssertNode(SyntaxKind kind) {
        try {
            Assert.True(_enumerator.MoveNext());
            Assert.Equal(kind, _enumerator.Current.kind);
            Assert.IsNotType<SyntaxToken>(_enumerator.Current);
        } catch when (MarkFailed()) {
            throw;
        }
    }

    /// <summary>
    /// Asserts the next element is a <see cref="SyntaxToken" />, is a specific <see cref="SyntaxKind" />, and the
    /// token's text matches.
    /// </summary>
    /// <param name="kind"><see cref="SyntaxKind" /> to assert on.</param>
    /// <param name="text">Text to assert on.</param>
    internal void AssertToken(SyntaxKind kind, string text) {
        try {
            Assert.True(_enumerator.MoveNext());
            Assert.Equal(kind, _enumerator.Current.kind);
            var token = Assert.IsType<SyntaxToken>(_enumerator.Current.AsToken());
            Assert.Equal(text, token.text);
        } catch when (MarkFailed()) {
            throw;
        }
    }

    private bool MarkFailed() {
        _hasErrors = true;
        return false;
    }

    private static IEnumerable<SyntaxNodeOrToken> Flatten(SyntaxNode node) {
        var stack = new Stack<SyntaxNodeOrToken>();
        stack.Push(node);

        while (stack.Count > 0) {
            var n = stack.Pop();
            yield return n;

            if (n.isToken)
                continue;

            var nodeList = n.ChildNodesAndTokens();

            foreach (var child in nodeList.Reverse())
                stack.Push(child);
        }
    }
}

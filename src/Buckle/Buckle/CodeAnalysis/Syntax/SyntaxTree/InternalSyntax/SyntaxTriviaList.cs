using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Represents multiple SyntaxTrivias.
/// </summary>
internal sealed class SyntaxTriviaList : IEnumerable<SyntaxTrivia> {
    internal SyntaxTriviaList(ImmutableArray<SyntaxTrivia> trivia) {
        this.trivia = trivia;
    }

    internal int count => trivia.Length;

    internal ImmutableArray<SyntaxTrivia> trivia { get; }

    internal SyntaxTrivia this[int index] => trivia[index];

    internal int fullWidth {
        get {
            int width = 0;

            foreach (var childTrivia in trivia)
                width += childTrivia.fullWidth;

            return width;
        }
    }

    public IEnumerator<SyntaxTrivia> GetEnumerator() {
        for (var i = 0; i < count; i++)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}

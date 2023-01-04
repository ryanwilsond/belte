using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

internal static partial class SyntaxFactory {
    internal static SyntaxToken Token(SyntaxTree syntaxTree, SyntaxKind kind, int position) {
        return new SyntaxToken(
            syntaxTree, kind, position, null, null,
            ImmutableArray<SyntaxTrivia>.Empty, ImmutableArray<SyntaxTrivia>.Empty
        );
    }
}

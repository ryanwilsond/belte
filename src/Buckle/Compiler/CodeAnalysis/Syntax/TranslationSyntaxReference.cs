using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

internal abstract class TranslationSyntaxReference : SyntaxReference {
    private readonly SyntaxReference _reference;

    private protected TranslationSyntaxReference(SyntaxReference reference) {
        _reference = reference;
    }

    internal sealed override TextSpan span => _reference.span;

    internal sealed override SyntaxTree syntaxTree => _reference.syntaxTree;

    internal sealed override SyntaxNode node => Translate(_reference);

    private protected abstract SyntaxNode Translate(SyntaxReference reference);
}

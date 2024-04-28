
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed class DirectiveParser : SyntaxParser {
    private readonly DirectiveStack _context;

    internal DirectiveParser(Lexer lexer, DirectiveStack context)
        : base(lexer, LexerMode.Directive, null, null, false) {
        _context = context;
    }

    internal BelteSyntaxNode ParseDirective(bool isAfterFirstTokenInFile, bool isAfterNonWhitespaceOnLine) {
        // TODO Implement after directives are added
        var position = _lexer.position;
        var hash = Match(SyntaxKind.HashToken);

        if (isAfterNonWhitespaceOnLine)
            ; // hash = AddDiagnostic(hash, Error.InvalidDirectivePlacement());

        BelteSyntaxNode result;
        switch (currentToken.kind) {
            default:
                var identifier = Match(SyntaxKind.IdentifierName, report: false);
                var end = ParseEndOfDirective(true);
                result = SyntaxFactory.BadDirectiveTrivia(hash, identifier, end);
                break;
        }

        return result;
    }

    private SyntaxToken ParseEndOfDirective(bool ignoreErrors) {
        var skippedTokens = new SyntaxListBuilder<SyntaxToken>();

        if (currentToken.kind != SyntaxKind.EndOfDirectiveToken &&
            currentToken.kind != SyntaxKind.EndOfFileToken) {
            skippedTokens = new SyntaxListBuilder<SyntaxToken>(10);

            while (currentToken.kind != SyntaxKind.EndOfDirectiveToken &&
                   currentToken.kind != SyntaxKind.EndOfFileToken) {
                skippedTokens.Add(EatToken().WithoutDiagnosticsGreen());
            }
        }

        var endOfDirective = currentToken.kind == SyntaxKind.EndOfDirectiveToken
            ? EatToken()
            : SyntaxFactory.Token(SyntaxKind.EndOfDirectiveToken);

        if (!skippedTokens.isNull) {
            endOfDirective = endOfDirective.TokenWithLeadingTrivia(
                SyntaxFactory.SkippedTokensTrivia(skippedTokens.ToList()));
        }

        return endOfDirective;
    }
}

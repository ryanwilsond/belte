using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed class DirectiveParser : SyntaxParser {
    private readonly DirectiveStack _context;

    internal DirectiveParser(Lexer lexer, DirectiveStack context)
        : base(lexer, LexerMode.Directive, null, null, false) {
        _context = context;
    }

    internal BelteSyntaxNode ParseDirective(
        bool isActive,
        bool endIsActive,
        bool isAfterFirstTokenInFile,
        bool isAfterNonWhitespaceOnLine) {
        var hash = Match(SyntaxKind.HashToken);

        if (isAfterNonWhitespaceOnLine)
            hash = AddDiagnostic(hash, Error.InvalidDirectivePlacement());

        BelteSyntaxNode result;
        switch (currentToken.kind) {
            case SyntaxKind.IfKeyword:
                result = ParseIfDirective(hash, EatToken(), isActive);
                break;
            case SyntaxKind.ElifKeyword:
                result = ParseElifDirective(hash, EatToken(), isActive, endIsActive);
                break;
            case SyntaxKind.ElseKeyword:
                result = ParseElseDirective(hash, EatToken(), isActive, endIsActive);
                break;
            case SyntaxKind.EndifKeyword:
                result = ParseEndIfDirective(hash, EatToken(), isActive, endIsActive);
                break;
            case SyntaxKind.DefineKeyword:
            case SyntaxKind.UndefKeyword:
                result = ParseDefineOrUndefDirective(
                    hash,
                    EatToken(),
                    isActive,
                    isAfterFirstTokenInFile && !isAfterNonWhitespaceOnLine
                );

                break;
            case SyntaxKind.HandleKeyword:
                result = ParseHandleDirective(hash, EatToken(), isActive);
                break;
            default:
                var identifier = Match(SyntaxKind.IdentifierToken);
                var end = ParseEndOfDirective();
                result = SyntaxFactory.BadDirectiveTrivia(hash, identifier, end, isActive);
                break;
        }

        return result;
    }

    private DirectiveTriviaSyntax ParseHandleDirective(SyntaxToken hash, SyntaxToken keyword, bool isActive) {
        var identifier = Match(SyntaxKind.IdentifierToken);
        var eod = ParseEndOfDirective();
        return SyntaxFactory.HandleDirectiveTrivia(hash, keyword, identifier, eod, isActive);
    }

    private SyntaxToken ParseEndOfDirective() {
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

    private DirectiveTriviaSyntax ParseElseDirective(SyntaxToken hash, SyntaxToken keyword, bool isActive, bool endIsActive) {
        var eod = ParseEndOfDirective();

        if (_context.HasPreviousIfOrElif()) {
            var branchTaken = endIsActive && !_context.PreviousBranchTaken();
            return SyntaxFactory.ElseDirectiveTrivia(hash, keyword, eod, endIsActive, branchTaken);
        } else if (_context.HasUnfinishedIf()) {
            return AddDiagnostic(
                SyntaxFactory.BadDirectiveTrivia(hash, keyword, eod, isActive),
                Error.EndifDirectiveExpected()
            );
        } else {
            return AddDiagnostic(
                SyntaxFactory.BadDirectiveTrivia(hash, keyword, eod, isActive),
                Error.UnexpectedDirective()
            );
        }
    }

    private DirectiveTriviaSyntax ParseEndIfDirective(SyntaxToken hash, SyntaxToken keyword, bool isActive, bool endIsActive) {
        var eod = ParseEndOfDirective();

        if (_context.HasUnfinishedIf()) {
            return SyntaxFactory.EndIfDirectiveTrivia(hash, keyword, eod, endIsActive);
        } else {
            return AddDiagnostic(
                SyntaxFactory.BadDirectiveTrivia(hash, keyword, eod, isActive),
                Error.UnexpectedDirective()
            );
        }
    }

    private DirectiveTriviaSyntax ParseIfDirective(SyntaxToken hash, SyntaxToken keyword, bool isActive) {
        var expr = ParseExpression();
        var eod = ParseEndOfDirective();
        var isTrue = EvaluateBool(expr);
        var branchTaken = isTrue;
        return SyntaxFactory.IfDirectiveTrivia(hash, keyword, expr, eod, isActive, branchTaken, isTrue);
    }

    private DirectiveTriviaSyntax ParseElifDirective(SyntaxToken hash, SyntaxToken keyword, bool isActive, bool endIsActive) {
        var expr = ParseExpression();
        var eod = ParseEndOfDirective();

        if (_context.HasPreviousIfOrElif()) {
            var isTrue = EvaluateBool(expr);
            var branchTaken = endIsActive && isTrue && !_context.PreviousBranchTaken();
            return SyntaxFactory.ElifDirectiveTrivia(hash, keyword, expr, eod, endIsActive, branchTaken, isTrue);
        } else {
            eod = eod.TokenWithLeadingTrivia(
                SyntaxList.Concat(SyntaxFactory.DisabledText(expr.ToString()), eod.GetLeadingTrivia())
            );

            if (_context.HasUnfinishedIf()) {
                return AddDiagnostic(
                    SyntaxFactory.BadDirectiveTrivia(hash, keyword, eod, isActive),
                    Error.EndifDirectiveExpected()
                );
            } else {
                return AddDiagnostic(
                    SyntaxFactory.BadDirectiveTrivia(hash, keyword, eod, isActive),
                    Error.UnexpectedDirective()
                );
            }
        }
    }

    private DirectiveTriviaSyntax ParseDefineOrUndefDirective(
        SyntaxToken hash,
        SyntaxToken keyword,
        bool isActive,
        bool isFollowingToken) {
        if (isFollowingToken)
            keyword = AddDiagnostic(keyword, Error.DirectiveFollowsToken());

        var name = Match(SyntaxKind.IdentifierToken);
        var end = ParseEndOfDirective();

        if (keyword.kind == SyntaxKind.DefineKeyword)
            return SyntaxFactory.DefineDirectiveTrivia(hash, keyword, name, end, isActive);
        else
            return SyntaxFactory.UndefDirectiveTrivia(hash, keyword, name, end, isActive);
    }

    private ExpressionSyntax ParseExpression() {
        return ParseLogicalOr();
    }

    private ExpressionSyntax ParseLogicalOr() {
        var left = ParseLogicalAnd();

        while (currentToken.kind == SyntaxKind.PipePipeToken) {
            var op = EatToken();
            var right = ParseLogicalAnd();
            left = SyntaxFactory.BinaryExpression(left, op, right);
        }

        return left;
    }

    private ExpressionSyntax ParseLogicalAnd() {
        var left = ParseEquality();

        while (currentToken.kind == SyntaxKind.AmpersandAmpersandToken) {
            var op = EatToken();
            var right = ParseEquality();
            left = SyntaxFactory.BinaryExpression(left, op, right);
        }

        return left;
    }

    private ExpressionSyntax ParseEquality() {
        var left = ParseLogicalNot();

        while (currentToken.kind is SyntaxKind.EqualsEqualsToken or SyntaxKind.ExclamationEqualsToken) {
            var op = EatToken();
            var right = ParseEquality();
            left = SyntaxFactory.BinaryExpression(left, op, right);
        }

        return left;
    }

    private ExpressionSyntax ParseLogicalNot() {
        if (currentToken.kind == SyntaxKind.ExclamationToken) {
            var op = EatToken();
            return SyntaxFactory.UnaryExpression(op, ParseLogicalNot());
        }

        return ParsePrimary();
    }

    private ExpressionSyntax ParsePrimary() {
        var k = currentToken.kind;

        switch (k) {
            case SyntaxKind.OpenParenToken:
                var open = EatToken();
                var expr = ParseExpression();
                var close = Match(SyntaxKind.CloseParenToken);
                return SyntaxFactory.ParenthesisExpression(open, expr, close);
            case SyntaxKind.IdentifierToken:
                var identifier = EatToken();
                return SyntaxFactory.IdentifierName(identifier);
            case SyntaxKind.TrueKeyword:
            case SyntaxKind.FalseKeyword:
                return SyntaxFactory.LiteralExpression(EatToken());
            default:
                return SyntaxFactory.IdentifierName(
                    WithAdditionalDiagnostics(Match(SyntaxKind.IdentifierToken), Error.InvalidDirectiveExpression())
                );
        }
    }

    private bool EvaluateBool(ExpressionSyntax expr) {
        var result = Evaluate(expr);

        if (result is bool v)
            return v;

        return false;
    }

    private object Evaluate(ExpressionSyntax expr) {
        switch (expr.kind) {
            case SyntaxKind.ParenthesizedExpression:
                return Evaluate(((ParenthesisExpressionSyntax)expr).expression);
            case SyntaxKind.LiteralExpression:
                return ((LiteralExpressionSyntax)expr).token.value;
            case SyntaxKind.BinaryExpression:
                var op = ((BinaryExpressionSyntax)expr).operatorToken;

                switch (op.kind) {
                    case SyntaxKind.AmpersandAmpersandToken:
                    case SyntaxKind.AmpersandToken:
                        return EvaluateBool(((BinaryExpressionSyntax)expr).left) && EvaluateBool(((BinaryExpressionSyntax)expr).right);
                    case SyntaxKind.PipePipeToken:
                    case SyntaxKind.PipeToken:
                        return EvaluateBool(((BinaryExpressionSyntax)expr).left) || EvaluateBool(((BinaryExpressionSyntax)expr).right);
                    case SyntaxKind.EqualsToken:
                        return Equals(Evaluate(((BinaryExpressionSyntax)expr).left), Evaluate(((BinaryExpressionSyntax)expr).right));
                    case SyntaxKind.ExclamationEqualsToken:
                        return !Equals(Evaluate(((BinaryExpressionSyntax)expr).left), Evaluate(((BinaryExpressionSyntax)expr).right));
                }

                break;
            case SyntaxKind.UnaryExpression:
                if (((UnaryExpressionSyntax)expr).operand.kind == SyntaxKind.ExclamationToken)
                    return !EvaluateBool(((UnaryExpressionSyntax)expr).operand);

                break;
            case SyntaxKind.IdentifierName:
                var id = ((IdentifierNameSyntax)expr).identifier.text;

                if (bool.TryParse(id, out var constantValue))
                    return constantValue;

                return IsDefined(id);
        }

        return false;
    }

    private bool IsDefined(string id) {
        var defState = _context.IsDefined(id);

        switch (defState) {
            default:
            case DefineState.Unspecified:
                return options.preprocessorSymbols.Contains(id);
            case DefineState.Defined:
                return true;
            case DefineState.Undefined:
                return false;
        }
    }
}

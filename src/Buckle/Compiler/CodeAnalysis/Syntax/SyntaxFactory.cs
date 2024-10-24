using System.Collections.Generic;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Class used for handling the creation of red SyntaxNodes.
/// </summary>
public static partial class SyntaxFactory {
    /// <summary>
    /// Creates a <see cref="SyntaxToken" /> with text.
    /// </summary>
    public static SyntaxToken Token(SyntaxKind kind) {
        return new SyntaxToken(InternalSyntax.SyntaxFactory.Token(kind));
    }

    /// <summary>
    /// Creates a <see cref="SyntaxToken" /> with text.
    /// </summary>
    public static SyntaxToken Token(SyntaxKind kind, string text) {
        return new SyntaxToken(InternalSyntax.SyntaxFactory.Token(kind, text));
    }

    /// <summary>
    /// Creates a <see cref="SyntaxToken" /> with text.
    /// </summary>
    public static SyntaxToken Token(SyntaxKind kind, string text, object value) {
        return new SyntaxToken(InternalSyntax.SyntaxFactory.Token(kind, text, value));
    }

    /// <summary>
    /// Creates a <see cref="SyntaxToken" /> with kind <see cref="SyntaxKind.IdentifierToken"/>.
    /// </summary>
    public static SyntaxToken Identifier(string text) {
        return Token(SyntaxKind.IdentifierToken, text);
    }

    /// <summary>
    /// Creates a <see cref="LiteralExpressionSyntax"/>.
    /// </summary>
    public static LiteralExpressionSyntax Literal(object value) {
        if (value is int or double) {
            return LiteralExpression(Token(SyntaxKind.NumericLiteralToken, value.ToString(), value));
        } else if (value is bool b) {
            return LiteralExpression(
                Token(b ? SyntaxKind.TrueKeyword : SyntaxKind.FalseKeyword, b ? "true" : "false", b)
            );
        } else if (value is string s) {
            return LiteralExpression(Token(SyntaxKind.StringLiteralToken, s, s));
        } else {
            throw ExceptionUtilities.Unreachable();
        }
    }

    /// <summary>
    /// Creates an <see cref="IdentifierNameSyntax" />.
    /// </summary>
    public static IdentifierNameSyntax IdentifierName(string name) {
        return IdentifierName(Identifier(name));
    }

    /// <summary>
    /// Creates a <see cref="NonNullableTypeSyntax" />.
    /// </summary>
    public static NonNullableTypeSyntax NonNullableType(string name) {
        return NonNullableType(IdentifierName(name), Token(SyntaxKind.ExclamationToken));
    }

    /// <summary>
    /// Creates a <see cref="ReferenceTypeSyntax" />.
    /// </summary>
    public static ReferenceTypeSyntax ReferenceType(string name) {
        return ReferenceType(Token(SyntaxKind.RefKeyword), IdentifierName(name));
    }

    /// <summary>
    /// Creates an empty <see cref="TemplateParameterListSyntax" />.
    /// </summary>
    public static TemplateParameterListSyntax TemplateParameterList() {
        return TemplateParameterList(
            Token(SyntaxKind.LessThanToken),
            SeparatedList<ParameterSyntax>(),
            Token(SyntaxKind.GreaterThanToken)
        );
    }

    /// <summary>
    /// Creates an empty <see cref="TemplateConstraintClauseListSyntax" />.
    /// </summary>
    public static TemplateConstraintClauseListSyntax ConstraintClauseList() {
        return TemplateConstraintClauseList(
            Token(SyntaxKind.WhereKeyword),
            Token(SyntaxKind.OpenBraceToken),
            List<TemplateConstraintClauseSyntax>(),
            Token(SyntaxKind.CloseBraceToken)
        );
    }

    /// <summary>
    /// Creates a <see cref="ReferenceExpressionSyntax" />.
    /// </summary>
    public static ReferenceExpressionSyntax Reference(string name) {
        return (ReferenceExpressionSyntax)InternalSyntax.SyntaxFactory.ReferenceExpression(
            InternalSyntax.SyntaxFactory.Token(SyntaxKind.RefKeyword),
            InternalSyntax.SyntaxFactory.IdentifierName(
                InternalSyntax.SyntaxFactory.Token(SyntaxKind.IdentifierToken, name)
            )
        ).CreateRed();
    }

    /// <summary>
    /// Creates a <see cref="ThisExpressionSyntax" />.
    /// </summary>
    public static ThisExpressionSyntax This() {
        return ThisExpression(Token(SyntaxKind.ThisKeyword));
    }

    /// <summary>
    /// Creates a <see cref="BlockStatementSyntax" />.
    /// </summary>
    public static BlockStatementSyntax Block(params StatementSyntax[] statements) {
        return BlockStatement(
            Token(SyntaxKind.OpenBraceToken),
            List(statements),
            Token(SyntaxKind.CloseBraceToken)
        );
    }

    /// <summary>
    /// Creates a <see cref="ReturnStatementSyntax" />.
    /// </summary>
    public static ReturnStatementSyntax Return(ExpressionSyntax expression) {
        return ReturnStatement(Token(SyntaxKind.ReturnKeyword), expression, Token(SyntaxKind.SemicolonToken));
    }

    /// <summary>
    /// Creates an <see cref="IfStatementSyntax" />.
    /// </summary>
    public static IfStatementSyntax If(ExpressionSyntax condition, StatementSyntax then) {
        return IfStatement(
            Token(SyntaxKind.IfKeyword),
            Token(SyntaxKind.OpenParenToken),
            condition,
            Token(SyntaxKind.CloseParenToken),
            then
        );
    }

    /// <summary>
    /// Creates an empty syntax list.
    /// </summary>
    public static SyntaxList<T> List<T>() where T : SyntaxNode {
        return new SyntaxList<T>((T)null);
    }

    /// <summary>
    /// Creates a syntax list.
    /// </summary>
    public static SyntaxList<T> List<T>(params T[] nodes) where T : SyntaxNode {
        return new SyntaxList<T>(nodes);
    }

    /// <summary>
    /// Creates a syntax list.
    /// </summary>
    public static SyntaxList<T> List<T>(IEnumerable<T> nodes) where T : SyntaxNode {
        return new SyntaxList<T>(nodes);
    }

    /// <summary>
    /// Creates an empty separated syntax list.
    /// </summary>
    public static SeparatedSyntaxList<T> SeparatedList<T>() where T : SyntaxNode {
        return new SeparatedSyntaxList<T>(new SyntaxNodeOrTokenList(null, 0));
    }

    /// <summary>
    /// Creates a separated syntax list.
    /// </summary>
    public static SeparatedSyntaxList<T> SeparatedList<T>(params T[] nodes) where T : SyntaxNode {
        return SeparatedList(nodes as IEnumerable<T>);
    }

    /// <summary>
    /// Creates a separated syntax list.
    /// </summary>
    public static SeparatedSyntaxList<T> SeparatedList<T>(IEnumerable<T> nodes) where T : SyntaxNode {
        if (nodes is not ICollection<T> collection || collection.Count == 0)
            return new SeparatedSyntaxList<T>(new SyntaxNodeOrTokenList(null, 0));

        using var enumerator = nodes.GetEnumerator();

        if (!enumerator.MoveNext())
            return new SeparatedSyntaxList<T>(new SyntaxNodeOrTokenList(null, 0));

        var firstNode = enumerator.Current;

        if (!enumerator.MoveNext())
            return new SeparatedSyntaxList<T>(new SyntaxNodeOrTokenList(firstNode, 0));

        var builder = new SeparatedSyntaxListBuilder<T>(collection.Count);
        builder.Add(firstNode);

        var comma = Token(SyntaxKind.CommaToken);

        do {
            builder.AddSeparator(comma);
            builder.Add(enumerator.Current);
        } while (enumerator.MoveNext());

        return builder.ToList();
    }

    /// <summary>
    /// Creates a syntax token list.
    /// </summary>
    public static SyntaxTokenList TokenList(params SyntaxToken[] tokens) {
        return new SyntaxTokenList(tokens);
    }

    /// <summary>
    /// Creates a syntax token list.
    /// </summary>
    public static SyntaxTokenList TokenList(IEnumerable<SyntaxToken> tokens) {
        return new SyntaxTokenList(tokens);
    }

    /// <summary>
    /// Creates an argument list.
    /// </summary>
    public static ArgumentListSyntax ArgumentList(params ArgumentSyntax[] arguments) {
        return ArgumentList(
            Token(SyntaxKind.OpenParenToken),
            SeparatedList(arguments),
            Token(SyntaxKind.CloseParenToken)
        );
    }
}

using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

internal abstract class Expression : Node {
    protected Expression(SyntaxTree syntaxTree) : base(syntaxTree) { }
}

internal sealed partial class LiteralExpression : Expression {
    internal Token token { get; }
    internal object value { get; }
    internal override SyntaxType type => SyntaxType.LITERAL_EXPRESSION;

    internal LiteralExpression(SyntaxTree syntaxTree, Token token_, object value_) : base(syntaxTree) {
        token = token_;
        value = value_;
    }

    internal LiteralExpression(SyntaxTree syntaxTree, Token token_) : this(syntaxTree, token_, token_.value) { }
}

internal sealed partial class BinaryExpression : Expression {
    internal Expression left { get; }
    internal Token op { get; }
    internal Expression right { get; }
    internal override SyntaxType type => SyntaxType.BINARY_EXPRESSION;

    internal BinaryExpression(SyntaxTree syntaxTree, Expression left_, Token op_, Expression right_)
        : base(syntaxTree) {
        left = left_;
        op = op_;
        right = right_;
    }
}

internal sealed partial class PostfixExpression : Expression {
    internal Token identifier { get; }
    internal Token op { get; }
    internal override SyntaxType type => SyntaxType.POSTFIX_EXPRESSION;

    internal PostfixExpression(SyntaxTree syntaxTree, Token identifier_, Token op_)
        : base(syntaxTree) {
        identifier = identifier_;
        op = op_;
    }
}

internal sealed partial class PrefixExpression : Expression {
    internal Token op { get; }
    internal Token identifier { get; }
    internal override SyntaxType type => SyntaxType.PREFIX_EXPRESSION;

    internal PrefixExpression(SyntaxTree syntaxTree, Token op_, Token identifier_)
        : base(syntaxTree) {
        op = op_;
        identifier = identifier_;
    }
}
internal sealed partial class ParenthesisExpression : Expression {
    internal Token? openParenthesis { get; }
    internal Expression expression { get; }
    internal Token? closeParenthesis { get; }
    internal override SyntaxType type => SyntaxType.PARENTHESIZED_EXPRESSION;

    internal ParenthesisExpression(
        SyntaxTree syntaxTree, Token openParenthesis_, Expression expression_, Token closeParenthesis_)
        : base(syntaxTree) {
        openParenthesis = openParenthesis_;
        expression = expression_;
        closeParenthesis = closeParenthesis_;
    }
}

internal sealed partial class UnaryExpression : Expression {
    internal Token op { get; }
    internal Expression operand { get; }
    internal override SyntaxType type => SyntaxType.UNARY_EXPRESSION;

    internal UnaryExpression(SyntaxTree syntaxTree, Token op_, Expression operand_) : base(syntaxTree) {
        op = op_;
        operand = operand_;
    }
}

internal sealed partial class NameExpression : Expression {
    internal Token identifier { get; }
    internal override SyntaxType type => SyntaxType.NAME_EXPRESSION;

    internal NameExpression(SyntaxTree syntaxTree, Token identifier_) : base(syntaxTree) {
        identifier = identifier_;
    }
}

internal sealed partial class AssignmentExpression : Expression {
    internal Token identifier { get; }
    internal Token assignmentToken { get; }
    internal Expression expression { get; }
    internal override SyntaxType type => SyntaxType.ASSIGN_EXPRESSION;

    internal AssignmentExpression(SyntaxTree syntaxTree, Token identifier_, Token assignmentToken_, Expression expression_)
        : base(syntaxTree) {
        identifier = identifier_;
        assignmentToken = assignmentToken_;
        expression = expression_;
    }
}

internal sealed partial class EmptyExpression : Expression {
    internal override SyntaxType type => SyntaxType.EMPTY_EXPRESSION;

    internal EmptyExpression(SyntaxTree syntaxTree) : base(syntaxTree) { }
}

internal sealed partial class CallExpression : Expression {
    internal NameExpression identifier { get; }
    internal Token? openParenthesis { get; }
    internal SeparatedSyntaxList<Expression> arguments { get; }
    internal Token? closeParenthesis { get; }
    internal override SyntaxType type => SyntaxType.CALL_EXPRESSION;

    internal CallExpression(
        SyntaxTree syntaxTree, NameExpression identifier_, Token openParenthesis_,
        SeparatedSyntaxList<Expression> arguments_, Token closeParenthesis_)
        : base(syntaxTree) {
        identifier = identifier_;
        openParenthesis = openParenthesis_;
        arguments = arguments_;
        closeParenthesis = closeParenthesis_;
    }
}

internal sealed partial class  IndexExpression : Expression {
    internal Expression operand { get; }
    internal Token? openBracket { get; }
    internal Expression index { get; }
    internal Token? closeBracket { get; }
    internal override SyntaxType type => SyntaxType.INDEX_EXPRESSION;

    internal IndexExpression(
        SyntaxTree syntaxTree, Expression operand_, Token openBracket_, Expression index_, Token closeBracket_)
        : base(syntaxTree) {
        operand = operand_;
        openBracket = openBracket_;
        index = index_;
        closeBracket = closeBracket_;
    }
}

internal sealed partial class InitializerListExpression : Expression {
    internal Token? openBrace { get; }
    internal SeparatedSyntaxList<Expression> items { get; }
    internal Token? closeBrace { get; }
    internal override SyntaxType type => SyntaxType.LITERAL_EXPRESSION;

    internal InitializerListExpression(SyntaxTree syntaxTree,
        Token openBrace_, SeparatedSyntaxList<Expression> items_, Token closeBrace_)
        : base(syntaxTree) {
        openBrace = openBrace_;
        items = items_;
        closeBrace = closeBrace_;
    }
}

internal sealed partial class ReferenceExpression : Expression {
    internal Token refKeyword { get; }
    internal Token identifier { get; }
    internal override SyntaxType type => SyntaxType.REFERENCE_EXPRESSION;

    internal ReferenceExpression(SyntaxTree syntaxTree, Token refKeyword_, Token identifier_) : base(syntaxTree) {
        refKeyword = refKeyword_;
        identifier = identifier_;
    }
}

internal sealed partial class InlineFunctionExpression : Expression {
    internal Token? openBrace { get; }
    internal ImmutableArray<Statement> statements { get; }
    internal Token? closeBrace { get; }
    internal override SyntaxType type => SyntaxType.INLINE_FUNCTION;

    internal InlineFunctionExpression(
        SyntaxTree syntaxTree, Token openBrace_, ImmutableArray<Statement> statements_, Token closeBrace_)
        : base(syntaxTree) {
        openBrace = openBrace_;
        statements = statements_;
        closeBrace = closeBrace_;
    }
}

internal sealed partial class CastExpression : Expression {
    internal Token? openParenthesis { get; }
    internal TypeClause typeClause { get; }
    internal Token? closeParenthesis { get; }
    internal Expression expression { get; }
    internal override SyntaxType type => SyntaxType.CAST_EXPRESSION;

    internal CastExpression(
        SyntaxTree syntaxTree, Token openParenthesis_, TypeClause typeClause_,
        Token closeParenthesis_, Expression expression_)
        : base(syntaxTree) {
        openParenthesis = openParenthesis_;
        typeClause = typeClause_;
        closeParenthesis = closeParenthesis_;
        expression = expression_;
    }
}

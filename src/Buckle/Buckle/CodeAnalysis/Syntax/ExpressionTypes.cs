using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Expression, not a full line of code and most expressions can be interchanged with most other expressions.
/// </summary>
internal abstract class Expression : Node {
    protected Expression(SyntaxTree syntaxTree) : base(syntaxTree) { }
}

/// <summary>
/// Literal expression, such as a number or a string.
/// E.g. "Hello, world!"
/// </summary>
internal sealed partial class LiteralExpression : Expression {
    internal LiteralExpression(SyntaxTree syntaxTree, Token token, object value) : base(syntaxTree) {
        this.token = token;
        this.value = value;
    }

    internal LiteralExpression(SyntaxTree syntaxTree, Token token) : this(syntaxTree, token, token.value) { }

    internal Token token { get; }

    internal object value { get; }

    internal override SyntaxType type => SyntaxType.LITERAL_EXPRESSION;
}

/// <summary>
/// Binary expression, with two operands and an operator.
/// E.g. 4 + 3
/// </summary>
internal sealed partial class BinaryExpression : Expression {
    /// <param name="left">Left side operand</param>
    /// <param name="op">Operator</param>
    /// <param name="right">Right side operand</param>
    internal BinaryExpression(SyntaxTree syntaxTree, Expression left, Token op, Expression right)
        : base(syntaxTree) {
        this.left = left;
        this.op = op;
        this.right = right;
    }

    /// <summary>
    /// Left side operand.
    /// </summary>
    internal Expression left { get; }

    /// <summary>
    /// Operator.
    /// </summary>
    internal Token op { get; }

    /// <summary>
    /// Right side operand.
    /// </summary>
    internal Expression right { get; }

    internal override SyntaxType type => SyntaxType.BINARY_EXPRESSION;
}

/// <summary>
/// Postfix expression, only two possible ones (++ or --). Cannot not be applied to literals.
/// E.g. x++
/// </summary>
internal sealed partial class PostfixExpression : Expression {
    /// <param name="identifier">Existing variable name</param>
    /// <param name="op">Operator</param>
    internal PostfixExpression(SyntaxTree syntaxTree, Token identifier, Token op)
        : base(syntaxTree) {
        this.identifier = identifier;
        this.op = op;
    }

    /// <summary>
    /// Existing variable name.
    /// </summary>
    internal Token identifier { get; }

    /// <summary>
    /// Operator.
    /// </summary>
    internal Token op { get; }

    internal override SyntaxType type => SyntaxType.POSTFIX_EXPRESSION;
}

/// <summary>
/// Prefix expression, only two possible ones (++ or --). Cannot be applied to literals.
/// E.g. ++x
/// </summary>
internal sealed partial class PrefixExpression : Expression {
    /// <param name="identifier">Existing variable name</param>
    /// <param name="op">Operator</param>
    internal PrefixExpression(SyntaxTree syntaxTree, Token op, Token identifier)
        : base(syntaxTree) {
        this.op = op;
        this.identifier = identifier;
    }

    /// <summary>
    /// Existing variable name.
    /// </summary>
    internal Token identifier { get; }

    /// <summary>
    /// Operator.
    /// </summary>
    internal Token op { get; }

    internal override SyntaxType type => SyntaxType.PREFIX_EXPRESSION;
}

/// <summary>
/// Parenthesis expression, only does something doing parsing and adjusts tree order.
/// E.g. (expression)
/// Not to be confused with the call expression, parenthesis do no invocation.
/// </summary>
internal sealed partial class ParenthesisExpression : Expression {
    internal ParenthesisExpression(
        SyntaxTree syntaxTree, Token openParenthesis, Expression expression, Token closeParenthesis)
        : base(syntaxTree) {
        this.openParenthesis = openParenthesis;
        this.expression = expression;
        this.closeParenthesis = closeParenthesis;
    }

    internal Token? openParenthesis { get; }

    internal Expression expression { get; }

    internal Token? closeParenthesis { get; }

    internal override SyntaxType type => SyntaxType.PARENTHESIZED_EXPRESSION;
}

/// <summary>
/// Unary expression, has higher precedence than binary expressions.
/// E.g. -3
/// </summary>
internal sealed partial class UnaryExpression : Expression {
    /// <param name="op">Operator</param>
    internal UnaryExpression(SyntaxTree syntaxTree, Token op, Expression operand) : base(syntaxTree) {
        this.op = op;
        this.operand = operand;
    }

    /// <summary>
    /// Operator.
    /// </summary>
    internal Token op { get; }

    internal Expression operand { get; }

    internal override SyntaxType type => SyntaxType.UNARY_EXPRESSION;
}

/// <summary>
/// Name expression, references a symbol (variable or function).
/// E.g. myVar
/// </summary>
internal sealed partial class NameExpression : Expression {
    /// <param name="identifier">Name of the symbol</param>
    internal NameExpression(SyntaxTree syntaxTree, Token identifier) : base(syntaxTree) {
        this.identifier = identifier;
    }

    /// <summary>
    /// Name of the symbol.
    /// </summary>
    internal Token identifier { get; }

    internal override SyntaxType type => SyntaxType.NAME_EXPRESSION;
}

/// <summary>
/// Assignment expression, similar to an operator but assigns to an existing variable.
/// Thus cannot be used on literals.
/// E.g. x = 4
/// </summary>
internal sealed partial class AssignmentExpression : Expression {
    /// <param name="identifier">Name of a variable</param>
    /// <param name="expression">Value to set variable to</param>
    internal AssignmentExpression(SyntaxTree syntaxTree, Token identifier, Token assignmentToken, Expression expression)
        : base(syntaxTree) {
        this.identifier = identifier;
        this.assignmentToken = assignmentToken;
        this.expression = expression;
    }

    /// <summary>
    /// Name of a variable.
    /// </summary>
    internal Token identifier { get; }

    internal Token assignmentToken { get; }

    /// <summary>
    /// Value to set variable to.
    /// </summary>
    internal Expression expression { get; }

    internal override SyntaxType type => SyntaxType.ASSIGN_EXPRESSION;
}

/// <summary>
/// Empty expression, used as debugging expressions and placeholders.
/// Can only be created in a source file by creating an expression statement with an empty expression:
///     ;
/// </summary>
internal sealed partial class EmptyExpression : Expression {
    internal EmptyExpression(SyntaxTree syntaxTree) : base(syntaxTree) { }

    internal override SyntaxType type => SyntaxType.EMPTY_EXPRESSION;
}

/// <summary>
/// Call expression, invokes a callable symbol (function).
/// E.g. myFunc(...)
/// </summary>
internal sealed partial class CallExpression : Expression {
    /// <param name="identifier">Name of the called function</param>
    /// <param name="arguments">Parameter list</param>
    internal CallExpression(
        SyntaxTree syntaxTree, NameExpression identifier, Token openParenthesis,
        SeparatedSyntaxList<Expression> arguments, Token closeParenthesis)
        : base(syntaxTree) {
        this.identifier = identifier;
        this.openParenthesis = openParenthesis;
        this.arguments = arguments;
        this.closeParenthesis = closeParenthesis;
    }

    /// <summary>
    /// Name of the called function.
    /// </summary>
    internal NameExpression identifier { get; }

    internal Token? openParenthesis { get; }

    /// <summary>
    /// Parameter list.
    /// </summary>
    internal SeparatedSyntaxList<Expression> arguments { get; }

    internal Token? closeParenthesis { get; }

    internal override SyntaxType type => SyntaxType.CALL_EXPRESSION;
}

/// <summary>
/// Index expression, only used on array types.
/// E.g. myArr[3]
/// </summary>
internal sealed partial class IndexExpression : Expression {
    /// <param name="operand">Anything with a type with dimension greater than 0</param>
    /// <param name="index">Zero indexed</param>
    internal IndexExpression(
        SyntaxTree syntaxTree, Expression operand, Token openBracket, Expression index, Token closeBracket)
        : base(syntaxTree) {
        this.operand = operand;
        this.openBracket = openBracket;
        this.index = index;
        this.closeBracket = closeBracket;
    }

    /// <summary>
    /// Anything with a type with dimension greater than 0.
    /// </summary>
    internal Expression operand { get; }

    internal Token? openBracket { get; }

    /// <summary>
    /// Zero indexed.
    /// </summary>
    internal Expression index { get; }

    internal Token? closeBracket { get; }

    internal override SyntaxType type => SyntaxType.INDEX_EXPRESSION;
}

/// <summary>
/// Initializer list expression, to initialize array types.
/// E.g. { 1, 2, 3 }
/// </summary>
internal sealed partial class InitializerListExpression : Expression {
    internal InitializerListExpression(SyntaxTree syntaxTree,
        Token openBrace, SeparatedSyntaxList<Expression> items, Token closeBrace)
        : base(syntaxTree) {
        this.openBrace = openBrace;
        this.items = items;
        this.closeBrace = closeBrace;
    }

    internal Token? openBrace { get; }

    internal SeparatedSyntaxList<Expression> items { get; }

    internal Token? closeBrace { get; }

    internal override SyntaxType type => SyntaxType.LITERAL_EXPRESSION;
}

/// <summary>
/// Reference expression, returns the reference to a symbol.
/// E.g. ref myVar
/// </summary>
internal sealed partial class ReferenceExpression : Expression {
    /// <param name="identifier">Name of the referenced symbol</param>
    internal ReferenceExpression(SyntaxTree syntaxTree, Token refKeyword, Token identifier) : base(syntaxTree) {
        this.refKeyword = refKeyword;
        this.identifier = identifier;
    }

    internal Token refKeyword { get; }

    /// <summary>
    /// Name of the referenced symbol.
    /// </summary>
    internal Token identifier { get; }

    internal override SyntaxType type => SyntaxType.REFERENCE_EXPRESSION;
}

// TODO Make sure that block statements can still have return statements (they might)
/// <summary>
/// Inline function expression, similar to local function but is evaluated immediately and has no signature.
/// E.g. { ... statements (including a return statement) ... }
/// The only thing distinguishing an inline function expression from a block statement is a return statement.
/// </summary>
internal sealed partial class InlineFunctionExpression : Expression {
    /// <param name="statements">Contains at least one return statement</param>
    internal InlineFunctionExpression(
        SyntaxTree syntaxTree, Token openBrace, ImmutableArray<Statement> statements, Token closeBrace)
        : base(syntaxTree) {
        this.openBrace = openBrace;
        this.statements = statements;
        this.closeBrace = closeBrace;
    }

    internal Token? openBrace { get; }

    /// <summary>
    /// Contains at least one return statement.
    /// </summary>
    internal ImmutableArray<Statement> statements { get; }

    internal Token? closeBrace { get; }

    internal override SyntaxType type => SyntaxType.INLINE_FUNCTION;
}

/// <summary>
/// Cast expresion (C-Style).
/// E.g. (int)3.4
/// </summary>
internal sealed partial class CastExpression : Expression {
    /// <param name="typeClause">The target type clause</param>
    internal CastExpression(
        SyntaxTree syntaxTree, Token openParenthesis, TypeClause typeClause,
        Token closeParenthesis, Expression expression)
        : base(syntaxTree) {
        this.openParenthesis = openParenthesis;
        this.typeClause = typeClause;
        this.closeParenthesis = closeParenthesis;
        this.expression = expression;
    }

    internal Token? openParenthesis { get; }

    /// <summary>
    /// The target type clause.
    /// </summary>
    internal TypeClause typeClause { get; }

    internal Token? closeParenthesis { get; }

    internal Expression expression { get; }

    internal override SyntaxType type => SyntaxType.CAST_EXPRESSION;
}

/// <summary>
/// Typeof expression (C#-Style).
/// E.g. typeof(int)
/// </summary>
internal sealed partial class TypeofExpression : Expression {
    /// <param name="typeCLause">The type to get the type type from</param>
    internal TypeofExpression(
        SyntaxTree syntaxTree, Token typeofKeyword, Token openParenthesis,
        TypeClause typeClause, Token closeParenthesis)
        : base(syntaxTree) {
        this.typeofKeyword = typeofKeyword;
        this.openParenthesis = openParenthesis;
        this.typeClause = typeClause;
        this.closeParenthesis = closeParenthesis;
    }

    internal Token typeofKeyword { get;  }

    internal Token openParenthesis { get;  }

    internal TypeClause typeClause { get; }

    internal Token closeParenthesis { get; }

    internal override SyntaxType type => SyntaxType.TYPEOF_EXPRESSION;
}
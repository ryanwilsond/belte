
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// All types of things to be found in a source file.
/// </summary>
internal enum SyntaxType {
    // Punctuation
    TildeToken,
    ExclamationToken,
    PercentToken,
    CaretToken,
    AmpersandToken,
    AsteriskToken,
    OpenParenToken,
    CloseParenToken,
    MinusToken,
    PlusToken,
    EqualsToken,
    OpenBraceToken,
    CloseBraceToken,
    OpenBracketToken,
    CloseBracketToken,
    PipeToken,
    SemicolonToken,
    LessThanToken,
    CommaToken,
    GreaterThanToken,
    SlashToken,

    // Compound punctuation
    PipePipeToken,
    AmpersandAmpersandToken,
    MinusMinusToken,
    PlusPlusToken,
    AsteriskAsteriskToken,
    QuestionQuestionToken,
    ExclamationEqualsToken,
    EqualsEqualsToken,
    LessThanEqualsToken,
    LessThanLessThanToken,
    LessThanLessThanEqualsToken,
    GreaterThanEqualsToken,
    GreaterThanGreaterThanToken,
    GreaterThanGreaterThanEqualsToken,
    SlashEqualsToken,
    AsteriskEqualsToken,
    PipeEqualsToken,
    AmpersandEqualsToken,
    PlusEqualsToken,
    MinusEqualsToken,
    AsteriskAsteriskEqualsToken,
    CaretEqualsToken,
    PercentEqualsToken,
    QuestionQuestionEqualsToken,
    GreaterThanGreaterThanGreaterThanToken,
    GreaterThanGreaterThanGreaterThanEqualsToken,

    // Keywords
    TypeOfKeyword,
    NullKeyword,
    TrueKeyword,
    FalseKeyword,
    IfKeyword,
    ElseKeyword,
    WhileKeyword,
    ForKeyword,
    DoKeyword,
    TryKeyword,
    CatchKeyword,
    FinallyKeyword,
    BreakKeyword,
    ContinueKeyword,
    ReturnKeyword,
    ConstKeyword,
    RefKeyword,
    IsKeyword,
    IsntKeyword,
    StructKeyword,
    VarKeyword,

    // Tokens with text
    BadToken,
    IdentifierToken,
    NumericLiteralToken,
    StringLiteralToken,

    // Trivia
    EndOfLineTrivia,
    WhitespaceTrivia,
    SingleLineCommentTrivia,
    MultiLineCommentTrivia,
    SkippedTokenTrivia,

    // Expressions
    ParenthesizedExpression,
    CastExpression,
    EmptyExpression,

    // Operator expressions
    BinaryExpression,
    UnaryExpression,
    IndexExpression,
    PrefixExpression,
    PostfixExpression,
    AssignExpression,
    CompoundAssignmentExpression,

    // Primary expressions
    LiteralExpression,
    TypeOfExpression,
    InlineFunction,
    NameExpression,
    CallExpression,
    RefExpression,

    // Statements
    Block,
    VariableDeclarationStatement,
    ExpressionStatement,
    LocalFunctionStatement,
    EmptyStatement,

    // Jump statements
    BreakStatement,
    ContinueStatement,
    ReturnStatement,
    WhileStatement,
    DoWhileStatement,
    ForStatement,

    // Checked statements
    IfStatement,
    ElseClause,
    TryStatement,
    CatchClause,
    FinallyClause,

    // Declarations
    CompilationUnit,
    GlobalStatement,

    // Type declarations
    StructDeclaration,
    TypeClause,
    Parameter,
    MethodDeclaration,

    // Other
    EndOfFileToken,
}

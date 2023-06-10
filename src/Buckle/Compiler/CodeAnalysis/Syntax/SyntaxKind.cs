
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// All types of things to be found in a source file.
/// </summary>
public enum SyntaxKind {
    None = 0,
    List = GreenNode.ListKind,

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
    QuestionToken,
    ColonToken,
    PeriodToken,

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
    QuestionPeriodToken,
    QuestionOpenBracketToken,

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
    ClassKeyword,
    VarKeyword,
    NewKeyword,
    ThisKeyword,

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
    TypeExpression,
    ThisExpression,
    EmptyExpression,

    // Operator expressions
    TernaryExpression,
    BinaryExpression,
    UnaryExpression,
    IndexExpression,
    PrefixExpression,
    PostfixExpression,
    AssignmentExpression,
    CompoundAssignmentExpression,

    // Primary expressions
    LiteralExpression,
    TypeOfExpression,
    IdentifierNameExpression,
    TemplateNameExpression,
    CallExpression,
    ReferenceExpression,
    MemberAccessExpression,
    ObjectCreationExpression,

    // Statements
    BlockStatement,
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

    // Attributes
    Attribute,

    // Type declarations
    StructDeclaration,
    ClassDeclaration,
    FieldDeclaration,
    Type,
    ArrayRankSpecifier,
    Parameter,
    ParameterList,
    TemplateParameterList,
    TemplateArgumentList,
    MethodDeclaration,
    ConstructorDeclaration,

    // Other
    Argument,
    ArgumentList,
    EndOfFileToken,
}

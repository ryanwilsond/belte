
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
    ConstexprKeyword,
    RefKeyword,
    IsKeyword,
    IsntKeyword,
    StructKeyword,
    ClassKeyword,
    NewKeyword,
    ThisKeyword,
    StaticKeyword,
    OperatorKeyword,

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
    CallExpression,
    ReferenceExpression,
    MemberAccessExpression,
    ObjectCreationExpression,

    // Statements
    BlockStatement,
    ExpressionStatement,
    LocalDeclarationStatement,
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
    VariableDeclaration,
    EqualsValueClause,
    StructDeclaration,
    ClassDeclaration,
    FieldDeclaration,
    MethodDeclaration,
    ConstructorDeclaration,
    OperatorDeclaration,

    // Names
    IdentifierName,
    TemplateName,
    QualifiedName,
    EmptyName,
    ArrayType,
    NonNullableType,
    ReferenceType,

    // Lists
    Argument,
    ArgumentList,
    Parameter,
    ParameterList,
    ArrayRankSpecifier,
    TemplateParameterList,
    TemplateArgumentList,
    Attribute,
    AttributeList,

    // Other
    EndOfFileToken,
}

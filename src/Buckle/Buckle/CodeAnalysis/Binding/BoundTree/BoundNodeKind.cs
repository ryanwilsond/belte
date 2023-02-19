
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Types of bound nodes.
/// </summary>
internal enum BoundNodeKind {
    Invalid,

    TernaryExpression,
    BinaryExpression,
    UnaryExpression,
    LiteralExpression,
    VariableExpression,
    AssignmentExpression,
    EmptyExpression,
    ErrorExpression,
    CallExpression,
    IndexExpression,
    CastExpression,
    CompoundAssignmentExpression,
    ReferenceExpression,
    TypeOfExpression,
    ConstructorExpression,
    MemberAccessExpression,
    PrefixExpression,
    PostfixExpression,

    BlockStatement,
    ExpressionStatement,
    VariableDeclarationStatement,
    IfStatement,
    WhileStatement,
    ForStatement,
    GotoStatement,
    LabelStatement,
    ConditionalGotoStatement,
    DoWhileStatement,
    TryStatement,
    ReturnStatement,
    NopStatement,
    FieldDeclarationStatement,
    BreakStatement,
    ContinueStatement,

    Type,
    TypeWrapper,
}

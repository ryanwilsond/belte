
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Types of bound nodes.
/// </summary>
internal enum BoundNodeType {
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
    InlineFunctionExpression,
    TypeOfExpression,
    ConstructorExpression,
    MemberAccessExpression,

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

    TypeClause,
}

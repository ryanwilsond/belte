
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Types of bound nodes.
/// </summary>
internal enum BoundNodeKind : byte {
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
    ObjectCreationExpression,
    ArrayCreationExpression,
    FieldAccessExpression,
    ConditionalAccessExpression,
    PrefixExpression,
    PostfixExpression,
    ThisExpression,
    BaseExpression,
    ExtendExpression,
    ThrowExpression,
    InitializerListExpression,
    InitializerDictionaryExpression,
    TypeExpression,

    BlockStatement,
    ExpressionStatement,
    LocalDeclarationStatement,
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

    VariableDeclaration,
    MethodGroup,
}

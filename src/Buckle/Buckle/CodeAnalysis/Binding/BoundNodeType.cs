using Buckle.IO;
using System.Collections.Generic;
using System;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Types of bound nodes.
/// </summary>
internal enum BoundNodeType {
    Invalid,

    UnaryExpression,
    LiteralExpression,
    BinaryExpression,
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

    TypeClause,
}

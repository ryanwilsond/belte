using System.IO;
using Buckle.IO;

namespace Buckle.CodeAnalysis.Binding;

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
    CastExpression,
    CompoundAssignmentExpression,

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
    ReturnStatement,
    NopStatement,
}

internal abstract class BoundNode {
    public abstract BoundNodeType type { get; }

    public override string ToString() {
        using (var writer = new StringWriter()) {
            this.WriteTo(writer);
            return writer.ToString();
        }
    }
}

internal sealed class BoundConstant {
    public object value { get; }

    public BoundConstant(object value_) {
        value = value_;
    }
}

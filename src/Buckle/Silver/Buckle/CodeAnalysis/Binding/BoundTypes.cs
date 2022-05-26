using System.IO;
using Buckle.IO;
using Buckle.CodeAnalysis.Symbols;

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
    IndexExpression,
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
    TryStatement,
    ReturnStatement,
    NopStatement,

    TypeClause,
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

internal sealed class BoundTypeClause : BoundNode {
    public TypeSymbol lType { get; }
    public bool isImplicit { get; }
    public bool isConst { get; }
    public bool isRef { get; }
    public int dimensions { get; }
    public override BoundNodeType type => BoundNodeType.TypeClause;

    public BoundTypeClause(
        TypeSymbol lType_, bool isImplicit_ = false, bool isConst_ = false,
        bool isRef_ = false, int dimensions_ = 0) {
        lType = lType_;
        isImplicit = isImplicit_;
        isConst = isConst_;
        isRef = isRef_;
        dimensions = dimensions_;
    }

    public BoundTypeClause ChildType() {
        if (dimensions > 0)
            return new BoundTypeClause(lType, isImplicit, isConst, isRef, dimensions - 1);
        else
            return null;
    }

    public BoundTypeClause BaseType() {
        if (dimensions > 0)
            return new BoundTypeClause(lType, isImplicit, isConst, isRef, 0);
        else
            return this;
    }

    public override string ToString() {
        string text = "";

        if (isConst)
            text += "const ";
        if (isRef)
            text += "ref ";

        text += lType.name;

        for (int i=0; i<dimensions; i++)
            text += "[]";

        return text;
    }
}

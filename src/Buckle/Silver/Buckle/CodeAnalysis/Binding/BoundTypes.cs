using System.IO;
using Buckle.IO;
using Buckle.CodeAnalysis.Symbols;
using System.Collections.Generic;
using System;

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
    ReferenceExpression,
    InlineFunctionExpression,

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
    public bool isConstantReference { get; }
    public bool isReference { get; }
    public bool isConstant { get; }
    // only mutable part of this tree, has to be to simplify converting literals to nullable
    public bool isNullable { get; set; }
    public bool isLiteral { get; }
    public int dimensions { get; }
    public override BoundNodeType type => BoundNodeType.TypeClause;

    public BoundTypeClause(
        TypeSymbol lType_, bool isImplicit_ = false, bool isConstRef_ = false, bool isRef_ = false,
        bool isConst_ = false, bool isNullable_ = false, bool isLiteral_ = false, int dimensions_ = 0) {
        lType = lType_;
        isImplicit = isImplicit_;
        isConstantReference = isConst_;
        isReference = isRef_;
        isConstant = isConst_;
        isNullable = isNullable_;
        isLiteral = isLiteral_;
        dimensions = dimensions_;
    }

    public BoundTypeClause ChildType() {
        if (dimensions > 0)
            return new BoundTypeClause(
                lType, isImplicit, isConstantReference, isReference, isConstant, isNullable, isLiteral, dimensions - 1);
        else
            return null;
    }

    public BoundTypeClause BaseType() {
        if (dimensions > 0)
            return new BoundTypeClause(
                lType, isImplicit, isConstantReference, isReference, isConstant, isNullable, isLiteral, 0);
        else
            return this;
    }

    public override string ToString() {
        string text = "";

        if (!isNullable && !isLiteral)
            text += "[NotNull]";

        if (isConstantReference)
            text += "const ";
        if (isReference)
            text += "ref ";
        if (isConstant)
            text += "const ";

        text += lType.name;

        for (int i=0; i<dimensions; i++)
            text += "[]";

        return text;
    }

    public static bool AboutEqual(BoundTypeClause returnType, BoundTypeClause typeClause) {
        if (returnType.lType != typeClause.lType)
            return false;
        if (returnType.isReference != typeClause.isReference)
            return false;
        if (returnType.dimensions != typeClause.dimensions)
            return false;

        return true;
    }
}

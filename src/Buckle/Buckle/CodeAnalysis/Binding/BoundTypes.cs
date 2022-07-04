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
    internal abstract BoundNodeType type { get; }

    public override string ToString() {
        using (var writer = new StringWriter()) {
            this.WriteTo(writer);
            return writer.ToString();
        }
    }
}

internal sealed class BoundConstant {
    internal object value { get; }

    internal BoundConstant(object value_) {
        value = value_;
    }
}

internal sealed class BoundTypeClause : BoundNode {
    internal TypeSymbol lType { get; }
    internal bool isImplicit { get; }
    internal bool isConstantReference { get; }
    internal bool isReference { get; }
    internal bool isConstant { get; }
    // only mutable part of this tree, has to be to simplify converting literals to nullable
    // TODO: consider making this immutable and solving another way to keep immutable design consistent
    internal bool isNullable { get; set; }
    internal bool isLiteral { get; }
    internal int dimensions { get; }
    internal override BoundNodeType type => BoundNodeType.TypeClause;

    internal static readonly BoundTypeClause NullableDecimal = new BoundTypeClause(TypeSymbol.Decimal, isNullable_: true);
    internal static readonly BoundTypeClause NullableInt = new BoundTypeClause(TypeSymbol.Int, isNullable_: true);
    internal static readonly BoundTypeClause NullableString = new BoundTypeClause(TypeSymbol.String, isNullable_: true);
    internal static readonly BoundTypeClause NullableBool = new BoundTypeClause(TypeSymbol.Bool, isNullable_: true);
    internal static readonly BoundTypeClause NullableAny = new BoundTypeClause(TypeSymbol.Any, isNullable_: true);
    internal static readonly BoundTypeClause Decimal = new BoundTypeClause(TypeSymbol.Decimal);
    internal static readonly BoundTypeClause Int = new BoundTypeClause(TypeSymbol.Int);
    internal static readonly BoundTypeClause String = new BoundTypeClause(TypeSymbol.String);
    internal static readonly BoundTypeClause Bool = new BoundTypeClause(TypeSymbol.Bool);
    internal static readonly BoundTypeClause Any = new BoundTypeClause(TypeSymbol.Any);

    internal BoundTypeClause(
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

    internal BoundTypeClause ChildType() {
        if (dimensions > 0)
            return new BoundTypeClause(
                lType, isImplicit, isConstantReference, isReference, isConstant, isNullable, isLiteral, dimensions - 1);
        else
            return null;
    }

    internal BoundTypeClause BaseType() {
        if (dimensions > 0)
            return new BoundTypeClause(
                lType, isImplicit, isConstantReference, isReference, isConstant, isNullable, isLiteral, 0);
        else
            return this;
    }

    public override string ToString() {
        var text = "";

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

    internal static bool AboutEqual(BoundTypeClause returnType, BoundTypeClause typeClause) {
        if (returnType.lType != typeClause.lType)
            return false;
        if (returnType.isReference != typeClause.isReference)
            return false;
        if (returnType.dimensions != typeClause.dimensions)
            return false;

        return true;
    }

    internal static BoundTypeClause Copy(BoundTypeClause value) {
        return new BoundTypeClause(
            value.lType, value.isImplicit, value.isConstantReference, value.isReference,
            value.isConstant, value.isNullable, value.isLiteral, value.dimensions);
    }
}

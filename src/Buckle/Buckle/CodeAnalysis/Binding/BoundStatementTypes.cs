using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// Note: All bound versions of statements and expression share function with parser equivalents.
/// Thus use their xml comments for reference.

/// <summary>
/// A bound statement, bound from a parser Statement
/// </summary>
internal abstract class BoundStatement : BoundNode { }

/// <summary>
/// A bound block statement, bound from a parser BlockStatement.
/// </summary>
internal sealed class BoundBlockStatement : BoundStatement {
    internal BoundBlockStatement(ImmutableArray<BoundStatement> statements) {
        this.statements = statements;
    }

    internal ImmutableArray<BoundStatement> statements { get; }

    internal override BoundNodeType type => BoundNodeType.BlockStatement;
}

/// <summary>
/// A bound expression statement, bound from a parser ExpressionStatement.
/// </summary>
internal sealed class BoundExpressionStatement : BoundStatement {
    internal BoundExpressionStatement(BoundExpression expression) {
        this.expression = expression;
    }

    internal BoundExpression expression { get; }

    internal override BoundNodeType type => BoundNodeType.ExpressionStatement;
}

/// <summary>
/// A bound variable declaration statement, bound from a parser VariableDeclarationStatement.
/// </summary>
internal sealed class BoundVariableDeclarationStatement : BoundStatement {
    internal BoundVariableDeclarationStatement(VariableSymbol variable, BoundExpression initializer) {
        this.variable = variable;
        this.initializer = initializer;
    }

    internal VariableSymbol variable { get; }

    internal BoundExpression initializer { get; }

    internal override BoundNodeType type => BoundNodeType.VariableDeclarationStatement;
}

/// <summary>
/// A bound if statement, bound from a parser IfStatement.
/// </summary>
internal sealed class BoundIfStatement : BoundStatement {
    internal BoundIfStatement(BoundExpression condition, BoundStatement then, BoundStatement elseStatement) {
        this.condition = condition;
        this.then = then;
        this.elseStatement = elseStatement;
    }

    internal BoundExpression condition { get; }

    internal BoundStatement then { get; }

    internal BoundStatement elseStatement { get; }

    internal override BoundNodeType type => BoundNodeType.IfStatement;
}

/// <summary>
/// A bound try statement, bound from a parser TryStatement.
/// Instead of having a catch clause and finally clause, it just has their bodies.
/// </summary>
internal sealed class BoundTryStatement : BoundStatement {
    internal BoundTryStatement(
        BoundBlockStatement body, BoundBlockStatement catchBody, BoundBlockStatement finallyBody) {
        this.body = body;
        this.catchBody = catchBody;
        this.finallyBody = finallyBody;
    }

    internal BoundBlockStatement body { get; }

    internal BoundBlockStatement catchBody { get; }

    internal BoundBlockStatement finallyBody { get; }

    internal override BoundNodeType type => BoundNodeType.TryStatement;
}

/// <summary>
/// A base type for bound loop types (for, do, do while).
/// Uses labels for gotos as the Lowerer rewrites all control of flow to gotos.
/// </summary>
internal abstract class BoundLoopStatement : BoundStatement {
    protected BoundLoopStatement(BoundLabel breakLabel, BoundLabel continueLabel) {
        this.breakLabel = breakLabel;
        this.continueLabel = continueLabel;
    }

    internal BoundLabel breakLabel { get; }

    internal BoundLabel continueLabel { get; }
}

/// <summary>
/// A bound while statement, bound from a parser WhileStatement.
/// </summary>
internal sealed class BoundWhileStatement : BoundLoopStatement {
    internal BoundWhileStatement(
        BoundExpression condition, BoundStatement body, BoundLabel breakLabel, BoundLabel continueLabel)
        : base(breakLabel, continueLabel) {
        this.condition = condition;
        this.body = body;
    }

    internal BoundExpression condition { get; }

    internal BoundStatement body { get; }

    internal override BoundNodeType type => BoundNodeType.WhileStatement;
}

/// <summary>
/// A bound for statement, bound from a parser ForStatement.
/// </summary>
internal sealed class BoundForStatement : BoundLoopStatement {
    internal BoundForStatement(
        BoundStatement initializer, BoundExpression condition, BoundExpression step,
        BoundStatement body, BoundLabel breakLabel, BoundLabel continueLabel)
        : base(breakLabel, continueLabel) {
        this.initializer = initializer;
        this.condition = condition;
        this.step = step;
        this.body = body;
    }

    internal BoundStatement initializer { get; }

    internal BoundExpression condition { get; }

    internal BoundExpression step { get; }

    internal BoundStatement body { get; }

    internal override BoundNodeType type => BoundNodeType.ForStatement;
}

/// <summary>
/// A bound do while statement, bound from a parser DoWhileStatement.
/// </summary>
internal sealed class BoundDoWhileStatement : BoundLoopStatement {
    internal BoundDoWhileStatement(
        BoundStatement body, BoundExpression condition, BoundLabel breakLabel, BoundLabel continueLabel)
        : base(breakLabel, continueLabel) {
        this.body = body;
        this.condition = condition;
    }

    internal BoundStatement body { get; }

    internal BoundExpression condition { get; }

    internal override BoundNodeType type => BoundNodeType.DoWhileStatement;
}

/// <summary>
/// A bound goto statement, produced by Lowerer. No parser equivalent.
/// E.g. goto label
/// </summary>
internal sealed class BoundGotoStatement : BoundStatement {
    internal BoundGotoStatement(BoundLabel label) {
        this.label = label;
    }

    internal BoundLabel label { get; }

    internal override BoundNodeType type => BoundNodeType.GotoStatement;
}

/// <summary>
/// A bound conditional goto statement, produced by Lowerer. No parser equivalent.
/// E.g. goto label if condition
/// </summary>
internal sealed class BoundConditionalGotoStatement : BoundStatement {
    internal BoundConditionalGotoStatement(BoundLabel label, BoundExpression condition, bool jumpIfTrue = true) {
        this.label = label;
        this.condition = condition;
        this.jumpIfTrue = jumpIfTrue;
    }

    internal BoundLabel label { get; }

    internal BoundExpression condition { get; }

    internal bool jumpIfTrue { get; }

    internal override BoundNodeType type => BoundNodeType.ConditionalGotoStatement;
}

/// <summary>
/// A bound label statement, produced by Lowerer. No parser equivalent.
/// E.g. label1:
/// </summary>
internal sealed class BoundLabelStatement : BoundStatement {
    internal BoundLabelStatement(BoundLabel label) {
        this.label = label;
    }

    internal BoundLabel label { get; }

    internal override BoundNodeType type => BoundNodeType.LabelStatement;
}

/// <summary>
/// A bound return statement, bound from a parser ReturnStatement.
/// </summary>
internal sealed class BoundReturnStatement : BoundStatement {
    internal BoundReturnStatement(BoundExpression expression) {
        this.expression = expression;
    }

    internal BoundExpression expression { get; }

    internal override BoundNodeType type => BoundNodeType.ReturnStatement;
}

/// <summary>
/// A bound NOP statement. Used to replace parser EmptyExpressions and used as debugging symbols and placeholders.
/// Used to mark the start and end of exception handlers in the Emitter.
/// </summary>
internal sealed class BoundNopStatement : BoundStatement {
    internal override BoundNodeType type => BoundNodeType.NopStatement;
}

using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal abstract class BoundStatement : BoundNode { }

internal sealed class BoundBlockStatement : BoundStatement {
    internal ImmutableArray<BoundStatement> statements { get; }
    internal override BoundNodeType type => BoundNodeType.BlockStatement;

    internal BoundBlockStatement(ImmutableArray<BoundStatement> statements_) {
        statements = statements_;
    }
}

internal sealed class BoundExpressionStatement : BoundStatement {
    internal BoundExpression expression { get; }
    internal override BoundNodeType type => BoundNodeType.ExpressionStatement;

    internal BoundExpressionStatement(BoundExpression expression_) {
        expression = expression_;
    }
}

internal sealed class BoundVariableDeclarationStatement : BoundStatement {
    internal VariableSymbol variable { get; }
    internal BoundExpression initializer { get; }
    internal override BoundNodeType type => BoundNodeType.VariableDeclarationStatement;

    internal BoundVariableDeclarationStatement(VariableSymbol variable_, BoundExpression initializer_) {
        variable = variable_;
        initializer = initializer_;
    }
}

internal sealed class BoundIfStatement : BoundStatement {
    internal BoundExpression condition { get; }
    internal BoundStatement then { get; }
    internal BoundStatement elseStatement { get; }
    internal override BoundNodeType type => BoundNodeType.IfStatement;

    internal BoundIfStatement(BoundExpression condition_, BoundStatement then_, BoundStatement elseStatement_) {
        condition = condition_;
        then = then_;
        elseStatement = elseStatement_;
    }
}

internal sealed class BoundTryStatement : BoundStatement {
    internal BoundBlockStatement body { get; }
    internal BoundBlockStatement catchBody { get; }
    internal BoundBlockStatement finallyBody { get; }
    internal override BoundNodeType type => BoundNodeType.TryStatement;

    internal BoundTryStatement(
        BoundBlockStatement body_, BoundBlockStatement catchBody_, BoundBlockStatement finallyBody_) {
        body = body_;
        catchBody = catchBody_;
        finallyBody = finallyBody_;
    }
}

internal abstract class BoundLoopStatement : BoundStatement {
    internal BoundLabel breakLabel { get; }
    internal BoundLabel continueLabel { get; }

    protected BoundLoopStatement(BoundLabel breakLabel_, BoundLabel continueLabel_) {
        breakLabel = breakLabel_;
        continueLabel = continueLabel_;
    }
}

internal sealed class BoundWhileStatement : BoundLoopStatement {
    internal BoundExpression condition { get; }
    internal BoundStatement body { get; }
    internal override BoundNodeType type => BoundNodeType.WhileStatement;

    internal BoundWhileStatement(
        BoundExpression condition_, BoundStatement body_, BoundLabel breakLabel, BoundLabel continueLabel)
        : base(breakLabel, continueLabel) {
        condition = condition_;
        body = body_;
    }
}

internal sealed class BoundForStatement : BoundLoopStatement {
    internal BoundStatement initializer { get; }
    internal BoundExpression condition { get; }
    internal BoundExpression step { get; }
    internal BoundStatement body { get; }
    internal override BoundNodeType type => BoundNodeType.ForStatement;

    internal BoundForStatement(
        BoundStatement initializer_, BoundExpression condition_, BoundExpression step_,
        BoundStatement body_, BoundLabel breakLabel, BoundLabel continueLabel)
        : base(breakLabel, continueLabel) {
        initializer = initializer_;
        condition = condition_;
        step = step_;
        body = body_;
    }
}

internal sealed class BoundDoWhileStatement : BoundLoopStatement {
    internal BoundStatement body { get; }
    internal BoundExpression condition { get; }
    internal override BoundNodeType type => BoundNodeType.DoWhileStatement;

    internal BoundDoWhileStatement(
        BoundStatement body_, BoundExpression condition_, BoundLabel breakLabel, BoundLabel continueLabel)
        : base(breakLabel, continueLabel) {
        body = body_;
        condition = condition_;
    }
}

internal sealed class BoundGotoStatement : BoundStatement {
    internal BoundLabel label { get; }
    internal override BoundNodeType type => BoundNodeType.GotoStatement;

    internal BoundGotoStatement(BoundLabel label_) {
        label = label_;
    }
}

internal sealed class BoundConditionalGotoStatement : BoundStatement {
    internal BoundLabel label { get; }
    internal BoundExpression condition { get; }
    internal bool jumpIfTrue { get; }
    internal override BoundNodeType type => BoundNodeType.ConditionalGotoStatement;

    internal BoundConditionalGotoStatement(BoundLabel label_, BoundExpression condition_, bool jumpIfTrue_ = true) {
        label = label_;
        condition = condition_;
        jumpIfTrue = jumpIfTrue_;
    }
}

internal sealed class BoundLabelStatement : BoundStatement {
    internal BoundLabel label { get; }
    internal override BoundNodeType type => BoundNodeType.LabelStatement;

    internal BoundLabelStatement(BoundLabel label_) {
        label = label_;
    }
}

internal sealed class BoundReturnStatement : BoundStatement {
    internal BoundExpression expression { get; }
    internal override BoundNodeType type => BoundNodeType.ReturnStatement;

    internal BoundReturnStatement(BoundExpression expression_) {
        expression = expression_;
    }
}

internal sealed class BoundNopStatement : BoundStatement {
    internal override BoundNodeType type => BoundNodeType.NopStatement;
}

using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal abstract class BoundStatement : BoundNode { }

internal sealed class BoundBlockStatement : BoundStatement {
    public ImmutableArray<BoundStatement> statements { get; }
    public override BoundNodeType type => BoundNodeType.BlockStatement;

    public BoundBlockStatement(ImmutableArray<BoundStatement> statements_) {
        statements = statements_;
    }
}

internal sealed class BoundExpressionStatement : BoundStatement {
    public BoundExpression expression { get; }
    public override BoundNodeType type => BoundNodeType.ExpressionStatement;

    public BoundExpressionStatement(BoundExpression expression_) {
        expression = expression_;
    }
}

internal sealed class BoundVariableDeclarationStatement : BoundStatement {
    public VariableSymbol variable { get; }
    public BoundExpression initializer { get; }
    public override BoundNodeType type => BoundNodeType.VariableDeclarationStatement;

    public BoundVariableDeclarationStatement(VariableSymbol variable_, BoundExpression initializer_) {
        variable = variable_;
        initializer = initializer_;
    }
}

internal sealed class BoundIfStatement : BoundStatement {
    public BoundExpression condition { get; }
    public BoundStatement then { get; }
    public BoundStatement elseStatement { get; }
    public override BoundNodeType type => BoundNodeType.IfStatement;

    public BoundIfStatement(BoundExpression condition_, BoundStatement then_, BoundStatement elseStatement_) {
        condition = condition_;
        then = then_;
        elseStatement = elseStatement_;
    }
}

internal sealed class BoundTryStatement : BoundStatement {
    public BoundBlockStatement body { get; }
    public BoundBlockStatement catchBody { get; }
    public BoundBlockStatement finallyBody { get; }
    public override BoundNodeType type => BoundNodeType.TryStatement;

    public BoundTryStatement(
        BoundBlockStatement body_, BoundBlockStatement catchBody_, BoundBlockStatement finallyBody_) {
        body = body_;
        catchBody = catchBody_;
        finallyBody = finallyBody_;
    }
}

internal abstract class BoundLoopStatement : BoundStatement {
    public BoundLabel breakLabel { get; }
    public BoundLabel continueLabel { get; }

    protected BoundLoopStatement(BoundLabel breakLabel_, BoundLabel continueLabel_) {
        breakLabel = breakLabel_;
        continueLabel = continueLabel_;
    }
}

internal sealed class BoundWhileStatement : BoundLoopStatement {
    public BoundExpression condition { get; }
    public BoundStatement body { get; }
    public override BoundNodeType type => BoundNodeType.WhileStatement;

    public BoundWhileStatement(
        BoundExpression condition_, BoundStatement body_, BoundLabel breakLabel, BoundLabel continueLabel)
        : base(breakLabel, continueLabel) {
        condition = condition_;
        body = body_;
    }
}

internal sealed class BoundForStatement : BoundLoopStatement {
    public BoundStatement initializer { get; }
    public BoundExpression condition { get; }
    public BoundExpression step { get; }
    public BoundStatement body { get; }
    public override BoundNodeType type => BoundNodeType.ForStatement;

    public BoundForStatement(
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
    public BoundStatement body { get; }
    public BoundExpression condition { get; }
    public override BoundNodeType type => BoundNodeType.DoWhileStatement;

    public BoundDoWhileStatement(
        BoundStatement body_, BoundExpression condition_, BoundLabel breakLabel, BoundLabel continueLabel)
        : base(breakLabel, continueLabel) {
        body = body_;
        condition = condition_;
    }
}

internal sealed class BoundGotoStatement : BoundStatement {
    public BoundLabel label { get; }
    public override BoundNodeType type => BoundNodeType.GotoStatement;

    public BoundGotoStatement(BoundLabel label_) {
        label = label_;
    }
}

internal sealed class BoundConditionalGotoStatement : BoundStatement {
    public BoundLabel label { get; }
    public BoundExpression condition { get; }
    public bool jumpIfTrue { get; }
    public override BoundNodeType type => BoundNodeType.ConditionalGotoStatement;

    public BoundConditionalGotoStatement(BoundLabel label_, BoundExpression condition_, bool jumpIfTrue_ = true) {
        label = label_;
        condition = condition_;
        jumpIfTrue = jumpIfTrue_;
    }
}

internal sealed class BoundLabelStatement : BoundStatement {
    public BoundLabel label { get; }
    public override BoundNodeType type => BoundNodeType.LabelStatement;

    public BoundLabelStatement(BoundLabel label_) {
        label = label_;
    }
}

internal sealed class BoundReturnStatement : BoundStatement {
    public BoundExpression expression { get; }
    public override BoundNodeType type => BoundNodeType.ReturnStatement;

    public BoundReturnStatement(BoundExpression expression_) {
        expression = expression_;
    }
}

internal sealed class BoundNopStatement : BoundStatement {
    // basically internal statement varient of BoundEmptyExpression
    public override BoundNodeType type => BoundNodeType.NopStatement;
}

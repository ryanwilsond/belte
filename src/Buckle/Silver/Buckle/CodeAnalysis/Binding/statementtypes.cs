using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding {

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

    internal sealed class BoundWhileStatement : BoundStatement {
        public BoundExpression condition { get; }
        public BoundStatement body { get; }
        public override BoundNodeType type => BoundNodeType.WhileStatement;

        public BoundWhileStatement(BoundExpression condition_, BoundStatement body_) {
            condition = condition_;
            body = body_;
        }
    }

    internal sealed class BoundForStatement : BoundStatement {
        public BoundVariableDeclarationStatement stepper { get; }
        public BoundExpression condition { get; }
        public BoundAssignmentExpression step { get; }
        public BoundStatement body { get; }
        public override BoundNodeType type => BoundNodeType.ForStatement;

        public BoundForStatement(
            BoundVariableDeclarationStatement stepper_, BoundExpression condition_,
            BoundAssignmentExpression step_, BoundStatement body_) {
            stepper = stepper_;
            condition = condition_;
            step = step_;
            body = body_;
        }
    }

    internal sealed class BoundGotoStatement : BoundStatement {
        public LabelSymbol label { get; }
        public override BoundNodeType type => BoundNodeType.GotoStatement;

        public BoundGotoStatement(LabelSymbol label_) {
            label = label_;
        }
    }

    internal sealed class BoundConditionalGotoStatement : BoundStatement {
        public LabelSymbol label { get; }
        public BoundExpression condition { get; }
        public bool jumpIfFalse { get; }
        public override BoundNodeType type => BoundNodeType.ConditionalGotoStatement;

        public BoundConditionalGotoStatement(LabelSymbol label_, BoundExpression condition_, bool jumpIfFalse_=false) {
            label = label_;
            condition = condition_;
            jumpIfFalse = jumpIfFalse_;
        }
    }

    internal sealed class BoundLabelStatement : BoundStatement {
        public LabelSymbol label { get; }
        public override BoundNodeType type => BoundNodeType.LabelStatement;

        public BoundLabelStatement(LabelSymbol label_) {
            label = label_;
        }
    }
}

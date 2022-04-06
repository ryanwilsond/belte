using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

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
        public BoundStatement initializer { get; }
        public BoundExpression condition { get; }
        public BoundExpression step { get; }
        public BoundStatement body { get; }
        public override BoundNodeType type => BoundNodeType.ForStatement;

        public BoundForStatement(
            BoundStatement initializer_, BoundExpression condition_,
            BoundExpression step_, BoundStatement body_) {
            initializer = initializer_;
            condition = condition_;
            step = step_;
            body = body_;
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

    internal sealed class BoundDoWhileStatement : BoundStatement {
        public BoundStatement body { get; }
        public BoundExpression condition { get; }
        public override BoundNodeType type => BoundNodeType.DoWhileStatement;

        public BoundDoWhileStatement(BoundStatement body_, BoundExpression condition_) {
            body = body_;
            condition = condition_;
        }
    }
}

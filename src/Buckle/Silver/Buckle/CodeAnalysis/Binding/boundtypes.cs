using System;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding {

    internal enum BoundNodeType {
        Invalid,
        UnaryExpression,
        LiteralExpression,
        BinaryExpression,
        VariableExpression,
        AssignmentExpression,
        BlockStatement,
        ExpressionStatement,
        VariableDeclarationStatement,
        IfStatement,
        WhileStatement,
        ForStatement,
    }

    internal abstract class BoundNode {
        public abstract BoundNodeType type { get; }
    }

    internal abstract class BoundExpression : BoundNode {
        public abstract Type lType { get; }
    }

    internal sealed class BoundLiteralExpression : BoundExpression {
        public override BoundNodeType type => BoundNodeType.LiteralExpression;
        public override Type lType => value.GetType();
        public object value { get; }

        public BoundLiteralExpression(object value_) {
            value = value_;
        }
    }

    internal sealed class BoundVariableExpression : BoundExpression {
        public VariableSymbol variable { get; }
        public override Type lType => variable.lType;
        public override BoundNodeType type => BoundNodeType.VariableExpression;

        public BoundVariableExpression(VariableSymbol variable_) {
            variable = variable_;
        }
    }

    internal sealed class BoundAssignmentExpression : BoundExpression {
        public VariableSymbol variable { get; }
        public BoundExpression expression { get; }
        public override BoundNodeType type => BoundNodeType.AssignmentExpression;
        public override Type lType => expression.lType;

        public BoundAssignmentExpression(VariableSymbol variable_, BoundExpression expression_) {
            variable = variable_;
            expression = expression_;
        }
    }

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

    internal sealed class BoundVariableDeclaration : BoundStatement {
        public VariableSymbol variable { get; }
        public BoundExpression initializer { get; }
        public override BoundNodeType type => BoundNodeType.VariableDeclarationStatement;

        public BoundVariableDeclaration(VariableSymbol variable_, BoundExpression initializer_) {
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
        public BoundVariableDeclaration stepper { get; }
        public BoundExpression condition { get; }
        public BoundAssignmentExpression step { get; }
        public BoundStatement body { get; }
        public override BoundNodeType type => BoundNodeType.ForStatement;

        public BoundForStatement(
            BoundVariableDeclaration stepper_, BoundExpression condition_,
            BoundAssignmentExpression step_, BoundStatement body_) {
            stepper = stepper_;
            condition = condition_;
            step = step_;
            body = body_;
        }
    }
}

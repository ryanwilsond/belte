using System;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding {

    internal enum BoundNodeType {
        Invalid,
        UNARY_EXPR,
        LITERAL_EXPR,
        BINARY_EXPR,
        VARIABLE_EXPR,
        ASSIGN_EXPR,
        BLOCK_STATEMENT,
        EXPRESSION_STATEMENT,
        VARIABLE_DECLARATION_STATEMENT,
        IF_STATEMENT,
        WHILE_STATEMENT,
        FOR_STATEMENT,
    }

    internal abstract class BoundNode {
        public abstract BoundNodeType type { get; }
    }

    internal abstract class BoundExpression : BoundNode {
        public abstract Type ltype { get; }
    }

    internal sealed class BoundLiteralExpression : BoundExpression {
        public override BoundNodeType type => BoundNodeType.LITERAL_EXPR;
        public override Type ltype => value.GetType();
        public object value { get; }

        public BoundLiteralExpression(object value_) {
            value = value_;
        }
    }

    internal sealed class BoundVariableExpression : BoundExpression {
        public VariableSymbol variable { get; }
        public override Type ltype => variable.ltype;
        public override BoundNodeType type => BoundNodeType.VARIABLE_EXPR;

        public BoundVariableExpression(VariableSymbol variable_) {
            variable = variable_;
        }
    }

    internal sealed class BoundAssignmentExpression : BoundExpression {
        public VariableSymbol variable { get; }
        public BoundExpression expr { get; }
        public override BoundNodeType type => BoundNodeType.ASSIGN_EXPR;
        public override Type ltype => expr.ltype;

        public BoundAssignmentExpression(VariableSymbol variable_, BoundExpression expr_) {
            variable = variable_;
            expr = expr_;
        }
    }

    internal abstract class BoundStatement : BoundNode { }

    internal sealed class BoundBlockStatement : BoundStatement {
        public ImmutableArray<BoundStatement> statements { get; }
        public override BoundNodeType type => BoundNodeType.BLOCK_STATEMENT;

        public BoundBlockStatement(ImmutableArray<BoundStatement> statements_) {
            statements = statements_;
        }
    }

    internal sealed class BoundExpressionStatement : BoundStatement {
        public BoundExpression expr { get; }
        public override BoundNodeType type => BoundNodeType.EXPRESSION_STATEMENT;

        public BoundExpressionStatement(BoundExpression expr_) {
            expr = expr_;
        }
    }

    internal sealed class BoundVariableDeclaration : BoundStatement {
        public VariableSymbol variable { get; }
        public BoundExpression init { get; }
        public override BoundNodeType type => BoundNodeType.VARIABLE_DECLARATION_STATEMENT;

        public BoundVariableDeclaration(VariableSymbol variable_, BoundExpression init_) {
            variable = variable_;
            init = init_;
        }
    }

    internal sealed class BoundIfStatement : BoundStatement {
        public BoundExpression condition { get; }
        public BoundStatement then { get; }
        public BoundStatement elsestatement { get; }
        public override BoundNodeType type => BoundNodeType.IF_STATEMENT;

        public BoundIfStatement(BoundExpression condition_, BoundStatement then_, BoundStatement elsestatement_) {
            condition = condition_;
            then = then_;
            elsestatement = elsestatement_;
        }
    }

    internal sealed class BoundWhileStatement : BoundStatement {
        public BoundExpression condition { get; }
        public BoundStatement body { get; }
        public override BoundNodeType type => BoundNodeType.WHILE_STATEMENT;

        public BoundWhileStatement(BoundExpression condition_, BoundStatement body_) {
            condition = condition_;
            body = body_;
        }
    }

    internal sealed class BoundForStatement : BoundStatement {
        public BoundVariableDeclaration it { get; }
        public BoundExpression condition { get; }
        public BoundAssignmentExpression step { get; }
        public BoundStatement body { get; }
        public override BoundNodeType type => BoundNodeType.FOR_STATEMENT;

        public BoundForStatement(
            BoundVariableDeclaration it_, BoundExpression condition_,
            BoundAssignmentExpression step_, BoundStatement body_) {
            it = it_;
            condition = condition_;
            step = step_;
            body = body_;
        }
    }
}

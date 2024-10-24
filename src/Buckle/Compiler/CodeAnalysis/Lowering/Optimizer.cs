using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.FlowAnalysis;
using Buckle.CodeAnalysis.Symbols;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Lowering;

/// <summary>
/// Optimizes BoundExpressions and BoundStatements.
/// </summary>
internal sealed class Optimizer : BoundTreeRewriter {
    /// <summary>
    /// Optimizes a <see cref="BoundStatement" />.
    /// </summary>
    /// <param name="statement"><see cref="BoundStatement" /> to optimize.</param>
    /// <param name="removeDeadCode">If the compiler is transpiling skip this part of optimizing.</param>
    /// <returns>Optimized <param name="statement" />.</returns>
    internal static BoundStatement Optimize(BoundStatement statement, bool removeDeadCode) {
        var optimizer = new Optimizer();
        var optimizedStatement = optimizer.RewriteStatement(statement);

        if (statement is BoundBlockStatement && removeDeadCode)
            return RemoveDeadCode(optimizedStatement as BoundBlockStatement);
        else
            return optimizedStatement;
    }

    private protected override BoundStatement RewriteConditionalGotoStatement(BoundConditionalGotoStatement statement) {
        /*

        goto <label> if <condition>

        ----> <condition> is constant true

        goto <label>

        ----> <condition> is constant false

        ;

        */
        if (ConstantValue.IsNotNull(statement.condition.constantValue)) {
            var condition = (bool)statement.condition.constantValue.value;
            condition = statement.jumpIfTrue ? condition : !condition;

            if (condition)
                return RewriteStatement(Goto(statement.label));
            else
                return RewriteStatement(Nop());
        }

        return base.RewriteConditionalGotoStatement(statement);
    }

    private protected override BoundExpression RewriteTernaryExpression(BoundTernaryExpression expression) {
        /*

        <left> <op> <center> <op> <right>

        ----> <op> is '?:' and <left> is constant true

        (<center>)

        ----> <op> is '?:' and <left> is constant false

       (<right>)

        */
        if (expression.op.opKind == BoundTernaryOperatorKind.Conditional) {
            if (ConstantValue.IsNotNull(expression.left.constantValue) && (bool)expression.left.constantValue.value)
                return RewriteExpression(expression.center);

            if (ConstantValue.IsNotNull(expression.left.constantValue) && !(bool)expression.left.constantValue.value)
                return RewriteExpression(expression.right);
        }

        return base.RewriteTernaryExpression(expression);
    }

    private protected override BoundExpression RewriteAssignmentExpression(BoundAssignmentExpression expression) {
        /*

        <left> = <right>

        ----> <right> is ref <left>

        ;

        ----> <right> is the same as <left>

        ;

        */
        var left = expression.left;
        var right = expression.right is BoundReferenceExpression r ? r.expression : expression.right;
        // TODO Expand this to cover more cases
        var canSimplify = left is BoundVariableExpression && right is BoundVariableExpression;

        if (canSimplify &&
            BindingUtilities.GetAssignedVariableSymbol(left) == BindingUtilities.GetAssignedVariableSymbol(right)) {
            return new BoundEmptyExpression();
        }

        return base.RewriteAssignmentExpression(expression);
    }

    private protected override BoundExpression RewriteIndexExpression(BoundIndexExpression expression) {
        /*

        <expression>[<index>]

        ----> <index> is constant, return item directly

        (<expression>[<index>])

        */
        if (expression.index.constantValue is null || expression.expression is not BoundInitializerListExpression i)
            return base.RewriteIndexExpression(expression);

        var index = (int)expression.index.constantValue.value;
        return RewriteExpression(i.items[index]);
    }

    private protected override BoundExpression RewriteCallExpression(BoundCallExpression expression) {
        /*

        <method>(<arguments>)

        ----> <method> is Length and argument is a constant list

        <length of constant list>

        */
        if (expression.method == BuiltinMethods.Length && expression.arguments[0].constantValue is not null) {
            var constantList = expression.arguments[0].constantValue.value as ImmutableArray<ConstantValue>?;

            if (constantList.HasValue)
                return RewriteLiteralExpression(new BoundLiteralExpression(constantList.Value.Length));
        }

        return base.RewriteCallExpression(expression);
    }

    private static BoundBlockStatement RemoveDeadCode(BoundBlockStatement statement) {
        var controlFlow = ControlFlowGraph.Create(statement);
        var reachableStatements = new HashSet<BoundStatement>(controlFlow.blocks.SelectMany(b => b.statements));

        var builder = statement.statements.ToBuilder();
        for (var i = builder.Count - 1; i >= 0; i--) {
            if (!reachableStatements.Contains(builder[i]))
                builder.RemoveAt(i);
        }

        return new BoundBlockStatement(builder.ToImmutable());
    }
}

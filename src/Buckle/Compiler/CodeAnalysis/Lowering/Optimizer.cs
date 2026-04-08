using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.FlowAnalysis;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Lowering;

/// <summary>
/// Optimizes BoundExpressions and BoundStatements.
/// </summary>
internal sealed class Optimizer : BoundTreeRewriter {
    private Optimizer() { }

    internal static BoundStatement Optimize(BoundStatement statement) {
        var optimizer = new Optimizer();
        return (BoundStatement)optimizer.Visit(statement);
    }

    internal static BoundBlockStatement RemoveDeadCode(BoundBlockStatement block, BelteDiagnosticQueue diagnostics) {
        var controlFlow = ControlFlowGraph.Create(block);
        var reachableStatements = new HashSet<BoundStatement>(controlFlow.blocks.SelectMany(b => b.statements));

        var builder = block.statements.ToBuilder();
        var seenScopes = new HashSet<SyntaxNode>();

        for (var i = 0; i < builder.Count; i++) {
            var statement = builder[i];

            // TODO CFG not updated for switches
            if (InSwitch(statement.syntax))
                continue;

            // TODO This only works on surface level and breaks on nested trys
            // TODO Will have to rewrite the CFG builder from scratch fix trys later
            if (!reachableStatements.Contains(statement) && statement.kind is not BoundKind.TryStatement and not
                BoundKind.SequencePoint and not BoundKind.SequencePointWithLocation) {
                var statementToRemove = statement;
                PotentiallyReportDeadCode(statementToRemove);
                builder.RemoveAt(i);
                i--;
            }
        }

        return new BoundBlockStatement(block.syntax, builder.ToImmutable(), block.locals, block.localFunctions);

        void PotentiallyReportDeadCode(BoundNode node) {
            var syntax = node.syntax;

            if (syntax.kind == SyntaxKind.LocalFunctionStatement)
                return;

            if (node.kind is BoundKind.GotoStatement or BoundKind.LabelStatement)
                return;

            // if (seenScopes.Add(syntax.parent))
            //     diagnostics.Push(Warning.UnreachableCode(syntax.location));
        }

        bool InSwitch(SyntaxNode node) {
            while (node is not null) {
                if (node.kind == SyntaxKind.SwitchSection)
                    return true;

                node = node.parent;
            }

            return false;
        }
    }

    internal override BoundNode VisitConditionalGotoStatement(BoundConditionalGotoStatement statement) {
        /*

        goto <label> if <condition>

        ----> <condition> is constant true

        goto <label>

        ----> <condition> is constant false

        ;

        */
        var constantValue = statement.condition.constantValue;

        if (statement.condition is BoundObjectCreationExpression { type.specialType: Symbols.SpecialType.Nullable } o)
            constantValue = o.arguments[0].constantValue;

        if (ConstantValue.IsNotNull(constantValue)) {
            var condition = (bool)constantValue.value;
            condition = statement.jumpIfTrue ? condition : !condition;

            if (condition)
                return Visit(Goto(statement.syntax, statement.label));
            else
                return Visit(Nop());
        }

        return base.VisitConditionalGotoStatement(statement);
    }

    internal override BoundNode VisitConditionalOperator(BoundConditionalOperator expression) {
        /*

        <left> <op> <center> <op> <right>

        ----> <left> is constant true

        (<center>)

        ----> <left> is constant false

       (<right>)

        */
        var condition = expression.condition;

        if (ConstantValue.IsNotNull(condition.constantValue) && (bool)condition.constantValue.value)
            return Visit(expression.trueExpression);

        if (ConstantValue.IsNotNull(condition.constantValue) && !(bool)condition.constantValue.value)
            return Visit(expression.falseExpression);

        return base.VisitConditionalOperator(expression);
    }

    internal override BoundNode VisitAssignmentOperator(BoundAssignmentOperator expression) {
        /*

        <left> = <right>

        ----> <right> is ref <left>

        <left>

        ----> <right> is the same as <left>

        <left>

        */
        var left = expression.left;
        var right = expression.right is BoundReferenceExpression r ? r.expression : expression.right;
        // TODO Expand this to cover more cases
        var canSimplify = left is BoundDataContainerExpression ld &&
            right is BoundDataContainerExpression rd &&
            ld.dataContainer.Equals(rd.dataContainer);

        if (canSimplify)
            return Visit(left);

        return base.VisitAssignmentOperator(expression);
    }

    internal override BoundNode VisitNullCoalescingAssignmentOperator(
        BoundNullCoalescingAssignmentOperator expression) {
        /*

        <left> = <right>

        ----> <right> is ref <left>

        <left>

        ----> <right> is the same as <left>

        <left>

        */
        var left = expression.left;
        var right = expression.right is BoundReferenceExpression r ? r.expression : expression.right;
        // TODO Expand this to cover more cases
        var canSimplify = left is BoundDataContainerExpression ld &&
            right is BoundDataContainerExpression rd &&
            ld.dataContainer.Equals(rd.dataContainer);

        if (canSimplify)
            return Visit(left);

        return base.VisitNullCoalescingAssignmentOperator(expression);
    }

    internal override BoundNode VisitArrayAccessExpression(BoundArrayAccessExpression expression) {
        /*

        <expression>[<index>]

        ----> <index> is constant, return item directly

        (<expression>[<index>])

        */
        if (expression.index.constantValue is null || expression.receiver is not BoundInitializerList i)
            return base.VisitArrayAccessExpression(expression);

        var index = (int)expression.index.constantValue.value;
        return Visit(i.items[index]);
    }
}

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.FlowAnalysis;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Lowering;

/// <summary>
/// Optimizes BoundExpressions and BoundStatements. Can be run multiple times.
/// </summary>
internal sealed class Optimizer : BoundTreeRewriterWithStackGuard {
    private readonly bool _isFirstPass;

    // TODO CSE/GVN on pure operations
    private bool _createdCompileTimeExpression = false;

    private Optimizer(bool isFirstPass) {
        _isFirstPass = isFirstPass;
    }

    internal static BoundStatement Optimize(
        BoundStatement statement,
        out bool createdCompileTimeExpression,
        bool isFirstPass) {
        var optimizer = new Optimizer(isFirstPass);

        var result = (BoundStatement)optimizer.Visit(statement);
        createdCompileTimeExpression = optimizer._createdCompileTimeExpression;

        return result;
    }

    internal static BoundBlockStatement RemoveDeadCode(
        Compilation compilation,
        MethodSymbol method,
        BoundBlockStatement block,
        BelteDiagnosticQueue diagnostics) {
        var controlFlow = ControlFlowGraph.Create(compilation, method, block);
        var reachableStatements = new HashSet<BoundStatement>(controlFlow.blocks.SelectMany(b => b.statements));

        var builder = block.statements.ToBuilder();
        var seenScopes = new HashSet<SyntaxNode>();

        for (var i = 0; i < builder.Count; i++) {
            var statement = builder[i];

again:
            if (!reachableStatements.Contains(statement)) {
                if (statement is BoundSequencePoint seqPoint) {
                    statement = seqPoint.statement;

                    if (statement is null)
                        continue;

                    goto again;
                }

                if (statement is BoundSequencePointWithLocation seqPointWithLocation) {
                    statement = seqPointWithLocation.statement;

                    if (statement is null)
                        continue;

                    goto again;
                }

                PotentiallyReportDeadCode(statement);
                builder.RemoveAt(i);
                i--;
            }
        }

        return new BoundBlockStatement(block.syntax, builder.ToImmutable(), block.locals, block.localFunctions);

        void PotentiallyReportDeadCode(BoundNode node) {
            var syntax = node.syntax;

            if (syntax.kind == SyntaxKind.LocalFunctionStatement)
                return;

            if (node.kind == BoundKind.LabelStatement)
                return;

            // TODO This would be cleaner to instead have a isCompilerGenerated property on all bound nodes
            if (node.kind == BoundKind.GotoStatement && !IsUserDeclaredGoto(node.syntax))
                return;

            if (node.kind == BoundKind.ReturnStatement && !IsUserDeclaredReturn(node.syntax))
                return;

            if (node.kind == BoundKind.ExpressionStatement &&
                node.syntax.kind is SyntaxKind.WithStatement or SyntaxKind.WithExpression) {
                return;
            }

            if (seenScopes.Add(syntax.parent))
                diagnostics.Push(Warning.UnreachableCode(syntax.location));
        }

        bool IsUserDeclaredGoto(SyntaxNode syntax) {
            switch (syntax.kind) {
                case SyntaxKind.ContinueStatement:
                case SyntaxKind.BreakStatement:
                    return true;
                default:
                    return false;
            }
        }

        bool IsUserDeclaredReturn(SyntaxNode syntax) {
            switch (syntax.kind) {
                case SyntaxKind.ReturnStatement:
                    return true;
                default:
                    return false;
            }
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

        if (statement.condition is BoundObjectCreationExpression { type.specialType: SpecialType.Nullable } o)
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

    internal override BoundNode VisitExpressionStatement(BoundExpressionStatement node) {
        /*

        <expression>

        ----> <expression> is call and method is pure and nothrow and the result is unused

        <args>

        */
        if (node.expression is BoundCallExpression call) {
            var syntax = node.syntax;
            var method = call.method;

            if (method.isPure && method.isNoThrow) {
                Debug.Assert(call.arguments.Length == method.parameterCount);

                if (method.parameterCount == 0)
                    return new BoundNopStatement(syntax);

                if (method.parameterCount == 1)
                    return Visit(Statement(syntax, call.arguments[0]));

                return Visit(Block(syntax, call.arguments.Select(a => Statement(a.syntax, a)).ToArray()));
            }
        }

        return base.VisitExpressionStatement(node);
    }

    internal override BoundNode VisitCallExpression(BoundCallExpression node) {
        /*

        <method>(<args>)

        ----> <method> is pure and the arguments are constant

        $?<method>(<args>)

        */
        if (!_isFirstPass && node.method.isPure) {
            var constArgs = true;

            foreach (var arg in node.arguments) {
                if (arg.constantValue is null) {
                    constArgs = false;
                    break;
                }
            }

            if (constArgs) {
                var rewritten = (BoundExpression)base.VisitCallExpression(node);
                _createdCompileTimeExpression = true;
                return new BoundCompileTimeExpression(node.syntax, rewritten, conditional: true, null, rewritten.type);
            }
        }

        return base.VisitCallExpression(node);
    }
}

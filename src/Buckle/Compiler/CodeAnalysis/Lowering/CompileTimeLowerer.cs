using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Evaluating;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class CompileTimeLowerer : BoundTreeRewriter {
    private readonly BelteDiagnosticQueue _diagnostics;
    private readonly Evaluator _evaluator;

    private CompileTimeLowerer(BoundProgram program, EvaluatorContext context, BelteDiagnosticQueue diagnostics) {
        _diagnostics = diagnostics;
        _evaluator = new Evaluator(program, context, []);
    }

    internal static BoundStatement Lower(
        BoundStatement statement,
        BelteDiagnosticQueue diagnostics,
        BoundProgram program,
        EvaluatorContext context) {
        var lowerer = new CompileTimeLowerer(program, context, diagnostics);
        return (BoundStatement)lowerer.Visit(statement);
    }

    internal override BoundNode VisitCompileTimeExpression(BoundCompileTimeExpression node) {
        try {
            var result = _evaluator.EvaluateExpression(node.expression, out var hasValue);
            return BoundFactory.Literal(node.syntax, result, node.type);
        } catch {
            if (!node.conditional)
                _diagnostics.Push(Error.InvalidCompileTimeExpression(node.syntax.location));

            return node.expression;
        }
    }
}

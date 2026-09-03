using System.Collections.Generic;
using System.Diagnostics;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Lowering;

// Only used for "late" constants (compile-time expressions)
internal sealed class ConstantFoldingPass : BoundTreeRewriterWithStackGuard {
    private readonly MethodSymbol _method;
    private readonly Dictionary<Symbol, ConstantValue> _constantMap;
    private readonly BelteDiagnosticQueue _diagnostics;

    private bool _madeProgress;

    private ConstantFoldingPass(
        MethodSymbol method,
        Dictionary<Symbol, ConstantValue> constantMap,
        BelteDiagnosticQueue diagnostics) {
        _method = method;
        _constantMap = constantMap;
        _diagnostics = diagnostics;
    }

    internal static BoundBlockStatement Fold(
        MethodSymbol method,
        BoundBlockStatement body,
        Dictionary<Symbol, ConstantValue> constantMap,
        out bool madeProgress,
        BelteDiagnosticQueue diagnostics) {
        var constantFolding = new ConstantFoldingPass(method, constantMap, diagnostics);

        try {
            return (BoundBlockStatement)constantFolding.Visit(body);
        } finally {
            madeProgress = constantFolding._madeProgress;
        }
    }

    internal override BoundNode VisitDataContainerExpression(BoundDataContainerExpression node) {
        if (_constantMap.TryGetValue(node.dataContainer, out var constant))
            return new BoundLiteralExpression(node.syntax, constant, node.type);

        return base.VisitDataContainerExpression(node);
    }

    internal override BoundNode VisitStackSlotExpression(BoundStackSlotExpression node) {
        if (_constantMap.TryGetValue(node.symbol, out var constant))
            return new BoundLiteralExpression(node.syntax, constant, node.type);

        return base.VisitStackSlotExpression(node);
    }

    internal override BoundNode VisitFieldAccessExpression(BoundFieldAccessExpression node) {
        if (_constantMap.TryGetValue(node.field, out var constant)) {
            _madeProgress = true;
            return new BoundLiteralExpression(node.syntax, constant, node.type);
        }

        return base.VisitFieldAccessExpression(node);
    }

    internal override BoundNode VisitFieldSlotExpression(BoundFieldSlotExpression node) {
        if (_constantMap.TryGetValue(node.field, out var constant)) {
            _madeProgress = true;
            return new BoundLiteralExpression(node.syntax, constant, node.type);
        }

        return base.VisitFieldSlotExpression(node);
    }

    internal override BoundNode VisitBinaryOperator(BoundBinaryOperator node) {
        var left = (BoundExpression)Visit(node.left);
        var right = (BoundExpression)Visit(node.right);

        if (left.constantValue is not null && right.constantValue is not null) {
            var diagnostics = BelteDiagnosticQueue.GetInstance();

            try {
                var constant = ConstantFolding.FoldBinary(
                    left,
                    right,
                    node.operatorKind,
                    node.type,
                    node.syntax.location,
                    diagnostics
                );

                if (diagnostics.Any())
                    return Error(node);

                if (constant is not null || _diagnostics.Any())
                    return new BoundLiteralExpression(node.syntax, constant, node.type);
            } finally {
                _diagnostics.PushRangeAndFree(diagnostics);
            }
        }

        return node.Update(left, right, node.operatorKind, node.method, node.constantValue, node.type);
    }

    internal override BoundNode VisitUnaryOperator(BoundUnaryOperator node) {
        var operand = (BoundExpression)Visit(node.operand);

        if (operand.constantValue is not null) {
            var constant = ConstantFolding.FoldUnary(operand, node.operatorKind, node.type);

            if (constant is not null)
                return new BoundLiteralExpression(node.syntax, constant, node.type);
        }

        return node.Update(operand, node.operatorKind, node.method, node.constantValue, node.type);
    }

    internal override BoundNode VisitIsOperator(BoundIsOperator node) {
        var left = (BoundExpression)Visit(node.left);
        var right = (BoundExpression)Visit(node.right);

        if (left.constantValue is not null && right.constantValue is not null) {
            var constant = ConstantFolding.FoldIs(left, right, node.isNot);

            if (constant is not null)
                return new BoundLiteralExpression(node.syntax, constant, node.type);
        }

        return node.Update(left, right, node.isNot, node.constantValue, node.type);
    }

    internal override BoundNode VisitNullAssertOperator(BoundNullAssertOperator node) {
        var operand = (BoundExpression)Visit(node.operand);

        if (operand.constantValue is not null) {
            var constant = ConstantFolding.FoldNullAssert(operand);

            if (constant is not null)
                return new BoundLiteralExpression(node.syntax, constant, node.type);
        }

        return node.Update(operand, node.throwIfNull, node.constantValue, node.type);
    }

    internal override BoundNode VisitConditionalOperator(BoundConditionalOperator node) {
        var condition = (BoundExpression)Visit(node.condition);
        var trueExpression = (BoundExpression)Visit(node.trueExpression);
        var falseExpression = (BoundExpression)Visit(node.falseExpression);

        if (condition.constantValue is not null &&
            trueExpression.constantValue is not null &&
            falseExpression.constantValue is not null) {
            var constant = ConstantFolding.FoldConditional(
                condition,
                trueExpression,
                falseExpression,
                node.type
            );

            if (constant is not null)
                return new BoundLiteralExpression(node.syntax, constant, node.type);
        }

        return node.Update(condition, node.isRef, trueExpression, falseExpression, node.constantValue, node.type);
    }

    internal override BoundNode VisitCastExpression(BoundCastExpression node) {
        var operand = (BoundExpression)Visit(node.operand);

        if (operand.constantValue is not null) {
            var diagnostics = BelteDiagnosticQueue.GetInstance();

            try {
                var constant = ConstantFolding.FoldCast(operand, new TypeWithAnnotations(node.type), diagnostics);

                if (diagnostics.Any())
                    return Error(node);

                if (constant is not null)
                    return new BoundLiteralExpression(node.syntax, constant, node.type);
            } finally {
                _diagnostics.PushRangeAndFree(diagnostics);
            }
        }

        return node.Update(operand, node.conversion, node.constantValue, node.type);
    }

    internal override BoundNode VisitArrayAccessExpression(BoundArrayAccessExpression node) {
        var receiver = (BoundExpression)Visit(node.receiver);
        var index = (BoundExpression)Visit(node.index);

        if (receiver.constantValue is not null && index.constantValue is not null) {
            var constant = ConstantFolding.FoldIndex(receiver, index, node.type);

            if (constant is not null)
                return new BoundLiteralExpression(node.syntax, constant, node.type);
        }

        return node.Update(receiver, index, node.constantValue, node.type);
    }

    internal override BoundNode VisitClampOperator(BoundClampOperator node) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override BoundNode VisitBitCastExpression(BoundBitCastExpression node) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override BoundNode VisitCompileTimeExpression(BoundCompileTimeExpression node) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override BoundNode VisitNullCoalescingOperator(BoundNullCoalescingOperator node) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override BoundNode VisitAssignmentOperator(BoundAssignmentOperator node) {
        var left = (BoundExpression)Visit(node.left);
        var right = (BoundExpression)Visit(node.right);

        if (left.expressionSymbol is { } symbol && symbol.IsConstExpr() &&
            right.constantValue is not null) {
            if (symbol.kind == SymbolKind.Field)
                _madeProgress = true;

            _constantMap.Add(symbol, right.constantValue);
            return null;
        }

        Debug.Assert(!(node.left.expressionSymbol is DataContainerSymbol d && d.isConstExpr) || right.hasErrors);

        return node.Update(left, right, node.isRef, node.type);
    }

    internal override BoundNode VisitExpressionStatement(BoundExpressionStatement node) {
        var expression = Visit(node.expression) as BoundExpression;

        if (expression is null)
            return new BoundNopStatement(node.syntax);

        return node.Update(expression);
    }

    internal override BoundNode VisitLocalDeclarationStatement(BoundLocalDeclarationStatement node) {
        var declaration = (BoundDataContainerDeclaration)Visit(node.declaration);

        if (declaration.dataContainer.isConstExpr && declaration.initializer.constantValue is not null) {
            _constantMap.Add(declaration.dataContainer, declaration.initializer.constantValue);
            return new BoundNopStatement(node.syntax);
        }

        Debug.Assert(!declaration.dataContainer.isConstExpr);

        return node.Update(declaration, node.isScoped, node.disposeMethod);
    }

    private static BoundErrorExpression Error(BoundExpression node) {
        return new BoundErrorExpression(node.syntax, LookupResultKind.Empty, [], [node], node.type, hasErrors: true);
    }
}

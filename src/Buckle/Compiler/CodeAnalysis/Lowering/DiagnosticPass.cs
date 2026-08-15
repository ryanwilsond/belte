using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Lowering;

// TODO Many more warnings we could check for here
internal sealed class DiagnosticPass : BoundTreeWalkerWithStackGuard {
    private readonly BelteDiagnosticQueue _diagnostics;
    private readonly NamedTypeSymbol _entryType;

    private bool _seenPossibleThrowingNode;

    private DiagnosticPass(BelteDiagnosticQueue diagnostics, NamedTypeSymbol entryType) {
        _diagnostics = diagnostics;
        _entryType = entryType;
    }

    internal static void ReportDiagnostics(
        BoundNode node,
        BelteDiagnosticQueue diagnostics,
        NamedTypeSymbol entryType) {
        try {
            var diagnosticPass = new DiagnosticPass(diagnostics, entryType);
            diagnosticPass.Visit(node);
        } catch (CancelledByStackGuardException ex) {
            ex.AddAnError(diagnostics);
        }
    }

    internal override BoundNode VisitTryStatement(BoundTryStatement node) {
        _seenPossibleThrowingNode = false;
        Visit(node.body);

        if (!_seenPossibleThrowingNode && node.catchBody is not null && node.finallyBody is null)
            _diagnostics.Push(Warning.UnnecessaryTryStatement(node.syntax.location));

        Visit(node.catchBody);
        Visit(node.finallyBody);
        return null;
    }

    internal override BoundNode VisitExpressionStatement(BoundExpressionStatement node) {
        if (node.expression is BoundCallExpression call && !call.method.returnsVoid) {
            if (call.method.hasMustUseReturnValueAttribute)
                _diagnostics.Push(Error.IgnoringRequiredReturnValue(call.syntax.location, call.method));
            else if (call.method is not ErrorMethodSymbol)
                _diagnostics.Push(Warning.IgnoringReturnValue(call.syntax.location, call.method));
        } else if (node.expression is BoundFunctionPointerCallExpression pCall &&
            !pCall.functionPointer.signature.returnsVoid) {
            _diagnostics.Push(Warning.IgnoringReturnValue(pCall.syntax.location, pCall.functionPointer.signature));
        }

        return base.VisitExpressionStatement(node);
    }

    internal override BoundNode VisitAssignmentOperator(BoundAssignmentOperator node) {
        CheckForAssignmentToSelf(node);
        return base.VisitAssignmentOperator(node);
    }

    private bool CheckForAssignmentToSelf(BoundAssignmentOperator node) {
        if (!node.hasAnyErrors && IsSameLocalOrField(node.left, node.right)) {
            _diagnostics.Push(Warning.AssignmentToSelf(node.syntax.location));
            return true;
        }

        return false;
    }

    private static BoundExpression StripImplicitCasts(BoundExpression expr) {
        var current = expr;

        while (true) {
            if (current is not BoundCastExpression conversion || !conversion.conversion.kind.IsImplicitCast())
                return current;

            current = conversion.operand;
        }
    }

    private static bool IsSameLocalOrField(BoundExpression expr1, BoundExpression expr2) {
        if (expr1 is null && expr2 is null)
            return true;

        if (expr1 is null || expr2 is null)
            return false;

        if (expr1.hasAnyErrors || expr2.hasAnyErrors)
            return false;

        expr1 = StripImplicitCasts(expr1);
        expr2 = StripImplicitCasts(expr2);

        if (expr1.kind != expr2.kind)
            return false;

        switch (expr1.kind) {
            case BoundKind.DataContainerExpression:
                var local1 = (BoundDataContainerExpression)expr1;
                var local2 = (BoundDataContainerExpression)expr2;
                return local1.dataContainer == local2.dataContainer;
            case BoundKind.FieldAccessExpression:
                var field1 = (BoundFieldAccessExpression)expr1;
                var field2 = (BoundFieldAccessExpression)expr2;
                return field1.field == field2.field &&
                    (field1.field.isStatic || IsSameLocalOrField(field1.receiver, field2.receiver));
            case BoundKind.ParameterExpression:
                var param1 = (BoundParameterExpression)expr1;
                var param2 = (BoundParameterExpression)expr2;
                return param1.parameter == param2.parameter;
            case BoundKind.ThisExpression:
                return true;
            default:
                return false;
        }
    }

    #region NoThrow Checking

    // TODO If 'nothrow' becomes more important/prevalent, it may be worth storing whether a node can throw directly
    // on the node itself and propagate that boolean value instead of performing all of these checks

    // TODO Potentially missed some, need to double check (same with todo comment in Conversion)
    // Note that some exceptions don't "count", e.g. stackalloc expressions can throw StackOverflowException
    // but we don't count that because it cannot be caught
    // Similarly, pointer related segfaults don't count because CorruptedStateException also cannot be caught

    internal override BoundNode VisitArrayAccessExpression(BoundArrayAccessExpression node) {
        _seenPossibleThrowingNode = true;
        return base.VisitArrayAccessExpression(node);
    }

    internal override BoundNode VisitArrayCreationExpression(BoundArrayCreationExpression node) {
        // Possible if array size exceeds runtime limit (int32 I believe?)
        // TODO Maybe all exception cases should be caught statically instead of at runtime?
        return base.VisitArrayCreationExpression(node);
    }

    internal override BoundNode VisitBinaryOperator(BoundBinaryOperator node) {
        _seenPossibleThrowingNode |= node.method is not null && !node.method.isNoThrow;
        return base.VisitBinaryOperator(node);
    }

    internal override BoundNode VisitCallExpression(BoundCallExpression node) {
        _seenPossibleThrowingNode |= !node.method.isNoThrow;
        return base.VisitCallExpression(node);
    }

    internal override BoundNode VisitCastExpression(BoundCastExpression node) {
        _seenPossibleThrowingNode |= node.conversion.CouldThrow();
        return base.VisitCastExpression(node);
    }

    internal override BoundNode VisitCStringLiteral(BoundCStringLiteral node) {
        // TODO This is true because it has to call a non nothrow helper?
        _seenPossibleThrowingNode = true;
        return base.VisitCStringLiteral(node);
    }

    internal override BoundNode VisitForEachStatement(BoundForEachStatement node) {
        // TODO We need to restructure foreach to always use enumeratorInfo to consolidate the methods
        // so its easier to check whether or not they are all marked nothrow

        if (node.enumeratorInfo is not null) {
            _seenPossibleThrowingNode |= !node.enumeratorInfo.disposeMethod.isNoThrow ||
                                         !node.enumeratorInfo.getCurrentMethod.isNoThrow ||
                                         !node.enumeratorInfo.getEnumeratorMethod.isNoThrow ||
                                         !node.enumeratorInfo.moveNextMethod.isNoThrow;
        }

        return base.VisitForEachStatement(node);
    }

    internal override BoundNode VisitIncrementOperator(BoundIncrementOperator node) {
        _seenPossibleThrowingNode |= node.method is not null && !node.method.isNoThrow;
        return base.VisitIncrementOperator(node);
    }

    internal override BoundNode VisitIndexerAccessExpression(BoundIndexerAccessExpression node) {
        _seenPossibleThrowingNode |= (node.method is not null && !node.method.isNoThrow) ||
            node.receiver.StrippedType().specialType == SpecialType.String;

        return base.VisitIndexerAccessExpression(node);
    }

    internal override BoundNode VisitInitializerDictionary(BoundInitializerDictionary node) {
        // Possible if duplicate keys
        // TODO Maybe all exception cases should be caught statically instead of at runtime?
        _seenPossibleThrowingNode = true;
        return base.VisitInitializerDictionary(node);
    }

    internal override BoundNode VisitInitializerList(BoundInitializerList node) {
        // Possible if array size exceeds runtime limit (int32 I believe?)
        // TODO Maybe all exception cases should be caught statically instead of at runtime?
        _seenPossibleThrowingNode = true;
        return base.VisitInitializerList(node);
    }

    internal override BoundNode VisitInlineILStatement(BoundInlineILStatement node) {
        // TODO Check each instruction for throwing potential
        // Right now we will just assume yes
        _seenPossibleThrowingNode = true;
        return base.VisitInlineILStatement(node);
    }

    internal override BoundNode VisitNullAssertOperator(BoundNullAssertOperator node) {
        _seenPossibleThrowingNode |= node.throwIfNull;
        return base.VisitNullAssertOperator(node);
    }

    internal override BoundNode VisitObjectCreationExpression(BoundObjectCreationExpression node) {
        _seenPossibleThrowingNode |= !node.constructor.isNoThrow;

        if (_entryType is not null && node.type.Equals(_entryType))
            _diagnostics.Push(Error.CannotCreateEntryType(node.syntax.location));

        return base.VisitObjectCreationExpression(node);
    }

    internal override BoundNode VisitReverseStatement(BoundReverseStatement node) {
        // TODO reverse clause doesn't allow specifiers so it cannot be marked 'nothrow'
        // TODO But don't they inherit the 'nothrow' specifier from the main method? So then this wouldn't always throw

        // If reversing becomes prevalent enough we should consider allowing specifiers on these clauses
        _seenPossibleThrowingNode = true;
        return base.VisitReverseStatement(node);
    }

    internal override BoundNode VisitThrowExpression(BoundThrowExpression node) {
        _seenPossibleThrowingNode = true;
        return base.VisitThrowExpression(node);
    }

    internal override BoundNode VisitUnaryOperator(BoundUnaryOperator node) {
        _seenPossibleThrowingNode |= node.method is not null && !node.method.isNoThrow;
        return base.VisitUnaryOperator(node);
    }

    internal override BoundNode VisitUnreachableStatement(BoundUnreachableStatement node) {
        _seenPossibleThrowingNode = true;
        return base.VisitUnreachableStatement(node);
    }

    #endregion

}

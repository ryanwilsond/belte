using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Lowering;

// TODO Many more warnings we could check for here
internal sealed partial class DiagnosticPass : BoundTreeWalkerWithStackGuard {
    private readonly MethodSymbol _method;
    private readonly BelteDiagnosticQueue _diagnostics;
    private readonly NamedTypeSymbol _entryType;
    private readonly ParameterUsageInfo _usedParameters;
    private readonly Dictionary<LocalFunctionSymbol, ParameterUsageInfo> _localUsedParameters;
    private readonly Dictionary<DataContainerSymbol, LocalUsageInfo> _localUsage;

    private bool _seenPossibleThrowingNode;

    private DiagnosticPass(MethodSymbol method, BelteDiagnosticQueue diagnostics, NamedTypeSymbol entryType) {
        _method = method;
        _diagnostics = diagnostics;
        _entryType = entryType;
        _usedParameters = new ParameterUsageInfo(_method.parameterCount);
        _localUsedParameters = [];
        _localUsage = [];
    }

    internal static void ReportDiagnostics(
        BoundNode node,
        MethodSymbol method,
        BelteDiagnosticQueue diagnostics,
        NamedTypeSymbol entryType) {
        try {
            var diagnosticPass = new DiagnosticPass(method, diagnostics, entryType);
            diagnosticPass.Visit(node);

            ReportUnusedParameters(method, diagnosticPass._usedParameters, diagnostics);

            foreach (var pair in diagnosticPass._localUsedParameters)
                ReportUnusedParameters(pair.Key, pair.Value, diagnostics);

            AnalyzeLocalUsage(method.declaringCompilation, diagnosticPass._localUsage, diagnostics);
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

        if (node.right is BoundParameterExpression parameterExpression &&
            node.left.kind == BoundKind.DiscardExpression) {
            VisitDiscardedParameterExpression(parameterExpression, node.syntax.location);
            return null;
        }

        NoteUsedAsVar(node.left);

        if (node.left is BoundDataContainerExpression dataContainerExpression)
            UpdateLocalUsage(dataContainerExpression.dataContainer, LocalUsageInfo.Reassigned);

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

    #region Parameter Usage


    private static void ReportUnusedParameters(
        MethodSymbol method,
        ParameterUsageInfo usedParameters,
        BelteDiagnosticQueue diagnostics) {
        for (var i = 0; i < method.parameterCount; i++) {
            if (!usedParameters.used[i]) {
                var parameter = method.parameters[i];
                var name = parameter.name;

                // Just a convention, no further semantic meaning
                if (!name.StartsWith('_'))
                    diagnostics.Push(Warning.UnusedParameter(parameter.location, method, name));
            } else if (usedParameters.usedIgnoringDiscard[i]) {
                foreach (var location in usedParameters.discardLocations)
                    diagnostics.Push(Warning.UnnecessaryParameterDiscard(location, method.parameters[i].name));
            }
        }
    }

    internal override BoundNode VisitLocalFunctionStatement(BoundLocalFunctionStatement node) {
        _localUsedParameters.Add(node.symbol, new ParameterUsageInfo(node.symbol.parameterCount));
        return base.VisitLocalFunctionStatement(node);
    }

    private void VisitDiscardedParameterExpression(BoundParameterExpression node, TextLocation location) {
        if (node.parameter.containingSymbol.Equals(_method)) {
            _usedParameters.used[node.parameter.ordinal] = true;
            _usedParameters.discardLocations.Add(location);
        } else if (node.parameter.containingSymbol is LocalFunctionSymbol localFunctionSymbol) {
            var localUsageInfo = _localUsedParameters[localFunctionSymbol];
            localUsageInfo.used[node.parameter.ordinal] = true;
            localUsageInfo.discardLocations.Add(location);
        } else {
            Debug.Assert(node.parameter.containingSymbol is MethodSymbol);
            Debug.Assert(_method is SourceReverseMethodSymbol or SourceStateMethodSymbol);
        }
    }

    internal override BoundNode VisitParameterExpression(BoundParameterExpression node) {
        if (node.parameter.containingSymbol.Equals(_method)) {
            _usedParameters.used[node.parameter.ordinal] = true;
            _usedParameters.usedIgnoringDiscard[node.parameter.ordinal] = true;
        } else if (node.parameter.containingSymbol is LocalFunctionSymbol localFunctionSymbol) {
            var localUsageInfo = _localUsedParameters[localFunctionSymbol];
            localUsageInfo.used[node.parameter.ordinal] = true;
            localUsageInfo.usedIgnoringDiscard[node.parameter.ordinal] = true;
        } else {
            Debug.Assert(node.parameter.containingSymbol is MethodSymbol);
            Debug.Assert(_method is SourceReverseMethodSymbol or SourceStateMethodSymbol);
            // TODO Reverse/state clauses don't contribute to target method parameter usage, but maybe they should?
        }

        return base.VisitParameterExpression(node);
    }

    #endregion

    #region Local Usage

    // TODO From initial testing the warning messages are accurate (i.e. don't suggest semantic errors)
    // But probably needs more testing
    private static void AnalyzeLocalUsage(
        Compilation compilation,
        Dictionary<DataContainerSymbol, LocalUsageInfo> localUsage,
        BelteDiagnosticQueue diagnostics) {
        var mergedUsage = MergeLocalUsage(localUsage);

        foreach (var (local, state) in mergedUsage) {
            if (!local.IsFromCompilation(compilation) || local.isCompilerGenerated || local.isGlobal)
                continue;

            var usage = state & LocalUsageInfo.UsagePertaining;

            if (usage == LocalUsageInfo.NotUsed) {
                // Implicit used when Destroy is called
                if (local.declarationKind is not DataContainerDeclarationKind.ScopedLocal)
                    diagnostics.Push(Warning.UnusedLocal(local.location, local.name));

                continue;
            }

            if ((usage & LocalUsageInfo.PassedByRefVar) != 0)
                continue;

            if (local.isConst || local.isConstExpr)
                continue;

            if ((usage & (LocalUsageInfo.Mutated | LocalUsageInfo.PassedByRefFinal | LocalUsageInfo.Reassigned)) == 0) {
                if ((state & LocalUsageInfo.HasConstExprInitializer) != 0)
                    diagnostics.Push(Warning.LocalCouldBeConstExpr(local.location, local.name));
                else
                    diagnostics.Push(Warning.LocalCouldBeConst(local.location, local.name));

                continue;
            }

            if (local.isFinal)
                continue;

            if ((usage & (LocalUsageInfo.Reassigned | LocalUsageInfo.PassedByRefConst)) == 0)
                diagnostics.Push(Warning.LocalCouldBeFinal(local.location, local.name));
        }
    }

    private static Dictionary<DataContainerSymbol, LocalUsageInfo> MergeLocalUsage(
        Dictionary<DataContainerSymbol, LocalUsageInfo> localUsage) {
        // Chained submissions might have multiple symbols that refer to the same local
        // In these cases we merge each individual usage's LocalUsageInfo into a consolidated one
        Dictionary<string, (DataContainerSymbol, LocalUsageInfo)> mergedInfo = [];

        foreach (var (local, usage) in localUsage) {
            if (!mergedInfo.TryAdd(local.name, (local, usage))) {
                var existing = mergedInfo[local.name];
                mergedInfo[local.name] = (existing.Item1, existing.Item2 | usage);
            }
        }

        return mergedInfo.ToDictionary(dict => dict.Value.Item1, dict => dict.Value.Item2);
    }

    internal override BoundNode VisitBlockStatement(BoundBlockStatement node) {
        foreach (var local in node.locals)
            _localUsage.TryAdd(local, LocalUsageInfo.NotUsed);

        return base.VisitBlockStatement(node);
    }

    internal override BoundNode VisitLocalDeclarationStatement(BoundLocalDeclarationStatement node) {
        _localUsage.TryAdd(node.declaration.dataContainer, LocalUsageInfo.NotUsed);

        if (node.declaration.initializer is { } initializer) {
            if (initializer.constantValue is not null || Binder.EnsureExpressionIsCompileTime(initializer, []))
                UpdateLocalUsage(node.declaration.dataContainer, LocalUsageInfo.HasConstExprInitializer);
        }

        return base.VisitLocalDeclarationStatement(node);
    }

    internal override BoundNode VisitDataContainerExpression(BoundDataContainerExpression node) {
        UpdateLocalUsage(node.dataContainer, LocalUsageInfo.Used);
        return base.VisitDataContainerExpression(node);
    }

    private void UpdateLocalUsage(DataContainerSymbol local, LocalUsageInfo usageInfo) {
        if (_localUsage.ContainsKey(local))
            _localUsage[local] |= usageInfo;
        else
            Debug.Assert(local.isGlobal);
    }

    internal override BoundNode VisitCallExpression(BoundCallExpression node) {
        _seenPossibleThrowingNode |= !node.method.isNoThrow;

        if (!node.method.isEffectivelyConst)
            NoteUsedAsVar(node.receiver);

        for (var i = 0; i < node.arguments.Length; i++) {
            var isConstParam = node.method.parameters[i].isConst;
            var refKind = node.argumentRefKinds.IsDefault ? RefKind.None : node.argumentRefKinds[i];

            if (!isConstParam)
                NoteUsedAsVar(node.arguments[i]);

            if (refKind != RefKind.None)
                NoteUsedAsRef(node.arguments[i], refKind);
        }

        return base.VisitCallExpression(node);
    }

    private void NoteUsedAsVar(BoundExpression node) {
        switch (node) {
            case BoundDataContainerExpression dataContainerExpression:
                UpdateLocalUsage(dataContainerExpression.dataContainer, LocalUsageInfo.Mutated);
                break;
            case BoundFieldAccessExpression fieldAccessExpression:
                NoteUsedAsVar(fieldAccessExpression.receiver);
                break;
            default:
                break;
        }
    }

    private void NoteUsedAsRef(BoundExpression node, RefKind refKind) {
        Debug.Assert(refKind != RefKind.None);

        switch (node) {
            case BoundDataContainerExpression dataContainerExpression:
                var local = dataContainerExpression.dataContainer;

                switch (refKind) {
                    case RefKind.Ref:
                        UpdateLocalUsage(local, LocalUsageInfo.PassedByRefVar);
                        break;
                    case RefKind.RefConst:
                        UpdateLocalUsage(local, LocalUsageInfo.PassedByRefConst);
                        break;
                    case RefKind.RefFinal:
                        UpdateLocalUsage(local, LocalUsageInfo.PassedByRefFinal);
                        break;
                    default:
                        Debug.Assert(refKind == RefKind.Out);
                        break;
                }

                break;
            case BoundFieldAccessExpression fieldAccessExpression:
                if (refKind != RefKind.RefConst)
                    NoteUsedAsVar(fieldAccessExpression.receiver);

                break;
            default:
                break;
        }
    }

    internal override BoundNode VisitCompoundAssignmentOperator(BoundCompoundAssignmentOperator node) {
        NoteUsedAsVar(node.left);

        if (node.left is BoundDataContainerExpression dataContainerExpression)
            UpdateLocalUsage(dataContainerExpression.dataContainer, LocalUsageInfo.Reassigned);

        return base.VisitCompoundAssignmentOperator(node);
    }

    internal override BoundNode VisitNullCoalescingAssignmentOperator(BoundNullCoalescingAssignmentOperator node) {
        NoteUsedAsVar(node.left);

        if (node.left is BoundDataContainerExpression dataContainerExpression)
            UpdateLocalUsage(dataContainerExpression.dataContainer, LocalUsageInfo.Reassigned);

        return base.VisitNullCoalescingAssignmentOperator(node);
    }

    internal override BoundNode VisitIncrementOperator(BoundIncrementOperator node) {
        _seenPossibleThrowingNode |= node.method is not null && !node.method.isNoThrow;

        NoteUsedAsVar(node.operand);

        if (node.operand is BoundDataContainerExpression dataContainerExpression)
            UpdateLocalUsage(dataContainerExpression.dataContainer, LocalUsageInfo.Reassigned);

        return base.VisitIncrementOperator(node);
    }

    internal override BoundNode VisitDoWhileStatement(BoundDoWhileStatement node) {
        foreach (var local in node.locals)
            _localUsage.TryAdd(local, LocalUsageInfo.NotUsed);

        return base.VisitDoWhileStatement(node);
    }

    internal override BoundNode VisitWhileStatement(BoundWhileStatement node) {
        foreach (var local in node.locals)
            _localUsage.TryAdd(local, LocalUsageInfo.NotUsed);

        return base.VisitWhileStatement(node);
    }

    internal override BoundNode VisitForStatement(BoundForStatement node) {
        foreach (var local in node.locals)
            _localUsage.TryAdd(local, LocalUsageInfo.NotUsed);

        foreach (var local in node.innerLocals)
            _localUsage.TryAdd(local, LocalUsageInfo.NotUsed);

        return base.VisitForStatement(node);
    }

    internal override BoundNode VisitNullBindingStatement(BoundNullBindingStatement node) {
        foreach (var local in node.locals)
            _localUsage.TryAdd(local, LocalUsageInfo.NotUsed);

        foreach (var local in node.innerLocals)
            _localUsage.TryAdd(local, LocalUsageInfo.NotUsed);

        return base.VisitNullBindingStatement(node);
    }

    internal override BoundNode VisitSwitchStatement(BoundSwitchStatement node) {
        foreach (var local in node.innerLocals)
            _localUsage.TryAdd(local, LocalUsageInfo.NotUsed);

        return base.VisitSwitchStatement(node);
    }

    internal override BoundNode VisitSwitchSection(BoundSwitchSection node) {
        foreach (var local in node.locals)
            _localUsage.TryAdd(local, LocalUsageInfo.NotUsed);

        return base.VisitSwitchSection(node);
    }

    #endregion

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
        if (node.enumeratorInfo is not null) {
            var info = node.enumeratorInfo;

            if (info.getCurrentMethod is not null && !info.getCurrentMethod.isNoThrow)
                _seenPossibleThrowingNode = true;
            else if (info.getEnumeratorMethod is not null && !info.getEnumeratorMethod.isNoThrow)
                _seenPossibleThrowingNode = true;
            else if (info.moveNextMethod is not null && !info.moveNextMethod.isNoThrow)
                _seenPossibleThrowingNode = true;
            else if (info.disposeMethod is not null && !info.disposeMethod.isNoThrow)
                _seenPossibleThrowingNode = true;
            else if (info.lengthOp is not null && !info.lengthOp.isNoThrow)
                _seenPossibleThrowingNode = true;
            else if (info.indexOp is not null && !info.indexOp.isNoThrow)
                _seenPossibleThrowingNode = true;
            else if (info.iterOp is not null && !info.iterOp.isNoThrow)
                _seenPossibleThrowingNode = true;

            // TODO Can range foreach throw?
        }

        _localUsage.TryAdd(node.valueLocal, LocalUsageInfo.NotUsed);

        if (node.indexLocal is not null)
            _localUsage.TryAdd(node.indexLocal, LocalUsageInfo.NotUsed);

        foreach (var local in node.locals)
            _localUsage.TryAdd(local, LocalUsageInfo.NotUsed);

        foreach (var local in node.innerLocals)
            _localUsage.TryAdd(local, LocalUsageInfo.NotUsed);

        return base.VisitForEachStatement(node);
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

    internal override BoundNode VisitOrThrowExpression(BoundOrThrowExpression node) {
        _seenPossibleThrowingNode = true;
        return base.VisitOrThrowExpression(node);
    }

    #endregion

}

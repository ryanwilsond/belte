using System.Collections.Immutable;
using System.Diagnostics;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class ForEachLoopBinder : LoopBinder {
    private readonly ForEachStatementSyntax _syntax;
    private SourceDataContainerSymbol _valueSymbol;
    private SourceDataContainerSymbol _indexSymbol;

    internal ForEachLoopBinder(Binder enclosing, ForEachStatementSyntax syntax)
        : base(enclosing) {
        _syntax = syntax;
    }

    internal override SyntaxNode scopeDesignator => _syntax;

    private protected override ImmutableArray<DataContainerSymbol> BuildLocals() {
        var locals = ArrayBuilder<DataContainerSymbol>.GetInstance();

        _valueSymbol = SourceDataContainerSymbol.MakeDeconstructionLocal(
            containingMember,
            this,
            this,
            null,
            _syntax.valueIdentifier,
            DataContainerDeclarationKind.ForEachLocal,
            _syntax
        );

        locals.Add(_valueSymbol);

        if (_syntax.indexIdentifier is not null) {
            _indexSymbol = SourceDataContainerSymbol.MakeDeconstructionLocal(
                containingMember,
                this,
                this,
                null,
                _syntax.indexIdentifier,
                DataContainerDeclarationKind.ForEachLocal,
                _syntax
            );

            locals.Add(_indexSymbol);
        }

        return locals.ToImmutableAndFree();
    }

    internal override BoundForEachStatement BindForEachParts(BelteDiagnosticQueue diagnostics, Binder originalBinder) {
        return BindForEachParts(_syntax, originalBinder, diagnostics);
    }

    internal override BoundStatement BindForEachDeconstruction(
        BelteDiagnosticQueue diagnostics,
        Binder originalBinder) {
        var collectionExpr = originalBinder.GetBinder(_syntax.expression)
            .BindRValueWithoutTargetType(_syntax.expression, diagnostics);

        _ = BindForEachCollection(
            _syntax,
            _syntax.expression,
            ref collectionExpr,
            diagnostics,
            out var inferredType
        );

        _valueSymbol.SetTypeWithAnnotations(inferredType);
        _indexSymbol?.SetTypeWithAnnotations(new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Int)));

        return new BoundExpressionStatement(_syntax, BoundFactory.Local(_syntax, _valueSymbol));
    }

    private BoundForEachStatement BindForEachParts(
        ForEachStatementSyntax node,
        Binder originalBinder,
        BelteDiagnosticQueue diagnostics) {
        _ = locals;

        var collectionExpr = originalBinder.GetBinder(_syntax.expression)
            .BindRValueWithoutTargetType(_syntax.expression, diagnostics);

        var forEachKind = BindForEachCollection(
            _syntax,
            _syntax.expression,
            ref collectionExpr,
            diagnostics,
            out var inferredType
        );

        var enumeratorInfo = forEachKind == ForEachLoopKind.IEnumerable
            ? BindEnumeratorInfo(_syntax, _syntax.expression, collectionExpr.type, diagnostics)
            : null;

        _valueSymbol.SetTypeWithAnnotations(inferredType);
        _indexSymbol?.SetTypeWithAnnotations(new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Int)));

        var body = originalBinder.BindPossibleEmbeddedStatement(node.body, diagnostics);

        return new BoundForEachStatement(
            node,
            enumeratorInfo,
            forEachKind,
            locals,
            collectionExpr,
            [],
            _valueSymbol,
            _indexSymbol,
            body,
            breakLabel,
            continueLabel
        );
    }

    private ForEachEnumeratorInfo BindEnumeratorInfo(
        SyntaxNode syntax,
        SyntaxNode collectionSyntax,
        TypeSymbol type,
        BelteDiagnosticQueue diagnostics) {
        var getEnumeratorMethod = FindForEachMethod(
            syntax,
            collectionSyntax,
            type,
            WellKnownMemberNames.GetEnumeratorMethodName,
            false,
            diagnostics
        );

        var enumeratorType = getEnumeratorMethod.returnType;

        var moveNextMethod = FindForEachMethod(
            syntax,
            collectionSyntax,
            enumeratorType,
            WellKnownMemberNames.MoveNextMethodName,
            false,
            diagnostics
        );

        var getCurrentMethod = FindForEachMethod(
            syntax,
            collectionSyntax,
            enumeratorType,
            WellKnownMemberNames.CurrentPropertyName,
            false,
            diagnostics
        );

        var disposeMethod = FindForEachMethod(
            syntax,
            collectionSyntax,
            enumeratorType,
            WellKnownMemberNames.Dispose,
            false,
            diagnostics
        );

        return new ForEachEnumeratorInfo(getEnumeratorMethod, moveNextMethod, getCurrentMethod, disposeMethod);
    }

    private MethodSymbol FindForEachMethod(
        SyntaxNode syntax,
        SyntaxNode collectionSyntax,
        TypeSymbol type,
        string methodName,
        bool warningsOnly,
        BelteDiagnosticQueue diagnostics) {
        var lookupResult = LookupResult.GetInstance();

        try {
            LookupMembersInType(
                lookupResult,
                type,
                methodName,
                arity: 0,
                basesBeingResolved: null,
                options: LookupOptions.Default,
                originalBinder: this,
                errorLocation: collectionSyntax.location,
                diagnose: false
            );

            if (!lookupResult.isMultiViable) {
                ReportPatternMemberLookupDiagnostics(
                    collectionSyntax,
                    lookupResult,
                    type,
                    methodName,
                    warningsOnly,
                    diagnostics
                );

                return null;
            }

            var candidateMethods = ArrayBuilder<MethodSymbol>.GetInstance();

            foreach (var member in lookupResult.symbols) {
                if (member.kind != SymbolKind.Method) {
                    candidateMethods.Free();

                    if (warningsOnly)
                        ReportEnumerableWarning(collectionSyntax, diagnostics, type, member);

                    return null;
                }

                var method = (MethodSymbol)member;

                if (method.parameterCount == 0)
                    candidateMethods.Add((MethodSymbol)member);
            }

            var methodSymbol = PerformForEachOverloadResolution(
                syntax,
                collectionSyntax,
                type,
                candidateMethods,
                warningsOnly,
                diagnostics
            );

            candidateMethods.Free();

            return methodSymbol;
        } finally {
            lookupResult.Free();
        }
    }

    private MethodSymbol PerformForEachOverloadResolution(
        SyntaxNode syntax,
        SyntaxNode collectionSyntax,
        TypeSymbol patternType,
        ArrayBuilder<MethodSymbol> candidateMethods,
        bool warningsOnly,
        BelteDiagnosticQueue diagnostics) {
        var analyzedArguments = AnalyzedArguments.GetInstance();
        var templateArguments = ArrayBuilder<TypeOrConstant>.GetInstance();
        var overloadResolutionResult = OverloadResolutionResult<MethodSymbol>.GetInstance();

        var dummyReceiver = new BoundValuePlaceholder(collectionSyntax, patternType);

        overloadResolution.MethodOverloadResolution(
            members: candidateMethods,
            templateArguments: templateArguments,
            receiver: dummyReceiver,
            arguments: analyzedArguments,
            result: overloadResolutionResult
        );

        MethodSymbol result = null;

        if (overloadResolutionResult.succeeded) {
            result = overloadResolutionResult.bestResult.member;

            if (result.isStatic || result.declaredAccessibility != Accessibility.Public) {
                if (warningsOnly) {
                    throw ExceptionUtilities.Unreachable();
                    // MessageID patternName = isAsync ? MessageID.IDS_FeatureAsyncStreams : MessageID.IDS_Collection;
                    // diagnostics.Add(ErrorCode.WRN_PatternNotPublicOrNotInstance, collectionSyntax.Location, patternType, patternName.Localize(), result);
                }

                result = null;
            } else {
                var argsToParams = overloadResolutionResult.bestResult.result.argsToParams;
                var expanded = overloadResolutionResult.bestResult.result.kind == MemberResolutionKind.Applicable;

                BindDefaultArguments(
                    syntax,
                    result.parameters,
                    analyzedArguments.arguments,
                    analyzedArguments.refKinds,
                    analyzedArguments.names,
                    ref argsToParams,
                    out var defaultArguments,
                    expanded,
                    diagnostics
                );

                // TODO Currently we never are finding methods with any parameters
                Debug.Assert(result.parameterCount == 0);
                // Debug.Assert(argsToParams.IsDefault);
                // info = new MethodArgumentInfo(result, analyzedArguments.Arguments.ToImmutable(), defaultArguments, expanded);
            }
        }
        // else if (overloadResolutionResult.GetAllApplicableMembers() is var applicableMembers &&
        //     applicableMembers.Length > 1) {
        //     if (warningsOnly) {
        // diagnostics.Add(ErrorCode.WRN_PatternIsAmbiguous, collectionSyntax.Location, patternType, MessageID.IDS_Collection.Localize(),
        //     applicableMembers[0], applicableMembers[1]);
        //     }
        // }

        overloadResolutionResult.Free();
        analyzedArguments.Free();
        templateArguments.Free();

        return result;
    }

    private void ReportPatternMemberLookupDiagnostics(
        SyntaxNode collectionSyntax,
        LookupResult lookupResult,
        TypeSymbol type,
        string memberName,
        bool warningsOnly,
        BelteDiagnosticQueue diagnostics) {
        if (lookupResult.symbols.Any()) {
            if (warningsOnly) {
                ReportEnumerableWarning(collectionSyntax, diagnostics, type, lookupResult.symbols.First());
            } else {
                lookupResult.Clear();

                LookupMembersInType(
                    lookupResult,
                    type,
                    memberName,
                    arity: 0,
                    basesBeingResolved: null,
                    options: LookupOptions.Default,
                    originalBinder: this,
                    errorLocation: collectionSyntax.location,
                    diagnose: true
                );

                if (lookupResult.error is not null)
                    diagnostics.Push(lookupResult.error);
            }
        } else if (!warningsOnly) {
            diagnostics.Push(Error.NoSuchMember(collectionSyntax.location, type, memberName));
        }
    }

    private void ReportEnumerableWarning(
        SyntaxNode collectionSyntax,
        BelteDiagnosticQueue diagnostics,
        TypeSymbol enumeratorType,
        Symbol patternMemberCandidate) {
        if (IsAccessible(patternMemberCandidate)) {
            throw ExceptionUtilities.Unreachable();
            // diagnostics.Add(ErrorCode.WRN_PatternBadSignature, collectionSyntax.Location, enumeratorType, MessageID.IDS_Collection.Localize(), patternMemberCandidate);
        }
    }

    internal override ImmutableArray<DataContainerSymbol> GetDeclaredLocalsForScope(SyntaxNode scopeDesignator) {
        if (_syntax == scopeDesignator)
            return locals;

        throw ExceptionUtilities.Unreachable();
    }

    internal override ImmutableArray<LocalFunctionSymbol> GetDeclaredLocalFunctionsForScope(
        BelteSyntaxNode scopeDesignator) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override ImmutableArray<TokenSymbol> GetDeclaredTokensForScope(SyntaxNode scopeDesignator) {
        if (this.scopeDesignator == scopeDesignator)
            return tokens;

        throw ExceptionUtilities.Unreachable();
    }
}

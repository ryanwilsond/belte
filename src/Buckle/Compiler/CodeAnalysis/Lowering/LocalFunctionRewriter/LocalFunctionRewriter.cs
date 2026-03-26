using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.FlowAnalysis;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class LocalFunctionRewriter : MethodToClassRewriter {
    private const int StaticClosureOrdinal = -1;
    private const int ThisOnlyClosureOrdinal = -2;

    private readonly Analysis _analysis;
    private readonly MethodSymbol _topLevelMethod;
    private readonly MethodSymbol _substitutedSourceMethod;
    private readonly Dictionary<BoundNode, ClosureEnvironment> _frames = [];
    private readonly Dictionary<NamedTypeSymbol, Symbol> _framePointers = [];
    private readonly Dictionary<ParameterSymbol, ParameterSymbol> _parameterMap = [];
    private readonly List<Analysis> _previousAnalyses;
    private readonly HashSet<DataContainerSymbol> _assignLocals;
    private readonly ImmutableHashSet<Symbol> _allCapturedVariables;

    private MethodSymbol _currentMethodInternal;
    private ParameterSymbol _currentFrameThis;
    private Symbol _innermostFramePointer;
    private TemplateMap _currentBodyTemplateMap;
    private ImmutableArray<TemplateParameterSymbol> _currentTemplateParameters;
    private ArrayBuilder<(MethodSymbol, BoundBlockStatement)> _synthesizedMethods;
    private ArrayBuilder<DataContainerSymbol> _addedLocals;
    private ArrayBuilder<BoundStatement> _addedStatements;
    private int _synthesizedFieldNameIdDispenser;
    private bool _seenBaseCall;
    private BoundExpression _thisProxyInitDeferred;

    private LocalFunctionRewriter(
        Analysis analysis,
        NamedTypeSymbol thisType,
        ParameterSymbol thisParameter,
        MethodSymbol method,
        MethodSymbol substitutedSourceMethod,
        TypeCompilationState compilationState,
        List<Analysis> previousAnalyses,
        BelteDiagnosticQueue diagnostics,
        HashSet<DataContainerSymbol> assignLocals)
        : base(compilationState, diagnostics) {
        _analysis = analysis;
        _assignLocals = assignLocals;
        _topLevelMethod = method;
        _currentMethodInternal = method;
        _currentTemplateParameters = method.templateParameters;
        _substitutedSourceMethod = substitutedSourceMethod;
        _innermostFramePointer = _currentFrameThis = thisParameter;
        _framePointers[thisType] = thisParameter;
        _previousAnalyses = previousAnalyses;
        _currentBodyTemplateMap = TemplateMap.Empty;

        var allCapturedVars = ImmutableHashSet.CreateBuilder<Symbol>();
        Analysis.VisitNestedFunctions(analysis.scopeTree, (scope, function) => {
            allCapturedVars.UnionWith(function.capturedVariables);
        });
        _allCapturedVariables = allCapturedVars.ToImmutable();
    }

    private protected override TemplateMap _templateMap => _currentBodyTemplateMap;

    private protected override MethodSymbol _currentMethod => _currentMethodInternal;

    private protected override NamedTypeSymbol _containingType => _topLevelMethod.containingType;

    private protected override bool NeedsProxy(Symbol localOrParameter) {
        return _allCapturedVariables.Contains(localOrParameter);
    }

    internal static BoundBlockStatement Rewrite(
        BoundBlockStatement loweredBody,
        NamedTypeSymbol thisType,
        MethodSymbol method,
        int methodOrdinal,
        MethodSymbol substitutedSourceMethod,
        TypeCompilationState state,
        List<Analysis> previousAnalyses,
        BelteDiagnosticQueue diagnostics,
        HashSet<DataContainerSymbol> assignLocals) {
        var analysis = Analysis.Analyze(loweredBody, method, methodOrdinal, state);
        var rewriter = new LocalFunctionRewriter(
            analysis,
            thisType,
            null, // TODO Check if we can actually synthesize this
            method,
            substitutedSourceMethod,
            state,
            previousAnalyses,
            diagnostics,
            assignLocals
        );

        rewriter.SynthesizeClosureEnvironments();
        rewriter.SynthesizeClosureMethods();

        var body = rewriter.AddStatementsIfNeeded((BoundBlockStatement)rewriter.Visit(loweredBody));

        if (rewriter._synthesizedMethods is not null) {
            if (state.synthesizedMethods is null) {
                state.synthesizedMethods = rewriter._synthesizedMethods;
            } else {
                state.synthesizedMethods.AddRange(rewriter._synthesizedMethods);
                rewriter._synthesizedMethods.Free();
            }
        }

        // analysis.Free();
        previousAnalyses.Add(analysis);
        return (BoundBlockStatement)body;
    }

    private void SynthesizeClosureEnvironments() {
        Analysis.VisitScopeTree(_analysis.scopeTree, scope => {
            if (scope.declaredEnvironment is { } env) {
                var frame = MakeFrame(scope, env);
                env.synthesizedEnvironment = frame;

                _compilationState.AddSynthesizedType(_containingType, frame);

                var typeLayouts = EvaluatorTypeLayoutVisitor.CreateTypeLayouts(frame);

                foreach (var typeLayout in typeLayouts)
                    _compilationState.typeLayouts.Add(typeLayout.Key, typeLayout.Value);

                if (frame.constructor is not null) {
                    AddSynthesizedMethod(
                        frame.constructor,
                        FlowAnalysisPass.AppendImplicitReturn(
                            MethodCompiler.BindSynthesizedMethodBody(
                                frame.constructor,
                                _compilationState,
                                _diagnostics
                            )
                        )
                    );
                }

                _frames.Add(scope.boundNode, env);
            }
        });

        SynthesizedClosureEnvironment MakeFrame(Scope scope, ClosureEnvironment env) {
            var scopeBoundNode = scope.boundNode;

            var syntax = scopeBoundNode.syntax;

            var methodOrdinal = _analysis.GetTopLevelMethodOrdinal();
            var closureOrdinal = _analysis.GetClosureOrdinal(env, syntax);

            var containingMethod = scope.containingFunction?.originalMethodSymbol ?? _topLevelMethod;

            if (_substitutedSourceMethod is not null && containingMethod == _topLevelMethod)
                containingMethod = _substitutedSourceMethod;

            var synthesizedEnvironment = new SynthesizedClosureEnvironment(
                _topLevelMethod,
                containingMethod,
                env.isStruct,
                syntax,
                methodOrdinal,
                closureOrdinal
            );

            foreach (var captured in env.capturedVariables) {
                var hoistedField = LambdaCapturedVariable.Create(
                    synthesizedEnvironment,
                    captured,
                    ref _synthesizedFieldNameIdDispenser
                );

                _proxies.Add(captured, new CapturedToFrameSymbolReplacement(hoistedField, isReusable: false));
                synthesizedEnvironment.AddHoistedField(hoistedField);
            }

            return synthesizedEnvironment;
        }
    }

    private void SynthesizeClosureMethods() {
        Analysis.VisitNestedFunctions(_analysis.scopeTree, (scope, nestedFunction) => {
            var originalMethod = nestedFunction.originalMethodSymbol;
            var syntax = originalMethod.syntaxReference;

            int closureOrdinal;
            ClosureKind closureKind;
            NamedTypeSymbol translatedLambdaContainer;
            SynthesizedClosureEnvironment containerAsFrame;
            int topLevelMethodOrdinal;
            int methodOrdinal;

            if (nestedFunction.containingEnvironment is not null) {
                containerAsFrame = nestedFunction.containingEnvironment.synthesizedEnvironment;
                translatedLambdaContainer = containerAsFrame;
                closureKind = ClosureKind.General;
                closureOrdinal = containerAsFrame.closureOrdinal;
            } else if (nestedFunction.capturesThis) {
                containerAsFrame = null;
                closureKind = ClosureKind.ThisOnly;
                translatedLambdaContainer = _topLevelMethod.containingType;
                closureOrdinal = ThisOnlyClosureOrdinal;
            } else {
                containerAsFrame = null;
                translatedLambdaContainer = _topLevelMethod.containingType;
                closureKind = ClosureKind.Static;
                closureOrdinal = StaticClosureOrdinal;
            }

            var structEnvironments = GetStructEnvironments(nestedFunction);

            topLevelMethodOrdinal = _analysis.GetTopLevelMethodOrdinal();
            methodOrdinal = GetLambdaId(
                syntax.node,
                closureKind,
                closureOrdinal,
                structEnvironments.SelectAsArray(e => e.closureId)
            );

            var synthesizedMethod = new SynthesizedClosureMethod(
                translatedLambdaContainer,
                structEnvironments,
                closureKind,
                _topLevelMethod,
                topLevelMethodOrdinal,
                originalMethod,
                nestedFunction.blockSyntax,
                _topLevelMethod.location,
                methodOrdinal,
                _compilationState
            );

            nestedFunction.synthesizedLoweredMethod = synthesizedMethod;
        });

        static ImmutableArray<SynthesizedClosureEnvironment> GetStructEnvironments(NestedFunction function) {
            var environments = ArrayBuilder<SynthesizedClosureEnvironment>.GetInstance();

            foreach (var env in function.capturedEnvironments) {
                if (env.isStruct)
                    environments.Add(env.synthesizedEnvironment);
            }

            return environments.ToImmutableAndFree();
        }
    }

    private protected override BoundExpression FramePointer(SyntaxNode syntax, NamedTypeSymbol frameClass) {
        if (_currentFrameThis is not null &&
            TypeSymbol.Equals(_currentFrameThis.type, frameClass, TypeCompareKind.ConsiderEverything)) {
            return new BoundThisExpression(syntax, frameClass);
        }

        var lambda = _currentMethod as SynthesizedClosureMethod;
        if (lambda is not null) {
            var start = lambda.parameterCount - lambda.extraSynthesizedParameterCount;

            for (var i = start; i < lambda.parameterCount; i++) {
                var potentialParameter = lambda.parameters[i];

                if (TypeSymbol.Equals(
                    potentialParameter.type.originalDefinition,
                    frameClass,
                    TypeCompareKind.ConsiderEverything)) {
                    return new BoundParameterExpression(syntax, potentialParameter, null, potentialParameter.type);
                }
            }
        }

        var framePointer = _framePointers[frameClass];

        if (_proxies.TryGetValue(framePointer, out var proxyField)) {
            return proxyField.Replacement(
                syntax,
                static (frameType, arg) => arg.self.FramePointer(arg.syntax, frameType),
                (syntax, self: this)
            );
        }

        var localFrame = (DataContainerSymbol)framePointer;
        return new BoundDataContainerExpression(syntax, localFrame, null, localFrame.type);
    }

    private protected override BoundNode VisitUnhoistedParameter(BoundParameterExpression node) {
        if (_parameterMap.TryGetValue(node.parameter, out var replacementParameter)) {
            return new BoundParameterExpression(
                node.syntax,
                replacementParameter,
                node.constantValue,
                replacementParameter.type,
                node.hasErrors
            );
        }

        return base.VisitUnhoistedParameter(node);
    }

    internal override BoundNode VisitThisExpression(BoundThisExpression node) {
        return _currentMethod == _topLevelMethod || _topLevelMethod.thisParameter is null
            ? node
            : FramePointer(node.syntax, (NamedTypeSymbol)node.Type());
    }

    internal override BoundNode VisitFunctionPointerLoad(BoundFunctionPointerLoad node) {
        if (node.targetMethod.methodKind == MethodKind.LocalFunction) {
            ImmutableArray<BoundExpression> arguments = default;
            ImmutableArray<RefKind> argRefKinds = default;

            RemapLocalFunction(
                node.syntax,
                node.targetMethod,
                out var receiver,
                out var remappedMethod,
                ref arguments,
                ref argRefKinds
            );

            return node.Update(remappedMethod, constrainedToTypeOpt: node.constrainedToTypeOpt, node.type);
        }

        return base.VisitFunctionPointerLoad(node);
    }

    internal override BoundNode VisitBaseExpression(BoundBaseExpression node) {
        return (!_currentMethod.isStatic &&
            TypeSymbol.Equals(
                _currentMethod.containingType,
                _topLevelMethod.containingType,
                TypeCompareKind.ConsiderEverything))
            ? node
            : FramePointer(node.syntax, _topLevelMethod.containingType);
    }

    private BoundExpression FrameOfType(SyntaxNode syntax, NamedTypeSymbol frameType) {
        var result = FramePointer(syntax, frameType.originalDefinition);
        return result;
    }

    private void RemapLocalFunction(
        SyntaxNode syntax,
        MethodSymbol localFunc,
        out BoundExpression receiver,
        out MethodSymbol method,
        ref ImmutableArray<BoundExpression> arguments,
        ref ImmutableArray<RefKind> argRefKinds) {
        var function = Analysis.GetNestedFunctionInTree(
            _analysis.scopeTree,
            localFunc.originalDefinition,
            _previousAnalyses
        );

        var loweredSymbol = function.synthesizedLoweredMethod;

        var frameCount = loweredSymbol.extraSynthesizedParameterCount;
        if (frameCount != 0) {
            var argumentsBuilder = ArrayBuilder<BoundExpression>.GetInstance(loweredSymbol.parameterCount);
            argumentsBuilder.AddRange(arguments);

            var start = loweredSymbol.parameterCount - frameCount;
            for (var i = start; i < loweredSymbol.parameterCount; i++) {
                var frameType = (NamedTypeSymbol)loweredSymbol.parameters[i].type.originalDefinition;

                if (frameType.arity > 0) {
                    var typeParameters = ((SynthesizedClosureEnvironment)frameType).constructedFromTemplateParameters;
                    var subst = _templateMap.SubstituteTemplateParameters(typeParameters);
                    frameType = frameType.Construct(subst.Select(s => new TypeOrConstant(s)).ToImmutableArray());
                }

                var frame = FrameOfType(syntax, frameType);
                argumentsBuilder.Add(frame);
            }

            var refKindsBuilder = ArrayBuilder<RefKind>.GetInstance(argumentsBuilder.Count);

            if (!argRefKinds.IsDefault)
                refKindsBuilder.AddRange(argRefKinds);
            else
                refKindsBuilder.AddMany(RefKind.None, arguments.Length);

            refKindsBuilder.AddMany(RefKind.Ref, frameCount);

            arguments = argumentsBuilder.ToImmutableAndFree();
            argRefKinds = refKindsBuilder.ToImmutableAndFree();
        }

        method = loweredSymbol;

        RemapLambdaOrLocalFunction(
            syntax,
            localFunc,
            SubstituteTemplateArguments(localFunc.templateArguments),
            loweredSymbol.closureKind,
            ref method,
            out receiver,
            out var constructedFrame
        );
    }

    private void RemapLambdaOrLocalFunction(
        SyntaxNode syntax,
        MethodSymbol originalMethod,
        ImmutableArray<TypeOrConstant> templateArguments,
        ClosureKind closureKind,
        ref MethodSymbol synthesizedMethod,
        out BoundExpression receiver,
        out NamedTypeSymbol constructedFrame) {
        var translatedLambdaContainer = synthesizedMethod.containingType;
        var containerAsFrame = translatedLambdaContainer as SynthesizedClosureEnvironment;

        var totalTemplateArgumentCount = (containerAsFrame?.arity ?? 0) + synthesizedMethod.arity;

        var realTemplateArguments = ImmutableArray.Create(
            _currentTemplateParameters.SelectAsArray(t => new TypeOrConstant(t)),
            0,
            totalTemplateArgumentCount - originalMethod.arity
        );

        if (!templateArguments.IsDefault) {
            realTemplateArguments = realTemplateArguments.Concat(templateArguments);
        }

        if (containerAsFrame is not null && containerAsFrame.arity != 0) {
            var containerTemplateArguments = ImmutableArray.Create(realTemplateArguments, 0, containerAsFrame.arity);

            realTemplateArguments = ImmutableArray.Create(
                realTemplateArguments,
                containerAsFrame.arity,
                realTemplateArguments.Length - containerAsFrame.arity
            );

            constructedFrame = containerAsFrame.Construct(containerTemplateArguments);
        } else {
            constructedFrame = translatedLambdaContainer;
        }

        synthesizedMethod = synthesizedMethod.AsMember(constructedFrame);

        if (synthesizedMethod.isTemplateMethod)
            synthesizedMethod = synthesizedMethod.Construct(realTemplateArguments);

        if (closureKind == ClosureKind.Singleton) {
            var field = containerAsFrame.singletonCache.AsMember(constructedFrame);
            receiver = new BoundFieldAccessExpression(syntax, null, field, null, field.type);
        } else if (closureKind == ClosureKind.Static) {
            receiver = new BoundTypeExpression(syntax, null, null, synthesizedMethod.containingType);
        } else {
            receiver = FrameOfType(syntax, constructedFrame);
        }
    }

    private ImmutableArray<TypeOrConstant> SubstituteTemplateArguments(
        ImmutableArray<TypeOrConstant> templateArguments) {
        if (templateArguments.IsEmpty)
            return templateArguments;

        var builder = ArrayBuilder<TypeOrConstant>.GetInstance(templateArguments.Length);
        foreach (var templateArgument in templateArguments) {
            TypeOrConstant oldTypeArg;
            var newTypeArg = templateArgument;

            if (newTypeArg.isType) {
                do {
                    oldTypeArg = newTypeArg;
                    newTypeArg = _templateMap.SubstituteType(oldTypeArg.type);
                } while (!TypeSymbol.Equals(
                    oldTypeArg.type.type,
                    newTypeArg.type.type,
                    TypeCompareKind.ConsiderEverything
                ));
            }

            builder.Add(newTypeArg);
        }

        return builder.ToImmutableAndFree();
    }

    internal override BoundNode VisitCastExpression(BoundCastExpression conversion) {
        // if (conversion.ConversionKind == ConversionKind.AnonymousFunction) {
        //     var result = (BoundExpression)RewriteLambdaConversion((BoundLambda)conversion.Operand);

        //     if (_inExpressionLambda && conversion.ExplicitCastInCode) {
        //         result = new BoundConversion(
        //             syntax: conversion.Syntax,
        //             operand: result,
        //             conversion: conversion.Conversion,
        //             isBaseConversion: false,
        //             @checked: false,
        //             explicitCastInCode: true,
        //             conversionGroupOpt: conversion.ConversionGroupOpt,
        //             constantValueOpt: conversion.ConstantValueOpt,
        //             type: conversion.Type);
        //     }

        //     return result;
        // }

        return base.VisitCastExpression(conversion);
    }

    internal override BoundNode VisitCallExpression(BoundCallExpression node) {
        if (node.method.methodKind == MethodKind.LocalFunction) {
            var args = VisitList(node.arguments);
            var argRefKinds = node.argumentRefKinds;
            var type = VisitType(node.Type());

            RemapLocalFunction(
                node.syntax,
                node.method,
                out var receiver,
                out var method,
                ref args,
                ref argRefKinds
            );

            return node.Update(receiver, method, args, argRefKinds, node.defaultArguments, node.resultKind, type);
        }

        var visited = base.VisitCallExpression(node);

        if (visited.kind != BoundKind.CallExpression)
            return visited;

        var rewritten = (BoundCallExpression)visited;

        if (!_seenBaseCall) {
            if (_currentMethod == _topLevelMethod && node.IsConstructorInitializer()) {
                _seenBaseCall = true;

                if (_thisProxyInitDeferred is not null) {
                    // return new BoundSequence(
                    //     syntax: node.syntax,
                    //     locals: ImmutableArray<LocalSymbol>.Empty,
                    //     sideEffects: ImmutableArray.Create<BoundExpression>(rewritten),
                    //     value: _thisProxyInitDeferred,
                    //     type: rewritten.Type);
                    throw new NotImplementedException();
                }
            }
        }

        return rewritten;
    }

    internal override BoundNode VisitBlockStatement(BoundBlockStatement node) {
        if (_frames.TryGetValue(node, out var frame)) {
            return IntroduceFrame(node, frame,
                (ArrayBuilder<BoundStatement> prologue, ArrayBuilder<DataContainerSymbol> newLocals) =>
                RewriteBlock(node, prologue, newLocals));
        } else {
            return RewriteBlock(
                node, ArrayBuilder<BoundStatement>.GetInstance(),
                ArrayBuilder<DataContainerSymbol>.GetInstance()
            );
        }
    }

    private BoundNode IntroduceFrame(
        BoundNode node,
        ClosureEnvironment env,
        Func<ArrayBuilder<BoundStatement>, ArrayBuilder<DataContainerSymbol>, BoundNode> func) {
        var frame = env.synthesizedEnvironment;
        var frameTemplateParameters = ImmutableArray.Create(
            _currentTemplateParameters.SelectAsArray(t => new TypeOrConstant(t)),
            0,
            frame.arity
        );

        var frameType = frame.ConstructIfGeneric(frameTemplateParameters);

        var framePointer = new SynthesizedDataContainerSymbol(
            _topLevelMethod,
            new TypeWithAnnotations(frameType),
            SynthesizedLocalKind.LambdaDisplayClass,
            frame.scopeSyntax
        );

        var syntax = node.syntax;

        var prologue = ArrayBuilder<BoundStatement>.GetInstance();

        if (frame.constructor is not null) {
            var constructor = frame.constructor.AsMember(frameType);

            prologue.Add(new BoundLocalDeclarationStatement(syntax,
                new BoundDataContainerDeclaration(syntax,
                    framePointer,
                    new BoundObjectCreationExpression(
                        syntax,
                        constructor,
                        [],
                        [],
                        [],
                        default,
                        false,
                        constructor.containingType
                    ),
                    false
                ))
            );
        }

        CapturedSymbolReplacement oldInnermostFrameProxy = null;
        if (_innermostFramePointer is not null) {
            _proxies.TryGetValue(_innermostFramePointer, out oldInnermostFrameProxy);

            if (env.capturesParent) {
                var capturedFrame = LambdaCapturedVariable.Create(frame, _innermostFramePointer, ref _synthesizedFieldNameIdDispenser);
                var frameParent = capturedFrame.AsMember(frameType);

                var left = new BoundFieldAccessExpression(
                    syntax,
                    new BoundDataContainerExpression(syntax, framePointer, null, frameType),
                    frameParent,
                    null,
                    frameParent.type
                );

                var right = FrameOfType(syntax, frameParent.type as NamedTypeSymbol);

                var assignment = new BoundExpressionStatement(
                    syntax,
                    new BoundAssignmentOperator(syntax, left, right, false, left.Type())
                );

                prologue.Add(assignment);

                // if (_compilationState.Emitting) {
                //     frame.AddHoistedField(capturedFrame);
                //     // _compilationState.AddSynthesizedMethod(frame, capturedFrame.GetCciAdapter());
                // }
                frame.AddHoistedField(capturedFrame);

                _proxies[_innermostFramePointer] = new CapturedToFrameSymbolReplacement(capturedFrame, false);
            }
        }

        foreach (var variable in env.capturedVariables)
            InitVariableProxy(syntax, variable, framePointer, prologue);

        var oldInnermostFramePointer = _innermostFramePointer;

        if (!framePointer.type.isPrimitiveType)
            _innermostFramePointer = framePointer;

        var addedLocals = ArrayBuilder<DataContainerSymbol>.GetInstance();
        addedLocals.Add(framePointer);
        _framePointers.Add(frame, framePointer);

        var result = func(prologue, addedLocals);

        _innermostFramePointer = oldInnermostFramePointer;

        if (_innermostFramePointer is not null) {
            if (oldInnermostFrameProxy is not null)
                _proxies[_innermostFramePointer] = oldInnermostFrameProxy;
            else
                _proxies.Remove(_innermostFramePointer);
        }

        return result;
    }

    private void InitVariableProxy(
        SyntaxNode syntax,
        Symbol symbol,
        DataContainerSymbol framePointer,
        ArrayBuilder<BoundStatement> prologue) {
        if (_proxies.TryGetValue(symbol, out var proxy)) {
            BoundExpression value;

            switch (symbol.kind) {
                case SymbolKind.Parameter:
                    var parameter = (ParameterSymbol)symbol;
                    ParameterSymbol parameterToUse;

                    if (!_parameterMap.TryGetValue(parameter, out parameterToUse))
                        parameterToUse = parameter;

                    value = new BoundParameterExpression(syntax, parameterToUse, null, parameterToUse.type);
                    break;
                case SymbolKind.Local:
                    var local = (DataContainerSymbol)symbol;

                    if (_assignLocals is null || !_assignLocals.Contains(local))
                        return;

                    DataContainerSymbol localToUse;

                    if (!_localMap.TryGetValue(local, out localToUse))
                        localToUse = local;

                    value = new BoundDataContainerExpression(syntax, localToUse, null, localToUse.type);
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(symbol.kind);
            }

            var left = proxy.Replacement(
                syntax,
                static (frameType1, arg)
                    => new BoundDataContainerExpression(arg.syntax, arg.framePointer, null, arg.framePointer.type),
                (syntax, framePointer)
            );

            var assignToProxy = new BoundAssignmentOperator(syntax, left, value, false, value.Type());

            if (_currentMethod.methodKind == MethodKind.Constructor &&
                symbol == _currentMethod.thisParameter &&
                !_seenBaseCall) {
                _thisProxyInitDeferred = assignToProxy;
            } else {
                prologue.Add(new BoundExpressionStatement(syntax, assignToProxy));
            }
        }
    }

    private BoundBlockStatement RewriteBlock(
        BoundBlockStatement node,
        ArrayBuilder<BoundStatement> prologue,
        ArrayBuilder<DataContainerSymbol> newLocals) {
        RewriteLocals(node.locals, newLocals);

        var newStatements = ArrayBuilder<BoundStatement>.GetInstance();

        InsertAndFreePrologue(newStatements, prologue);

        foreach (var statement in node.statements) {
            var replacement = (BoundStatement)Visit(statement);

            if (replacement is not null)
                newStatements.Add(replacement);
        }

        return node.Update(newStatements.ToImmutableAndFree(), newLocals.ToImmutableAndFree(), node.localFunctions);
    }

    private static void InsertAndFreePrologue<T>(
        ArrayBuilder<BoundStatement> result,
        ArrayBuilder<T> prologue) where T : BoundNode {
        foreach (var node in prologue) {
            if (node is BoundStatement stmt)
                result.Add(stmt);
            else
                result.Add(new BoundExpressionStatement(node.syntax, (BoundExpression)(BoundNode)node));
        }

        prologue.Free();
    }

    internal override BoundNode VisitLocalFunctionStatement(BoundLocalFunctionStatement node) {
        RewriteLambdaOrLocalFunction(node, out _, out _, out _, out _, out _, out _);
        return new BoundNopStatement(node.syntax);
    }

    private SynthesizedClosureMethod RewriteLambdaOrLocalFunction(
        BoundLocalFunctionStatement node,
        out ClosureKind closureKind,
        out NamedTypeSymbol translatedLambdaContainer,
        out SynthesizedClosureEnvironment containerAsFrame,
        out BoundNode lambdaScope,
        out int topLevelMethodOrdinal,
        out int methodOrdinal) {
        var function = Analysis.GetNestedFunctionInTree(_analysis.scopeTree, node.symbol, _previousAnalyses);
        var synthesizedMethod = function.synthesizedLoweredMethod;

        closureKind = synthesizedMethod.closureKind;
        translatedLambdaContainer = synthesizedMethod.containingType;
        containerAsFrame = translatedLambdaContainer as SynthesizedClosureEnvironment;
        topLevelMethodOrdinal = _analysis.GetTopLevelMethodOrdinal();
        methodOrdinal = synthesizedMethod.methodOrdinal;

        if (function.containingEnvironment is not null) {
            BoundNode tmpScope = null;
            Analysis.VisitScopeTree(_analysis.scopeTree, scope => {
                if (scope.declaredEnvironment == function.containingEnvironment)
                    tmpScope = scope.boundNode;
            });
            lambdaScope = tmpScope;
        } else {
            lambdaScope = null;
        }

        foreach (var parameter in node.symbol.parameters)
            _parameterMap.Add(parameter, synthesizedMethod.parameters[parameter.ordinal]);

        var oldMethod = _currentMethodInternal;
        var oldFrameThis = _currentFrameThis;
        var oldTemplateParameters = _currentTemplateParameters;
        var oldInnermostFramePointer = _innermostFramePointer;
        var oldTemplateMap = _currentBodyTemplateMap;
        var oldAddedStatements = _addedStatements;
        var oldAddedLocals = _addedLocals;
        _addedStatements = null;
        _addedLocals = null;

        _currentMethodInternal = synthesizedMethod;
        if (closureKind == ClosureKind.Static || closureKind == ClosureKind.Singleton) {
            _innermostFramePointer = _currentFrameThis = null;
        } else {
            _currentFrameThis = synthesizedMethod.thisParameter;
            _framePointers.TryGetValue(translatedLambdaContainer, out _innermostFramePointer);
        }

        _currentTemplateParameters = containerAsFrame?.templateParameters.Concat(synthesizedMethod.templateParameters)
            ?? synthesizedMethod.templateParameters;
        _currentBodyTemplateMap = synthesizedMethod.templateMap;

        if (node.body is BoundBlockStatement block) {
            var body = AddStatementsIfNeeded((BoundBlockStatement)VisitBlockStatement(block));
            body = Lowerer.Flatten(synthesizedMethod, body);

            if (!ControlFlowGraph.AllPathsReturn(body))
                _diagnostics.Push(Error.NotAllPathsReturn(node.symbol.location));

            if (_compilationState.compilation.options.buildMode.Evaluating()) {
                body = EvaluatorSlotRewriter.Rewrite(
                    synthesizedMethod,
                    body,
                    _compilationState.typeLayouts,
                    _compilationState.compilation.previous?.boundProgram,
                    out var slotManager
                );

                _compilationState.AddMethodLayout(synthesizedMethod, slotManager);
            }

            AddSynthesizedMethod(synthesizedMethod, (BoundBlockStatement)body);
        }

        _currentMethodInternal = oldMethod;
        _currentFrameThis = oldFrameThis;
        _currentTemplateParameters = oldTemplateParameters;
        _innermostFramePointer = oldInnermostFramePointer;
        _currentBodyTemplateMap = oldTemplateMap;
        _addedLocals = oldAddedLocals;
        _addedStatements = oldAddedStatements;

        return synthesizedMethod;
    }

    private BoundBlockStatement AddStatementsIfNeeded(BoundBlockStatement body) {
        if (_addedLocals is not null) {
            _addedStatements.Add(body);

            body = new BoundBlockStatement(
                body.syntax,
                _addedStatements.ToImmutableAndFree(),
                _addedLocals.ToImmutableAndFree(),
                []
            );

            _addedLocals = null;
            _addedStatements = null;
        }

        return body;
    }

    private void AddSynthesizedMethod(MethodSymbol method, BoundBlockStatement body) {
        _synthesizedMethods ??= ArrayBuilder<(MethodSymbol, BoundBlockStatement)>.GetInstance();
        _synthesizedMethods.Add((method, body));
    }

    private int GetLambdaId(
        SyntaxNode syntax,
        ClosureKind closureKind,
        int closureOrdinal,
        ImmutableArray<int> structClosureIds) {
        // SyntaxNode? lambdaOrLambdaBodySyntax;
        // bool isLambdaBody;

        // if (syntax is LocalFunctionStatementSyntax localFunction) {
        //     lambdaOrLambdaBodySyntax = localFunction.body;

        //     if (lambdaOrLambdaBodySyntax is null) {
        //         lambdaOrLambdaBodySyntax = localFunction;
        //         isLambdaBody = false;
        //     } else {
        //         isLambdaBody = true;
        //     }
        // }

        // int lambdaId;
        // int previousLambdaId = default;

        // if (closureRudeEdit == null &&
        //     slotAllocator?.TryGetPreviousLambda(lambdaOrLambdaBodySyntax, isLambdaBody, closureOrdinal, structClosureIds, out previousLambdaId, out lambdaRudeEdit) == true &&
        //     lambdaRudeEdit == null) {
        //     lambdaId = previousLambdaId;
        // } else {
        //     lambdaId = new DebugId(_lambdaDebugInfoBuilder.Count, CompilationState.ModuleBuilderOpt.CurrentGenerationOrdinal);

        //     var rudeEdit = closureRudeEdit ?? lambdaRudeEdit;
        //     if (rudeEdit != null) {
        //         _lambdaRuntimeRudeEditsBuilder.Add(new LambdaRuntimeRudeEditInfo(previousLambdaId, rudeEdit.Value));
        //     }
        // }

        // int syntaxOffset = _topLevelMethod.CalculateLocalSyntaxOffset(LambdaUtilities.GetDeclaratorPosition(lambdaOrLambdaBodySyntax), lambdaOrLambdaBodySyntax.SyntaxTree);
        // _lambdaDebugInfoBuilder.Add(new EncLambdaInfo(new LambdaDebugInfo(syntaxOffset, lambdaId, closureOrdinal), structClosureIds));
        // return lambdaId;
        return 1;
    }
}

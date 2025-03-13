using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class LocalFunctionRewriter : BoundTreeRewriter {
    private readonly Analysis _analysis;
    private readonly MethodSymbol _topLevelMethod;
    private readonly TypeCompilationState _compilationState;
    private readonly MethodSymbol _substitutedSourceMethod;
    private readonly Dictionary<BoundNode, ClosureEnvironment> _frames = [];
    private readonly Dictionary<NamedTypeSymbol, Symbol> _framePointers = [];
    private readonly Dictionary<ParameterSymbol, ParameterSymbol> _parameterMap = [];

    private MethodSymbol _currentMethod;
    private ParameterSymbol _currentFrameThis;
    private Symbol _innermostFramePointer;
    private TemplateMap _currentBodyTemplateMap;
    private ImmutableArray<TemplateParameterSymbol> _currentTemplateParameters;
    private ArrayBuilder<(MethodSymbol, BoundBlockStatement)> _synthesizedMethods;
    private ArrayBuilder<DataContainerSymbol> _addedLocals;
    private ArrayBuilder<BoundStatement> _addedStatements;

    private LocalFunctionRewriter(
        Analysis analysis,
        NamedTypeSymbol thisType,
        ParameterSymbol thisParameter,
        MethodSymbol method,
        MethodSymbol substitutedSourceMethod,
        TypeCompilationState compilationState) {
        _analysis = analysis;
        _topLevelMethod = method;
        _currentMethod = method;
        _compilationState = compilationState;
        _currentTemplateParameters = method.templateParameters;
        _substitutedSourceMethod = substitutedSourceMethod;
        _innermostFramePointer = _currentFrameThis = thisParameter;
        _framePointers[thisType] = thisParameter;
    }

    internal static BoundBlockStatement Rewrite(
        BoundBlockStatement loweredBody,
        NamedTypeSymbol thisType,
        MethodSymbol method,
        int methodOrdinal,
        MethodSymbol substitutedSourceMethod,
        TypeCompilationState state,
        BelteDiagnosticQueue diagnostics) {
        var analysis = Analysis.Analyze(loweredBody, method, methodOrdinal, state);
        var rewriter = new LocalFunctionRewriter(
            analysis,
            thisType,
            null, // TODO Check if we can actually synthesize this
            method,
            substitutedSourceMethod,
            state
        );

        rewriter.SynthesizeClosureEnvironments();
        rewriter.SynthesizeClosureMethods();

        var body = (BoundBlockStatement)rewriter.Visit(loweredBody);

        if (rewriter._synthesizedMethods is not null) {
            if (state.synthesizedMethods is null) {
                state.synthesizedMethods = rewriter._synthesizedMethods;
            } else {
                state.synthesizedMethods.AddRange(rewriter._synthesizedMethods);
                rewriter._synthesizedMethods.Free();
            }
        }

        analysis.Free();
        return body;
    }

    private void SynthesizeClosureEnvironments() {
        Analysis.VisitScopeTree(_analysis.scopeTree, scope => {
            if (scope.declaredEnvironment is { } env) {
                var frame = MakeFrame(scope, env);
                env.synthesizedEnvironment = frame;

                // TODO Do we do this anyway even though its unwrapped later
                // _compilationState.ModuleBuilderOpt.AddSynthesizedDefinition(ContainingType, frame.GetCciAdapter());
                // if (frame.constructor is not null) {
                //     AddSynthesizedMethod(
                //         frame.Constructor,
                //         FlowAnalysisPass.AppendImplicitReturn(
                //             MethodCompiler.BindSynthesizedMethodBody(frame.Constructor, CompilationState, Diagnostics),
                //             frame.Constructor));
                // }

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
                // TODO Do we need to do this?
                // var hoistedField = LambdaCapturedVariable.Create(synthesizedEnvironment, captured, ref _synthesizedFieldNameIdDispenser);
                // proxies.Add(captured, new CapturedToFrameSymbolReplacement(hoistedField, isReusable: false));
                // synthesizedEnvironment.AddHoistedField(hoistedField);
                // CompilationState.ModuleBuilderOpt.AddSynthesizedDefinition(synthesizedEnvironment, hoistedField.GetCciAdapter());
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
                // TODO What should the ordinal be
                // closureOrdinal = LambdaDebugInfo.ThisOnlyClosureOrdinal;
            } else {
                containerAsFrame = null;
                translatedLambdaContainer = _topLevelMethod.containingType;
                closureKind = ClosureKind.Static;
                // TODO What should the ordinal be
                // closureOrdinal = LambdaDebugInfo.StaticClosureOrdinal;
            }

            var structEnvironments = GetStructEnvironments(nestedFunction);

            // Move the body of the lambda to a freshly generated synthetic method on its frame.
            topLevelMethodOrdinal = _analysis.GetTopLevelMethodOrdinal();
            // TODO What should the ordinal be
            methodOrdinal = 0;
            // methodOrdinal = GetMethodOrdinal(syntax, closureKind, closureOrdinal, structEnvironments.SelectAsArray(e => e.ClosureId), containerAsFrame?.RudeEdit);

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

    private protected BoundExpression FramePointer(SyntaxNode syntax, NamedTypeSymbol frameClass) {
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
        // TODO Pretty sure we won't ever actually use the FramePointer
        // CapturedSymbolReplacement proxyField;
        // if (proxies.TryGetValue(framePointer, out proxyField)) {
        //     // However, frame pointer local variables themselves can be "captured".  In that case
        //     // the inner frames contain pointers to the enclosing frames.  That is, nested
        //     // frame pointers are organized in a linked list.
        //     return proxyField.Replacement(
        //         syntax,
        //         static (frameType, arg) => arg.self.FramePointer(arg.syntax, frameType),
        //         (syntax, self: this));
        // }

        var localFrame = (DataContainerSymbol)framePointer;
        return new BoundDataContainerExpression(syntax, localFrame, null, localFrame.type);
    }

    internal override BoundNode VisitThisExpression(BoundThisExpression node) {
        return _currentMethod == _topLevelMethod || _topLevelMethod.thisParameter is null
            ? node
            : FramePointer(node.syntax, (NamedTypeSymbol)node.type);
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
        var function = Analysis.GetNestedFunctionInTree(_analysis.scopeTree, localFunc.originalDefinition);
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
                    var subst = _currentBodyTemplateMap.SubstituteTemplateParameters(typeParameters);
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
            receiver = new BoundTypeExpression(syntax, null, synthesizedMethod.containingType);
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

            do {
                oldTypeArg = newTypeArg;
                newTypeArg = _currentBodyTemplateMap.SubstituteType(oldTypeArg.type);
            } while (!TypeSymbol.Equals(
                oldTypeArg.type.type,
                newTypeArg.type.type,
                TypeCompareKind.ConsiderEverything
            ));

            builder.Add(newTypeArg);
        }

        return builder.ToImmutableAndFree();
    }

    internal override BoundNode VisitCallExpression(BoundCallExpression node) {
        if (node.method.methodKind == MethodKind.LocalFunction) {
            var args = VisitList(node.arguments);
            var argRefKinds = node.argumentRefKinds;
            var type = VisitType(node.type);

            RemapLocalFunction(
                node.syntax,
                node.method,
                out var receiver,
                out var method,
                ref args,
                ref argRefKinds
            );

            // return node.Update(
            //     receiver,
            //     node.initialBindingReceiverIsSubjectToCloning,
            //     method,
            //     args,
            //     node.argumentNames,
            //     argRefKinds,
            //     node.IsDelegateCall,
            //     node.Expanded,
            //     node.InvokedAsExtensionMethod,
            //     node.ArgsToParamsOpt,
            //     node.DefaultArguments,
            //     node.ResultKind,
            //     type);
            return node.Update(receiver, method, args, argRefKinds, node.defaultArguments, node.resultKind, type);
        }

        var visited = base.VisitCallExpression(node);

        if (visited.kind != BoundKind.CallExpression)
            return visited;

        var rewritten = (BoundCallExpression)visited;

        // TODO do we need this?
        // Check if we need to init the 'this' proxy in a ctor call
        // if (!_seenBaseCall) {
        //     if (_currentMethod == _topLevelMethod && node.IsConstructorInitializer()) {
        //         _seenBaseCall = true;
        //         if (_thisProxyInitDeferred != null) {
        //             // Insert the this proxy assignment after the ctor call.
        //             // Create bound sequence: { ctor call, thisProxyInitDeferred }
        //             return new BoundSequence(
        //                 syntax: node.Syntax,
        //                 locals: ImmutableArray<LocalSymbol>.Empty,
        //                 sideEffects: ImmutableArray.Create<BoundExpression>(rewritten),
        //                 value: _thisProxyInitDeferred,
        //                 type: rewritten.Type);
        //         }
        //     }
        // }

        return rewritten;
    }

    internal override BoundNode VisitBlockStatement(BoundBlockStatement node) {
        if (_frames.TryGetValue(node, out var frame)) {
            return IntroduceFrame(node, frame,
                (ArrayBuilder<BoundExpression> prologue, ArrayBuilder<DataContainerSymbol> newLocals) =>
                RewriteBlock(node, prologue, newLocals));
        } else {
            return RewriteBlock(
                node, ArrayBuilder<BoundExpression>.GetInstance(),
                ArrayBuilder<DataContainerSymbol>.GetInstance()
            );
        }
    }

    private BoundNode IntroduceFrame(
        BoundNode node,
        ClosureEnvironment env,
        Func<ArrayBuilder<BoundExpression>, ArrayBuilder<DataContainerSymbol>, BoundNode> func) {
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
            frame.scopeSyntax
        );

        var syntax = node.syntax;

        var prologue = ArrayBuilder<BoundExpression>.GetInstance();

        // if (frame.constructor is not null) {
        //     var constructor = frame.constructor.AsMember(frameType);

        //     prologue.Add(new BoundAssignmentOperator(syntax,
        //         new BoundLocal(syntax, framePointer, null, frameType),
        //         new BoundObjectCreationExpression(syntax: syntax, constructor: constructor),
        //         frameType));
        // }

        // CapturedSymbolReplacement oldInnermostFrameProxy = null;
        // if ((object)_innermostFramePointer != null) {
        //     proxies.TryGetValue(_innermostFramePointer, out oldInnermostFrameProxy);
        //     if (env.CapturesParent) {
        //         var capturedFrame = LambdaCapturedVariable.Create(frame, _innermostFramePointer, ref _synthesizedFieldNameIdDispenser);
        //         FieldSymbol frameParent = capturedFrame.AsMember(frameType);
        //         BoundExpression left = new BoundFieldAccess(syntax, new BoundLocal(syntax, framePointer, null, frameType), frameParent, null);
        //         BoundExpression right = FrameOfType(syntax, frameParent.Type as NamedTypeSymbol);
        //         BoundExpression assignment = new BoundAssignmentOperator(syntax, left, right, left.Type);
        //         prologue.Add(assignment);

        //         if (CompilationState.Emitting) {
        //             Debug.Assert(capturedFrame.Type.IsReferenceType); // Make sure we're not accidentally capturing a struct by value
        //             frame.AddHoistedField(capturedFrame);
        //             CompilationState.ModuleBuilderOpt.AddSynthesizedDefinition(frame, capturedFrame.GetCciAdapter());
        //         }

        //         proxies[_innermostFramePointer] = new CapturedToFrameSymbolReplacement(capturedFrame, isReusable: false);
        //     }
        // }

        // Capture any parameters of this block.  This would typically occur
        // at the top level of a method or lambda with captured parameters.
        // foreach (var variable in env.CapturedVariables) {
        //     InitVariableProxy(syntax, variable, framePointer, prologue);
        // }

        var oldInnermostFramePointer = _innermostFramePointer;

        if (!framePointer.type.isPrimitiveType)
            _innermostFramePointer = framePointer;

        var addedLocals = ArrayBuilder<DataContainerSymbol>.GetInstance();
        addedLocals.Add(framePointer);
        _framePointers.Add(frame, framePointer);

        var result = func(prologue, addedLocals);

        _innermostFramePointer = oldInnermostFramePointer;

        // if ((object)_innermostFramePointer != null) {
        //     if (oldInnermostFrameProxy != null) {
        //         proxies[_innermostFramePointer] = oldInnermostFrameProxy;
        //     } else {
        //         proxies.Remove(_innermostFramePointer);
        //     }
        // }

        return result;
    }

    private BoundBlockStatement RewriteBlock(
        BoundBlockStatement node,
        ArrayBuilder<BoundExpression> prologue,
        ArrayBuilder<DataContainerSymbol> newLocals) {
        // RewriteLocals(node.locals, newLocals);

        var newStatements = ArrayBuilder<BoundStatement>.GetInstance();

        // InsertAndFreePrologue(newStatements, prologue);

        foreach (var statement in node.statements) {
            var replacement = (BoundStatement)Visit(statement);

            if (replacement is not null)
                newStatements.Add(replacement);
        }

        return node.Update(newStatements.ToImmutableAndFree(), newLocals.ToImmutableAndFree(), node.localFunctions);
    }

    // TODO Visit Try/Catch

    internal override BoundNode VisitLocalFunctionStatement(BoundLocalFunctionStatement node) {
        RewriteLambdaOrLocalFunction(
            node,
            out var closureKind,
            out var translatedLambdaContainer,
            out var containerAsFrame,
            out var lambdaScope,
            out var topLevelMethodOrdinal,
            out var methodOrdinal
        );

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
        var function = Analysis.GetNestedFunctionInTree(_analysis.scopeTree, node.symbol);
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

        // _compilationState.synthesizedMethods.AddSynthesizedDefinition(translatedLambdaContainer, synthesizedMethod.GetCciAdapter());

        foreach (var parameter in node.symbol.parameters) {
            _parameterMap.Add(parameter, synthesizedMethod.parameters[parameter.ordinal]);
        }

        // rewrite the lambda body as the generated method's body
        var oldMethod = _currentMethod;
        var oldFrameThis = _currentFrameThis;
        var oldTemplateParameters = _currentTemplateParameters;
        var oldInnermostFramePointer = _innermostFramePointer;
        var oldTemplateMap = _currentBodyTemplateMap;
        var oldAddedStatements = _addedStatements;
        var oldAddedLocals = _addedLocals;
        _addedStatements = null;
        _addedLocals = null;

        // switch to the generated method

        _currentMethod = synthesizedMethod;
        if (closureKind == ClosureKind.Static || closureKind == ClosureKind.Singleton) {
            // no link from a static lambda to its container
            _innermostFramePointer = _currentFrameThis = null;
        } else {
            _currentFrameThis = synthesizedMethod.thisParameter;
            _framePointers.TryGetValue(translatedLambdaContainer, out _innermostFramePointer);
        }

        _currentTemplateParameters = containerAsFrame?.templateParameters.Concat(synthesizedMethod.templateParameters)
            ?? synthesizedMethod.templateParameters;
        _currentBodyTemplateMap = synthesizedMethod.templateMap;

        if (node.body is BoundBlockStatement block) {
            // var body = AddStatementsIfNeeded((BoundStatement)VisitBlock(block));
            var body = (BoundBlockStatement)VisitBlockStatement(block);
            // CheckLocalsDefined(body);
            AddSynthesizedMethod(synthesizedMethod, body);
        }

        _currentMethod = oldMethod;
        _currentFrameThis = oldFrameThis;
        _currentTemplateParameters = oldTemplateParameters;
        _innermostFramePointer = oldInnermostFramePointer;
        _currentBodyTemplateMap = oldTemplateMap;
        _addedLocals = oldAddedLocals;
        _addedStatements = oldAddedStatements;

        return synthesizedMethod;
    }

    private void AddSynthesizedMethod(MethodSymbol method, BoundBlockStatement body) {
        _synthesizedMethods ??= ArrayBuilder<(MethodSymbol, BoundBlockStatement)>.GetInstance();
        _synthesizedMethods.Add((method, body));
    }

}

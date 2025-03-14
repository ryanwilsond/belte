using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Lowering;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal sealed class MethodCompiler {
    private readonly Compilation _compilation;
    private readonly bool _emitting;
    private readonly BelteDiagnosticQueue _diagnostics;
    private readonly MethodSymbol _entryPoint;
    private readonly Dictionary<MethodSymbol, BoundBlockStatement> _methodBodies;
    private readonly ArrayBuilder<NamedTypeSymbol> _types;

    private MethodCompiler(
        Compilation compilation,
        BelteDiagnosticQueue diagnostics,
        MethodSymbol entryPoint,
        bool emitting) {
        _compilation = compilation;
        _diagnostics = diagnostics;
        _entryPoint = entryPoint;
        _emitting = emitting;
        _types = ArrayBuilder<NamedTypeSymbol>.GetInstance();
        _methodBodies = [];
    }

    internal static BoundProgram CompileMethodBodies(
        Compilation compilation,
        BelteDiagnosticQueue diagnostics,
        bool emitting) {
        var globalNamespace = compilation.globalNamespaceInternal;
        var entryPoint = GetEntryPoint(compilation, globalNamespace, diagnostics);

        var methodCompiler = new MethodCompiler(compilation, diagnostics, entryPoint, emitting);

        methodCompiler.CompileNamespace(globalNamespace);
        return methodCompiler.CreateBoundProgram();
    }

    private static MethodSymbol GetEntryPoint(
        Compilation compilation,
        NamespaceSymbol globalNamespace,
        BelteDiagnosticQueue diagnostics) {
        var builder = ArrayBuilder<MethodSymbol>.GetInstance();
        var globalsCount = 0;
        var programClasses = globalNamespace.GetMembers(WellKnownMemberNames.TopLevelStatementsEntryPointTypeName);

        if (programClasses.Length > 0) {
            var programClass = (NamedTypeSymbol)programClasses[0];

            foreach (var member in programClass.GetMembers(WellKnownMemberNames.EntryPointMethodName)) {
                if (member is MethodSymbol m && HasEntryPointSignature(m)) {
                    if (m is SynthesizedEntryPoint)
                        globalsCount++;

                    builder.Add(m);
                }
            }
        }

        var entryPointCandidates = builder.ToImmutableAndFree();
        MethodSymbol entryPoint = null;

        if (entryPointCandidates.Length == 0 && !compilation.options.isScript) {
            diagnostics.Push(Error.NoSuitableEntryPoint());
        } else if (entryPointCandidates.Length == 1) {
            entryPoint = entryPointCandidates[0];
        } else {
            if (entryPointCandidates.Length > globalsCount + 1)
                diagnostics.Push(Error.MultipleMains(entryPointCandidates[0].location));

            if (globalsCount > 0 && entryPointCandidates.Length > globalsCount)
                diagnostics.Push(Error.MainAndGlobals(entryPointCandidates[0].location));

            if (globalsCount > 1) {
                for (var i = 0; i < entryPointCandidates.Length; i++) {
                    if (entryPointCandidates[i] is SynthesizedEntryPoint s)
                        diagnostics.Push(Error.GlobalStatementsInMultipleFiles(s.location));
                }
            }
        }

        return entryPoint;
    }

    private static bool HasEntryPointSignature(MethodSymbol method) {
        var returnType = method.returnType;

        if (returnType.specialType != SpecialType.Int && !returnType.IsVoidType()) {
            if (returnType.specialType == SpecialType.Nullable &&
                returnType.GetNullableUnderlyingType().specialType != SpecialType.Int) {
                return false;
            }
        }

        if (method.refKind != RefKind.None)
            return false;

        if (method.parameterCount == 0)
            return true;

        if (method.parameterCount > 1)
            return false;

        if (!method.parameterRefKinds.IsDefault)
            return false;

        var firstType = method.parameters[0].type;

        if (firstType.specialType != SpecialType.List)
            return false;

        var elementType = ((NamedTypeSymbol)firstType).templateArguments[0].type;

        if (elementType.specialType != SpecialType.String)
            return false;

        return true;
    }

    private BoundProgram CreateBoundProgram() {
        return new BoundProgram(
            _methodBodies.ToImmutableDictionary(),
            _types.ToImmutableAndFree(),
            _entryPoint,
            _compilation.previous?.boundProgram
        );
    }

    private void CompileNamespace(NamespaceSymbol globalNamespace) {
        foreach (var member in globalNamespace.GetMembersUnordered()) {
            switch (member) {
                case NamedTypeSymbol n:
                    CompileNamedType(n);
                    break;
                    // TODO Namespaces can only contain named types currently?
                    // case MethodSymbol m:
                    //     var _ = new Binder.ProcessedFieldInitializers();
                    //     CompileMethod(m, ref _, null);
                    //     break;
            }
        }
    }

    private void CompileNamedType(NamedTypeSymbol namedType) {
        _types.Add(namedType);

        var state = new TypeCompilationState(namedType, _compilation);
        var members = namedType.GetMembers();
        var processedInitializers = new Binder.ProcessedFieldInitializers();

        var sourceType = namedType as SourceMemberContainerTypeSymbol;

        if (sourceType is not null) {
            Binder.BindFieldInitializers(
                _compilation,
                sourceType.initializers,
                _diagnostics,
                ref processedInitializers
            );
        }

        for (var ordinal = 0; ordinal < members.Length; ordinal++) {
            var member = members[ordinal];

            switch (member) {
                case NamedTypeSymbol n:
                    CompileNamedType(n);
                    break;
                case MethodSymbol m:
                    var initializers = m.methodKind == MethodKind.Constructor ? processedInitializers : default;
                    CompileMethod(m, ordinal, ref initializers, state);
                    break;
                case FieldSymbol f:
                    if (f.isConstExpr)
                        f.GetConstantValue(ConstantFieldsInProgress.Empty);

                    break;
            }
        }

        if (state.synthesizedMethods is not null) {
            foreach (var synthesizedMethod in state.synthesizedMethods)
                _methodBodies.Add(synthesizedMethod.Item1, synthesizedMethod.Item2);
        }

        state.Free();
    }

    private void CompileMethod(
        MethodSymbol method,
        int methodOrdinal,
        ref Binder.ProcessedFieldInitializers processedInitializers,
        TypeCompilationState state) {
        if (method.isAbstract)
            return;

        var currentDiagnostics = BelteDiagnosticQueue.GetInstance();
        BoundBlockStatement analyzedInitializers = null;

        var includeInitializers = method.IncludeFieldInitializersInBody();
        var includeNonEmptyInitializers = includeInitializers &&
            !processedInitializers.boundInitializers.IsDefaultOrEmpty;

        if (includeNonEmptyInitializers && processedInitializers.loweredInitializers is null) {
            analyzedInitializers = InitializerRewriter.RewriteConstructor(
                processedInitializers.boundInitializers,
                method
            );
        }

        var body = BindMethodBody(
            method,
            state,
            currentDiagnostics,
            includeInitializers,
            analyzedInitializers
        );

        if (!_emitting || currentDiagnostics.AnyErrors()) {
            _diagnostics.PushRangeAndFree(currentDiagnostics);
            _methodBodies.Add(method, body);
            return;
        }

        var loweredBody = LowerBody(
            method,
            methodOrdinal,
            body,
            state,
            _compilation.previousAnalyses,
            _compilation.options.buildMode,
            currentDiagnostics
        );

        _diagnostics.PushRangeAndFree(currentDiagnostics);
        _methodBodies.Add(method, loweredBody);
    }

    private static BoundBlockStatement LowerBody(
        MethodSymbol method,
        int methodOrdinal,
        BoundBlockStatement body,
        TypeCompilationState state,
        List<LocalFunctionRewriter.Analysis> previousAnalyses,
        BuildMode buildMode,
        BelteDiagnosticQueue currentDiagnostics) {
        var loweredBody = Lowerer.Lower(method, body, currentDiagnostics);

        // ? C# handles closure a little different than we do so we just let the C# compiler handle that itself
        if (buildMode != BuildMode.CSharpTranspile) {
            // ? TODO Why do we have a substitutedMethodSymbol parameter here if it's never supplied?
            loweredBody = LocalFunctionRewriter.Rewrite(
                loweredBody,
                state.type,
                method,
                methodOrdinal,
                null,
                state,
                previousAnalyses,
                currentDiagnostics
            );
        }

        return loweredBody;
    }

    private static BoundBlockStatement BindMethodBody(
        MethodSymbol method,
        TypeCompilationState state,
        BelteDiagnosticQueue diagnostics,
        bool includeInitializers,
        BoundBlockStatement initializersBody) {
        BoundBlockStatement body = null;
        var syntax = method.GetNonNullSyntaxNode();
        initializersBody ??= new BoundBlockStatement(syntax, [], [], []);
        var builder = ArrayBuilder<BoundStatement>.GetInstance();
        BelteSyntaxNode syntaxNode = null;

        if (method is SourceMemberMethodSymbol sourceMethod) {
            syntaxNode = sourceMethod.syntaxNode;
            var bodyBinder = sourceMethod.TryGetBodyBinder();

            if (bodyBinder is null)
                return null;

            var methodBody = bodyBinder.BindMethodBody(syntaxNode, diagnostics);

            switch (methodBody) {
                case BoundConstructorMethodBody constructor:
                    body = constructor.body;

                    if (constructor.initializer is BoundExpressionStatement expressionStatement) {
                        ReportConstructorInitializerCycles(
                            method,
                            expressionStatement.expression,
                            state,
                            syntaxNode,
                            diagnostics
                        );

                        if (includeInitializers)
                            builder.Add(initializersBody);

                        builder.Add(constructor.initializer);

                        if (body is not null)
                            builder.Add(body);

                        body = new BoundBlockStatement(syntax, builder.ToImmutableAndFree(), constructor.locals, []);
                    }

                    return body;
                case BoundNonConstructorMethodBody nonConstructor:
                    body = nonConstructor.body;
                    break;
                case BoundBlockStatement block:
                    body = block;
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(methodBody.kind);
            }
        } else if (method is SynthesizedConstructorSymbol constructor) {
            // TODO Pretty sure we don't need to actually do anything here
            // var baseConstructorCall = Binder.GenerateBaseParameterlessConstructorInitializer(constructor, diagnostics);
            // var statement = new BoundExpressionStatement(syntax, baseConstructorCall);
            // body = new BoundBlockStatement(syntax, [statement], [], []);
        } else if (method is SynthesizedEntryPoint entryPoint) {
            var bodyBinder = entryPoint.GetBodyBinder();
            var bodyBuilder = ArrayBuilder<BoundStatement>.GetInstance();

            for (var i = 0; i < entryPoint.statements.Length; i++) {
                var statement = entryPoint.statements[i];
                var boundStatement = bodyBinder.BindStatement(statement.statement, diagnostics);

                if (i == entryPoint.statements.Length - 1 && boundStatement is BoundExpressionStatement e) {
                    var expression = e.expression;

                    if (!expression.type.IsVoidType())
                        boundStatement = new BoundReturnStatement(syntax, RefKind.None, e.expression);
                }

                bodyBuilder.Add(boundStatement);
            }

            body = new BoundBlockStatement(syntax, bodyBuilder.ToImmutableAndFree(), [], []);

            // body = ((BoundNonConstructorMethodBody)methodBody).body;
        }

        var constructorInitializer = BindImplicitConstructorInitializerIfAny(method, state, syntaxNode, diagnostics);

        if (includeInitializers)
            builder.Add(initializersBody);

        if (constructorInitializer is not null)
            builder.Add(constructorInitializer);

        if (body is not null)
            builder.Add(body);

        return new BoundBlockStatement(syntax, builder.ToImmutableAndFree(), [], []);
    }

    private static BoundStatement BindImplicitConstructorInitializerIfAny(
        MethodSymbol method,
        TypeCompilationState state,
        SyntaxNode syntax,
        BelteDiagnosticQueue diagnostics) {
        if (method.methodKind == MethodKind.Constructor) {
            var compilation = method.declaringCompilation;
            var call = Binder.BindImplicitConstructorInitializer(method, diagnostics, compilation);

            if (call is not null) {
                ReportConstructorInitializerCycles(method, call, state, syntax, diagnostics);
                return new BoundExpressionStatement(call.syntax, call);
            }
        }

        return null;
    }

    private static void ReportConstructorInitializerCycles(
        MethodSymbol method,
        BoundExpression expression,
        TypeCompilationState state,
        SyntaxNode syntax,
        BelteDiagnosticQueue diagnostics) {
        var call = expression as BoundCallExpression;

        if (call is not null &&
            call.method != method &&
            TypeSymbol.Equals(call.method.containingType, method.containingType, TypeCompareKind.ConsiderEverything)) {
            state.ReportConstructorInitializerCycles(method, call.method, syntax, diagnostics);
        }
    }
}

using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class LocalFunctionRewriter : BoundTreeRewriter {
    private ArrayBuilder<(MethodSymbol, BoundBlockStatement)> _synthesizedMethods;

    private LocalFunctionRewriter() {

    }

    internal static BoundBlockStatement Rewrite(
        BoundBlockStatement loweredBody,
        NamedTypeSymbol thisType,
        MethodSymbol method,
        int methodOrdinal,
        TypeCompilationState state,
        BelteDiagnosticQueue diagnostics) {
        var analysis = Analysis.Analyze(loweredBody, method, methodOrdinal, state);
        var rewriter = new LocalFunctionRewriter(

        );

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

    private void SynthesizeClosureMethods() {
        Analysis.VisitNestedFunctions(_analysis.ScopeTree, (scope, nestedFunction) => {
            var originalMethod = nestedFunction.OriginalMethodSymbol;
            var syntax = originalMethod.DeclaringSyntaxReferences[0].GetSyntax();

            int closureOrdinal;
            ClosureKind closureKind;
            NamedTypeSymbol translatedLambdaContainer;
            SynthesizedClosureEnvironment containerAsFrame;
            DebugId topLevelMethodId;
            DebugId lambdaId;

            if (nestedFunction.ContainingEnvironmentOpt != null) {
                containerAsFrame = nestedFunction.ContainingEnvironmentOpt.SynthesizedEnvironment;
                translatedLambdaContainer = containerAsFrame;

                closureKind = ClosureKind.General;
                closureOrdinal = containerAsFrame.ClosureId.Ordinal;
            } else if (nestedFunction.CapturesThis) {
                containerAsFrame = null;
                translatedLambdaContainer = _topLevelMethod.ContainingType;
                closureKind = ClosureKind.ThisOnly;
                closureOrdinal = LambdaDebugInfo.ThisOnlyClosureOrdinal;
            } else if ((nestedFunction.CapturedEnvironments.Count == 0 &&
                        originalMethod.MethodKind == MethodKind.LambdaMethod &&
                        _analysis.MethodsConvertedToDelegates.Contains(originalMethod)) ||
                       // If we are in a variant interface, runtime might not consider the
                       // method synthesized directly within the interface as variant safe.
                       // For simplicity we do not perform precise analysis whether this would
                       // definitely be the case. If we are in a variant interface, we always force
                       // creation of a display class.
                       VarianceSafety.GetEnclosingVariantInterface(_topLevelMethod) is object) {
                translatedLambdaContainer = containerAsFrame = GetStaticFrame(Diagnostics, syntax);
                closureKind = ClosureKind.Singleton;
                closureOrdinal = LambdaDebugInfo.StaticClosureOrdinal;
            } else {
                // Lower directly onto the containing type
                containerAsFrame = null;
                translatedLambdaContainer = _topLevelMethod.ContainingType;
                closureKind = ClosureKind.Static;
                closureOrdinal = LambdaDebugInfo.StaticClosureOrdinal;
            }

            Debug.Assert((object)translatedLambdaContainer != _topLevelMethod.ContainingType ||
                         VarianceSafety.GetEnclosingVariantInterface(_topLevelMethod) is null);

            var structEnvironments = getStructEnvironments(nestedFunction);

            // Move the body of the lambda to a freshly generated synthetic method on its frame.
            topLevelMethodId = _analysis.GetTopLevelMethodId();
            lambdaId = GetLambdaId(syntax, closureKind, closureOrdinal, structEnvironments.SelectAsArray(e => e.ClosureId), containerAsFrame?.RudeEdit);

            var synthesizedMethod = new SynthesizedClosureMethod(
                translatedLambdaContainer,
                structEnvironments,
                closureKind,
                _topLevelMethod,
                topLevelMethodId,
                originalMethod,
                nestedFunction.BlockSyntax,
                lambdaId,
                CompilationState);
            nestedFunction.SynthesizedLoweredMethod = synthesizedMethod;
        });

        static ImmutableArray<SynthesizedClosureEnvironment> getStructEnvironments(Analysis.NestedFunction function) {
            var environments = ArrayBuilder<SynthesizedClosureEnvironment>.GetInstance();

            foreach (var env in function.CapturedEnvironments) {
                if (env.IsStruct) {
                    environments.Add(env.SynthesizedEnvironment);
                }
            }

            return environments.ToImmutableAndFree();
        }
    }
}

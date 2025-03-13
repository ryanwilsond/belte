using System;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class LocalFunctionRewriter {
    private sealed class Analysis : BoundTreeWalkerWithStackGuardWithoutRecursionOnLeftOfBinaryOperator {
        internal readonly Scope scopeTree;

        private readonly MethodSymbol _topLevelMethod;
        private readonly int _topLevelMethodOrdinal;
        private readonly TypeCompilationState _compilationState;

        private int _ordinalCounter;
        private int _closureCounter;

        private Analysis(
            Scope scopeTree,
            MethodSymbol topLevelMethod,
            int topLevelMethodOrdinal,
            TypeCompilationState compilationState) {
            this.scopeTree = scopeTree;
            _topLevelMethod = topLevelMethod;
            _topLevelMethodOrdinal = topLevelMethodOrdinal;
            _compilationState = compilationState;
        }

        internal static Analysis Analyze(
            BoundStatement loweredBody,
            MethodSymbol method,
            int topLevelMethodOrdinal,
            TypeCompilationState compilationState) {
            var scopeTree = ScopeTreeBuilder.Build(loweredBody, method);
            var analysis = new Analysis(scopeTree, method, topLevelMethodOrdinal, compilationState);

            analysis.MakeAndAssignEnvironments();

            // TODO We don't have debugging yet, so we can always perform this merge
            // This can affect when a variable is in scope whilst debugging, so only do this in release mode.
            // if (compilationState.Compilation.Options.OptimizationLevel == OptimizationLevel.Release) {
            analysis.MergeEnvironments();
            // }

            analysis.InlineThisOnlyEnvironments();
            return analysis;
        }

        internal static NestedFunction GetNestedFunctionInTree(Scope treeRoot, MethodSymbol functionSymbol) {
            return Helper(treeRoot) ?? throw ExceptionUtilities.Unreachable();

            NestedFunction Helper(Scope scope) {
                foreach (var function in scope.nestedFunctions) {
                    if (function.originalMethodSymbol == functionSymbol)
                        return function;
                }

                foreach (var nestedScope in scope.nestedScopes) {
                    var found = Helper(nestedScope);

                    if (found is not null)
                        return found;
                }

                return null;
            }
        }

        internal int GetTopLevelMethodOrdinal() {
            return _ordinalCounter++;
        }

        internal int GetClosureOrdinal(ClosureEnvironment environment, SyntaxNode syntax) {
            return _closureCounter++;

            // TODO This shouldn't really matter
            // var parentClosure = environment.Parent?.SynthesizedEnvironment;

            // // Frames are created and assigned top-down, so the parent scope's environment has to be assigned at this point.
            // // This may not be true if environments are merged in release build.
            // Debug.Assert(_slotAllocator == null || environment.Parent is null || parentClosure is not null);

            // rudeEdit = parentClosure?.RudeEdit;
            // var parentClosureId = parentClosure?.ClosureId;

            // var structCaptures = _slotAllocator != null && environment.IsStruct
            //     ? environment.CapturedVariables.SelectAsArray(v => v is ThisParameterSymbol ? GeneratedNames.ThisProxyFieldName() : v.Name)
            //     : default;

            // DebugId closureId;
            // if (rudeEdit == null &&
            //     _slotAllocator != null &&
            //     _slotAllocator.TryGetPreviousClosure(syntax, parentClosureId, structCaptures, out var previousClosureId, out rudeEdit) &&
            //     rudeEdit == null) {
            //     closureId = previousClosureId;
            // } else {
            //     closureId = new DebugId(closureDebugInfo.Count, _compilationState.ModuleBuilderOpt.CurrentGenerationOrdinal);
            // }

            // int syntaxOffset = _topLevelMethod.CalculateLocalSyntaxOffset(LambdaUtilities.GetDeclaratorPosition(syntax), syntax.SyntaxTree);
            // closureDebugInfo.Add(new EncClosureInfo(new ClosureDebugInfo(syntaxOffset, closureId), parentClosureId, structCaptures));

            // return closureId;
        }

        internal static void VisitNestedFunctions(Scope scope, Action<Scope, NestedFunction> action) {
            foreach (var function in scope.nestedFunctions)
                action(scope, function);

            foreach (var nested in scope.nestedScopes)
                VisitNestedFunctions(nested, action);
        }

        internal static bool CheckNestedFunctions(Scope scope, Func<Scope, NestedFunction, bool> func) {
            foreach (var function in scope.nestedFunctions) {
                if (func(scope, function))
                    return true;
            }

            foreach (var nested in scope.nestedScopes) {
                if (CheckNestedFunctions(nested, func))
                    return true;
            }

            return false;
        }

        internal static void VisitScopeTree(Scope treeRoot, Action<Scope> action) {
            action(treeRoot);

            foreach (var nested in treeRoot.nestedScopes)
                VisitScopeTree(nested, action);
        }

        internal void Free() {
            scopeTree.Free();
        }

        private void InlineThisOnlyEnvironments() {
            if (!_topLevelMethod.TryGetThisParameter(out var thisParam) || thisParam is null)
                return;

            var env = scopeTree.declaredEnvironment;

            if (env is null)
                return;

            if (env.capturedVariables.Count > 1 ||
                !env.capturedVariables.Contains(thisParam)) {
                return;
            }

            if (env.isStruct) {
                var cantRemove = CheckNestedFunctions(scopeTree, (scope, closure) => {
                    return closure.capturedEnvironments.Contains(env) &&
                        closure.containingEnvironment is not null;
                });

                if (!cantRemove)
                    RemoveEnv();
            }
            // TODO What is variance safety
            // else if (VarianceSafety.GetEnclosingVariantInterface(_topLevelMethod) is null) {
            //     // Class-based 'this' closures can move member functions to
            //     // the top-level type and environments which capture the 'this'
            //     // environment can capture 'this' directly.
            //     // Note: the top-level type is treated as the initial containing
            //     // environment, so by removing the 'this' environment, all
            //     // nested environments which captured a pointer to the 'this'
            //     // environment will now capture 'this'
            //     RemoveEnv();
            //     VisitNestedFunctions(ScopeTree, (scope, closure) => {
            //         if (closure.ContainingEnvironmentOpt == env) {
            //             closure.ContainingEnvironmentOpt = null;
            //         }
            //     });
            // }

            void RemoveEnv() {
                scopeTree.declaredEnvironment = null;
                VisitNestedFunctions(scopeTree, (scope, nested) => {
                    var index = nested.capturedEnvironments.IndexOf(env);

                    if (index >= 0)
                        nested.capturedEnvironments.RemoveAt(index);
                });
            }
        }

        private void MakeAndAssignEnvironments() {
            VisitScopeTree(scopeTree, scope => {
                var variablesInEnvironment = scope.declaredLocals;

                if (variablesInEnvironment.Count == 0)
                    return;

                // var isStruct = VarianceSafety.GetEnclosingVariantInterface(_topLevelMethod) is null;
                var isStruct = false;
                var closures = new SetWithInsertionOrder<NestedFunction>();
                bool addedItem;

                do {
                    addedItem = false;
                    VisitNestedFunctions(scope, (closureScope, closure) => {
                        if (!closures.Contains(closure) &&
                            (closure.capturedVariables.Overlaps(scope.declaredLocals) ||
                             closure.capturedVariables.Overlaps(closures.Select(c => c.originalMethodSymbol)))) {
                            closures.Add(closure);
                            addedItem = true;
                            // This before check for CanTakeInRefParameters, but that should always be true
                            // isStruct &= true;
                            isStruct = true;
                        }
                    });
                } while (addedItem == true);

                var env = new ClosureEnvironment(variablesInEnvironment, isStruct);
                scope.declaredEnvironment = env;

                _topLevelMethod.TryGetThisParameter(out var thisParam);

                foreach (var closure in closures) {
                    closure.capturedEnvironments.Add(env);

                    if (thisParam is not null && env.capturedVariables.Contains(thisParam))
                        closure.capturesThis = true;
                }
            });
        }

        private PooledDictionary<Scope, PooledHashSet<NestedFunction>> CalculateFunctionsCapturingScopeVariables() {
            var closuresCapturingScopeVariables = PooledDictionary<Scope, PooledHashSet<NestedFunction>>.GetInstance();
            var environmentsToScopes = PooledDictionary<ClosureEnvironment, Scope>.GetInstance();

            VisitScopeTree(scopeTree, scope => {
                if (scope.declaredEnvironment is not null) {
                    closuresCapturingScopeVariables[scope] = PooledHashSet<NestedFunction>.GetInstance();
                    environmentsToScopes[scope.declaredEnvironment] = scope;
                }

                foreach (var closure in scope.nestedFunctions) {
                    foreach (var env in closure.capturedEnvironments)
                        closuresCapturingScopeVariables[environmentsToScopes[env]].Add(closure);
                }
            });

            environmentsToScopes.Free();

            foreach (var (scope, capturingClosures) in closuresCapturingScopeVariables) {
                if (scope.declaredEnvironment is null)
                    continue;

                var currentScope = scope;
                while (currentScope.declaredEnvironment is null || currentScope.declaredEnvironment.capturesParent) {
                    currentScope = currentScope.parent;

                    if (currentScope is null)
                        throw ExceptionUtilities.Unreachable();

                    if (currentScope.declaredEnvironment is null ||
                        currentScope.declaredEnvironment.isStruct) {
                        continue;
                    }

                    closuresCapturingScopeVariables[currentScope].AddAll(capturingClosures);
                }
            }

            return closuresCapturingScopeVariables;
        }

        private void MergeEnvironments() {
            var closuresCapturingScopeVariables = CalculateFunctionsCapturingScopeVariables();

            foreach (var (scope, closuresCapturingScope) in closuresCapturingScopeVariables) {
                if (closuresCapturingScope.Count == 0)
                    continue;

                var scopeEnv = scope.declaredEnvironment;

                if (scopeEnv.isStruct)
                    continue;

                var bestScope = scope;
                var currentScope = scope;

                while (currentScope.parent is not null) {
                    if (!currentScope.canMergeWithParent)
                        break;

                    var parentScope = currentScope.parent;

                    var env = parentScope.declaredEnvironment;
                    if (env is null || env.isStruct) {
                        currentScope = parentScope;
                        continue;
                    }

                    var closuresCapturingParentScope = closuresCapturingScopeVariables[parentScope];

                    if (!closuresCapturingParentScope.SetEquals(closuresCapturingScope))
                        break;

                    bestScope = parentScope;

                    currentScope = parentScope;
                }

                if (bestScope == scope)
                    continue;

                var targetEnv = bestScope.declaredEnvironment;

                foreach (var variable in scopeEnv.capturedVariables)
                    targetEnv.capturedVariables.Add(variable);

                scope.declaredEnvironment = null;

                foreach (var closure in closuresCapturingScope) {
                    closure.capturedEnvironments.Remove(scopeEnv);

                    if (!closure.capturedEnvironments.Contains(targetEnv))
                        closure.capturedEnvironments.Add(targetEnv);

                    if (closure.containingEnvironment == scopeEnv)
                        closure.containingEnvironment = targetEnv;
                }
            }

            foreach (var set in closuresCapturingScopeVariables.Values)
                set.Free();

            closuresCapturingScopeVariables.Free();
        }
    }
}

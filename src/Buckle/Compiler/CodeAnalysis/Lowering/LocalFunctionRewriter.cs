using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class LocalFunctionRewriter {
    internal static BoundBlockStatement Rewrite(
        BoundBlockStatement loweredBody,
        NamedTypeSymbol thisType,
        MethodSymbol method,
        int methodOrdinal,
        TypeCompilationState state,
        BelteDiagnosticQueue diagnostics) {
        var analysis = Analysis.Analyze(loweredBody, method, methodOrdinal, state);
        // TODO
        // var rewriter = new LocalFunctionRewriter(
        //     analysis,
        //     thisType,
        //     method,
        //     methodOrdinal,
        //     substitutedSourceMethod,
        //     state,
        //     diagnostics
        // );

        return loweredBody;
    }

    private sealed class Analysis : BoundTreeWalkerWithStackGuardWithoutRecursionOnLeftOfBinaryOperator {
        internal readonly Scope scopeTree;

        private readonly MethodSymbol _topLevelMethod;
        private readonly int _topLevelMethodOrdinal;
        private readonly TypeCompilationState _compilationState;

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

    private sealed class ScopeTreeBuilder : BoundTreeWalkerWithStackGuardWithoutRecursionOnLeftOfBinaryOperator {
        private readonly Dictionary<Symbol, Scope> _localToScope = [];
        private readonly MethodSymbol _topLevelMethod;

        private readonly PooledDictionary<LabelSymbol, ArrayBuilder<Scope>> _scopesAfterLabel
            = PooledDictionary<LabelSymbol, ArrayBuilder<Scope>>.GetInstance();
        private readonly ArrayBuilder<ArrayBuilder<LabelSymbol>> _labelsInScope
            = ArrayBuilder<ArrayBuilder<LabelSymbol>>.GetInstance();

        private Scope _currentScope;
        private NestedFunction _currentFunction;
        private bool _inExpressionTree;

        private ScopeTreeBuilder(Scope rootScope, MethodSymbol topLevelMethod) {
            _currentScope = rootScope;
            _labelsInScope.Push(ArrayBuilder<LabelSymbol>.GetInstance());
            _topLevelMethod = topLevelMethod;
        }

        internal static Scope Build(BoundNode node, MethodSymbol topLevelMethod) {
            var rootScope = new Scope(parent: null, boundNode: node, containingFunction: null);
            var builder = new ScopeTreeBuilder(rootScope, topLevelMethod);
            builder.Build();
            return rootScope;
        }

        private void Build() {
            DeclareLocals(_currentScope, _topLevelMethod.parameters);
            Visit(_currentScope.boundNode);

            foreach (var scopes in _scopesAfterLabel.Values)
                scopes.Free();

            _scopesAfterLabel.Free();
            var labels = _labelsInScope.Pop();
            labels.Free();
            _labelsInScope.Free();
        }

        internal override BoundNode VisitMethodGroup(BoundMethodGroup node)
            => throw ExceptionUtilities.Unreachable();

        internal override BoundNode VisitBlockStatement(BoundBlockStatement node) {
            var oldScope = _currentScope;
            PushOrReuseScope(node, node.locals);
            var result = base.VisitBlockStatement(node);
            PopScope(oldScope);
            return result;
        }

        internal override BoundNode VisitTryStatement(BoundTryStatement node) {
            // TODO This is probably wrong, doesn't seem to account for locals inside the catch and finally bodies
            var oldScope = _currentScope;
            PushOrReuseScope(node, ((BoundBlockStatement)node.body).locals);
            var result = base.VisitTryStatement(node);
            PopScope(oldScope);
            return result;
        }

        internal override BoundNode VisitLocalFunctionStatement(BoundLocalFunctionStatement node) {
            return VisitNestedFunction(node.symbol.originalDefinition, node.body);
        }

        private protected override void VisitArguments(BoundCallExpression node) {
            if (node.method.methodKind == MethodKind.LocalFunction)
                AddIfCaptured(node.method.originalDefinition);

            base.VisitArguments(node);
        }

        internal override BoundNode VisitParameterExpression(BoundParameterExpression node) {
            AddIfCaptured(node.parameter);
            return base.VisitParameterExpression(node);
        }

        internal override BoundNode VisitDataContainerExpression(BoundDataContainerExpression node) {
            AddIfCaptured(node.dataContainer);
            return base.VisitDataContainerExpression(node);
        }

        internal override BoundNode VisitBaseExpression(BoundBaseExpression node) {
            AddIfCaptured(_topLevelMethod.thisParameter);
            return base.VisitBaseExpression(node);
        }

        internal override BoundNode VisitThisExpression(BoundThisExpression node) {
            var thisParam = _topLevelMethod.thisParameter;

            if (thisParam is not null) {
                AddIfCaptured(thisParam);
            } else {
                // TODO Forwarded from Roslyn:
                // TODO: Why don't we drop "this" while lowering if method is static?
                //       Actually, considering that method group expression does not evaluate to a particular value
                //       why do we have it in the lowered tree at all?
            }

            return base.VisitThisExpression(node);
        }

        internal override BoundNode VisitLabelStatement(BoundLabelStatement node) {
            _labelsInScope.Peek().Add(node.label);
            _scopesAfterLabel.Add(node.label, ArrayBuilder<Scope>.GetInstance());
            return base.VisitLabelStatement(node);
        }

        internal override BoundNode VisitGotoStatement(BoundGotoStatement node) {
            CheckCanMergeWithParent(node.label);
            return base.VisitGotoStatement(node);
        }

        internal override BoundNode VisitConditionalGotoStatement(BoundConditionalGotoStatement node) {
            CheckCanMergeWithParent(node.label);
            return base.VisitConditionalGotoStatement(node);
        }

        private void CheckCanMergeWithParent(LabelSymbol jumpTarget) {
            if (_scopesAfterLabel.TryGetValue(jumpTarget, out var scopesAfterLabel)) {
                foreach (var scope in scopesAfterLabel)
                    scope.canMergeWithParent = false;

                scopesAfterLabel.Clear();
            }
        }

        private BoundNode VisitNestedFunction(MethodSymbol functionSymbol, BoundBlockStatement body) {
            if (body is null) {
                _currentScope.nestedFunctions.Add(new NestedFunction(functionSymbol, blockSyntax: null));
                return null;
            }

            var function = new NestedFunction(functionSymbol, new SyntaxReference(body.syntax));
            _currentScope.nestedFunctions.Add(function);

            var oldFunction = _currentFunction;
            _currentFunction = function;

            var oldScope = _currentScope;
            CreateAndPushScope(body);

            DeclareLocals(_currentScope, functionSymbol.parameters, _inExpressionTree);

            var result = _inExpressionTree
                ? base.VisitBlockStatement(body)
                : VisitBlockStatement(body);

            PopScope(oldScope);
            _currentFunction = oldFunction;
            return result;
        }

        private void AddIfCaptured(Symbol symbol) {
            if (_currentFunction is null)
                return;

            if (symbol is DataContainerSymbol local && local.isConstExpr)
                return;

            if (symbol is MethodSymbol method && _currentFunction.originalMethodSymbol == method)
                return;

            if (symbol.containingSymbol != _currentFunction.originalMethodSymbol) {
                var scope = _currentScope;
                var function = _currentFunction;

                while (function != null && symbol.containingSymbol != function.originalMethodSymbol) {
                    function.capturedVariables.Add(symbol);

                    while (scope.containingFunction == function)
                        scope = scope.parent;

                    function = scope.containingFunction;
                }

                if (symbol.kind == SymbolKind.Method)
                    return;

                if (_localToScope.TryGetValue(symbol, out var declarationScope))
                    declarationScope.declaredLocals.Add(symbol);
            }
        }

        private void PushOrReuseScope<TSymbol>(BoundNode node, ImmutableArray<TSymbol> locals) where TSymbol : Symbol {
            if (!locals.IsEmpty && _currentScope.boundNode != node)
                CreateAndPushScope(node);

            DeclareLocals(_currentScope, locals);
        }

        private void CreateAndPushScope(BoundNode node) {
            var scope = CreateNestedScope(_currentScope, _currentFunction);

            foreach (var label in _labelsInScope.Peek())
                _scopesAfterLabel[label].Add(scope);

            _labelsInScope.Push(ArrayBuilder<LabelSymbol>.GetInstance());
            _currentScope = scope;

            Scope CreateNestedScope(Scope parentScope, NestedFunction currentFunction) {
                var newScope = new Scope(parentScope, node, currentFunction);
                parentScope.nestedScopes.Add(newScope);
                return newScope;
            }
        }

        private void PopScope(Scope scope) {
            if (scope == _currentScope)
                return;

            var labels = _labelsInScope.Pop();

            foreach (var label in labels) {
                var scopes = _scopesAfterLabel[label];
                scopes.Free();
                _scopesAfterLabel.Remove(label);
            }

            labels.Free();
            _currentScope = _currentScope.parent;
        }

        private void DeclareLocals<TSymbol>(Scope scope, ImmutableArray<TSymbol> locals, bool declareAsFree = false)
            where TSymbol : Symbol {
            foreach (var local in locals) {
                if (!declareAsFree)
                    _localToScope.Add(local, scope);
            }
        }
    }

    private sealed class Scope {
        internal readonly Scope parent;
        internal readonly ArrayBuilder<Scope> nestedScopes = ArrayBuilder<Scope>.GetInstance();
        internal readonly ArrayBuilder<NestedFunction> nestedFunctions = ArrayBuilder<NestedFunction>.GetInstance();
        internal readonly SetWithInsertionOrder<Symbol> declaredLocals = new SetWithInsertionOrder<Symbol>();
        internal readonly BoundNode boundNode;
        internal readonly NestedFunction containingFunction;

        internal ClosureEnvironment declaredEnvironment;

        internal Scope(Scope parent, BoundNode boundNode, NestedFunction containingFunction) {
            this.parent = parent;
            this.boundNode = boundNode;
            this.containingFunction = containingFunction;
        }

        internal bool canMergeWithParent { get; set; } = true;

        internal void Free() {
            foreach (var scope in nestedScopes)
                scope.Free();

            nestedScopes.Free();

            foreach (var function in nestedFunctions)
                function.Free();

            nestedFunctions.Free();
        }

        public override string ToString() {
            return boundNode.syntax.ToString();
        }
    }

    private sealed class NestedFunction {
        internal readonly MethodSymbol originalMethodSymbol;
        internal readonly SyntaxReference blockSyntax;
        internal readonly PooledHashSet<Symbol> capturedVariables = PooledHashSet<Symbol>.GetInstance();
        internal readonly ArrayBuilder<ClosureEnvironment> capturedEnvironments
            = ArrayBuilder<ClosureEnvironment>.GetInstance();

        internal ClosureEnvironment containingEnvironment;
        // internal SynthesizedClosureMethod synthesizedLoweredMethod;

        internal NestedFunction(MethodSymbol symbol, SyntaxReference blockSyntax) {
            originalMethodSymbol = symbol;
            this.blockSyntax = blockSyntax;
        }

        internal bool capturesThis { get; set; }

        internal void Free() {
            capturedVariables.Free();
            capturedEnvironments.Free();
        }
    }

    [DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
    private sealed class ClosureEnvironment {
        internal readonly SetWithInsertionOrder<Symbol> capturedVariables;
        internal readonly bool isStruct;

        internal ClosureEnvironment parent;
        // internal SynthesizedClosureEnvironment synthesizedEnvironment;

        internal ClosureEnvironment(IEnumerable<Symbol> capturedVariables, bool isStruct) {
            this.capturedVariables = new SetWithInsertionOrder<Symbol>();

            foreach (var item in capturedVariables)
                this.capturedVariables.Add(item);

            this.isStruct = isStruct;
        }

        internal bool capturesParent => parent is not null;

        private string GetDebuggerDisplay() {
            var depth = 0;
            var current = parent;

            while (current is not null) {
                depth++;
                current = current.parent;
            }

            return $"{depth}: captures [{string.Join(", ", capturedVariables.Select(v => v.name))}]";
        }
    }
}

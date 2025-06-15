using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class LocalFunctionRewriter {
    private sealed class ScopeTreeBuilder : BoundTreeWalkerWithStackGuardWithoutRecursionOnLeftOfBinaryOperator {
        private readonly Dictionary<Symbol, Scope> _localToScope = [];
        private readonly MethodSymbol _topLevelMethod;
        private readonly CompilationOptions _options;

        private readonly PooledDictionary<LabelSymbol, ArrayBuilder<Scope>> _scopesAfterLabel
            = PooledDictionary<LabelSymbol, ArrayBuilder<Scope>>.GetInstance();
        private readonly ArrayBuilder<ArrayBuilder<LabelSymbol>> _labelsInScope
            = ArrayBuilder<ArrayBuilder<LabelSymbol>>.GetInstance();

        private Scope _currentScope;
        private NestedFunction _currentFunction;

        private ScopeTreeBuilder(Scope rootScope, MethodSymbol topLevelMethod, CompilationOptions options) {
            _currentScope = rootScope;
            _labelsInScope.Push(ArrayBuilder<LabelSymbol>.GetInstance());
            _topLevelMethod = topLevelMethod;
            _options = options;
        }

        internal static Scope Build(BoundNode node, MethodSymbol topLevelMethod, CompilationOptions options) {
            var rootScope = new Scope(parent: null, boundNode: node, containingFunction: null);
            var builder = new ScopeTreeBuilder(rootScope, topLevelMethod, options);
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

        internal override BoundNode VisitMethodGroup(BoundMethodGroup node) {
            if (_options.isScript)
                return base.VisitMethodGroup(node);

            throw ExceptionUtilities.Unreachable();
        }

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

            DeclareLocals(_currentScope, functionSymbol.parameters);

            var result = VisitBlockStatement(body);

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
}

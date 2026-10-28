using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

internal partial class Binder {
    private sealed class ExpansionBodyRewriter : BoundTreeExpander {
        private readonly DataContainerSymbol _returnLocal;
        private readonly LabelSymbol _successLabel;
        private readonly Binder _binder;
        private readonly Binder _inMethodBinder;
        private readonly BelteDiagnosticQueue _diagnostics;
        private readonly ImmutableArray<BoundExpression> _arguments;
        private readonly Dictionary<ParameterSymbol, DataContainerSymbol> _parameterMap;
        private readonly Dictionary<DataContainerSymbol, DataContainerSymbol> _localMap;

        private readonly Stack<Binder> _binderStack;
        private Binder _currentBodyBinder;

        private ExpansionBodyRewriter(
            Binder binder,
            Binder inMethodBinder,
            MethodSymbol method,
            ImmutableArray<DataContainerSymbol> locals,
            ImmutableArray<BoundExpression> arguments,
            BelteDiagnosticQueue diagnostics) {
            _binder = binder;
            _inMethodBinder = inMethodBinder;
            _diagnostics = diagnostics;
            _arguments = arguments;
            _parameterMap = [];
            _localMap = [];

            if (!method.returnType.IsVoidType()) {
                _returnLocal = CreateTempLocal("<return_local>", method.returnType, method.refKind, out var tempCount);
                _binder.AddLocalToScope(_returnLocal);
                _successLabel = new SynthesizedLabelSymbol($"<ReturnLabel>{tempCount - 1}");
            }

            foreach (var parameter in method.parameters) {
                if (parameter.isConstExpr)
                    continue;

                var parameterLocal = CreateTempLocal($"<{parameter.name}>", parameter.type, parameter.refKind, out _);
                _parameterMap.Add(parameter, parameterLocal);
                _binder.AddLocalToScope(parameterLocal);
            }

            foreach (var local in locals) {
                if (local.isConstExpr)
                    continue;

                var newLocal = CreateTempLocal(local.name, local.type, local.refKind, out _);
                _localMap.Add(local, newLocal);
                _binder.AddLocalToScope(newLocal);
            }

            _binderStack = new Stack<Binder>();
            _binderStack.Push(_inMethodBinder);
            _currentBodyBinder = _inMethodBinder;

            DataContainerSymbol CreateTempLocal(string prefix, TypeSymbol type, RefKind refKind, out int tempCount) {
                var name = prefix;
                tempCount = 0;

                while (binder.LookupLocal(SyntaxFactory.Identifier(name)) is not null)
                    name = $"{prefix}{tempCount++}";

                return new SynthesizedDataContainerSymbol(
                    binder.containingMember,
                    new TypeWithAnnotations(type),
                    SynthesizedLocalKind.ExpansionTemp,
                    name,
                    refKind
                );
            }
        }

        private protected override MethodSymbol _container { get; set => field = null; }

        internal static List<BoundStatement> Rewrite(
            Binder binder,
            Binder inMethodBinder,
            MethodSymbol method,
            ImmutableArray<BoundExpression> arguments,
            BoundBlockStatement body,
            BelteDiagnosticQueue diagnostics,
            out DataContainerSymbol returnLocal) {
            var rewriter = new ExpansionBodyRewriter(
                binder,
                inMethodBinder,
                method,
                body.locals,
                arguments,
                diagnostics
            );

            var rewritten = new List<BoundStatement>();

            if (rewriter._returnLocal is not null)
                rewritten.Add(BoundFactory.LocalDeclaration(body.syntax, rewriter._returnLocal, null));

            foreach (var (parameter, local) in rewriter._parameterMap)
                rewritten.Add(BoundFactory.LocalDeclaration(body.syntax, local, arguments[parameter.ordinal]));

            rewritten.AddRange(((BoundBlockStatement)rewriter.ExpandStatement(body)[0]).statements);

            if (rewriter._returnLocal is not null)
                rewritten.Add(BoundFactory.Label(body.syntax, rewriter._successLabel));

            returnLocal = rewriter._returnLocal;
            return rewritten;
        }

        private protected override List<BoundStatement> ExpandStatement(BoundStatement statement) {
            var statementBinder = _currentBodyBinder.GetBinder(statement.syntax);

            if (statementBinder is not null) {
                _currentBodyBinder = statementBinder;
                _binderStack.Push(statementBinder);
            }

            var result = base.ExpandStatement(statement);

            if (statementBinder is not null) {
                _binderStack.Pop();
                _currentBodyBinder = _binderStack.Peek();
            }

            return result;
        }

        private protected override List<BoundStatement> ExpandReturnStatement(
            BoundReturnStatement statement) {
            var syntax = statement.syntax;

            if (_returnLocal is null && statement.expression is not null) {
                _diagnostics.Push(
                    Error.UnexpectedReturnValue(((ReturnStatementSyntax)statement.syntax).keyword.location)
                );

                return [];
            } else if (_returnLocal is not null && statement.expression is null) {
                _diagnostics.Push(
                    Error.MissingReturnValue(((ReturnStatementSyntax)statement.syntax).keyword.location)
                );

                return [];
            }

            if (_returnLocal is null)
                return [BoundFactory.Goto(syntax, _successLabel)];

            var statements = ExpandExpression(statement.expression, out var newExpression);

            var convertedExpression = _binder.CreateReturnConversion(
                statement.syntax,
                _diagnostics,
                newExpression,
                _returnLocal.refKind,
                _returnLocal.type
            );

            statements.AddRange([
                BoundFactory.Statement(syntax, BoundFactory.Assignment(syntax,
                    BoundFactory.Local(syntax, _returnLocal),
                    convertedExpression,
                    false,
                    _returnLocal.type
                )),
                BoundFactory.Goto(syntax, _successLabel)
            ]);

            return statements;
        }

        private protected override List<BoundStatement> ExpandInsertStatement(BoundInsertStatement statement) {
            var inserted = _currentBodyBinder.BindInsertStatementContinued(
                statement.syntax,
                _diagnostics,
                _arguments[statement.parameter.ordinal].constantValue
            );

            return ExpandStatement(inserted);
        }

        private protected override List<BoundStatement> ExpandParameterExpression(
            BoundParameterExpression expression,
            out BoundExpression replacement,
            UseKind _) {
            if (_parameterMap.TryGetValue(expression.parameter, out var local)) {
                replacement = BoundFactory.Local(expression.syntax, local);
            } else {
                Debug.Assert(expression.parameter.isConstExpr);
                replacement = expression;
            }

            return [];
        }

        private protected override List<BoundStatement> ExpandDataContainerExpression(
            BoundDataContainerExpression expression,
            out BoundExpression replacement,
            UseKind _) {
            if (_localMap.TryGetValue(expression.dataContainer, out var local)) {
                replacement = BoundFactory.Local(expression.syntax, local);
            } else {
                Debug.Assert(expression.dataContainer.isConstExpr);
                replacement = expression;
            }

            return [];
        }

        private protected override List<BoundStatement> ExpandLocalDeclarationStatement(
            BoundLocalDeclarationStatement statement) {
            if (_localMap.TryGetValue(statement.declaration.dataContainer, out var local)) {
                return base.ExpandLocalDeclarationStatement(
                    BoundFactory.LocalDeclaration(statement.syntax, local, statement.declaration.initializer)
                );
            } else {
                Debug.Assert(statement.declaration.dataContainer.isConstExpr);
                return [statement];
            }
        }
    }
}

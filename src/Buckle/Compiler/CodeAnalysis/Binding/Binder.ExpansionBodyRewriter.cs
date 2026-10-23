using System.Collections.Generic;
using System.Collections.Immutable;
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

        private ExpansionBodyRewriter(
            Binder binder,
            Binder inMethodBinder,
            MethodSymbol method,
            ImmutableArray<BoundExpression> arguments,
            BelteDiagnosticQueue diagnostics) {
            _binder = binder;
            _inMethodBinder = inMethodBinder;
            _diagnostics = diagnostics;
            _arguments = arguments;
            _parameterMap = [];

            if (!method.returnType.IsVoidType()) {
                _returnLocal = CreateTempLocal("<return_local>", out var tempCount);
                _binder.AddLocalToScope(_returnLocal);
                _successLabel = new SynthesizedLabelSymbol($"<ReturnLabel>{tempCount - 1}");
            }

            foreach (var parameter in method.parameters) {
                if (parameter.isConstExpr)
                    continue;

                var parameterLocal = CreateTempLocal($"<{parameter.name}>", out _);
                _parameterMap.Add(parameter, parameterLocal);
                _binder.AddLocalToScope(parameterLocal);
            }

            DataContainerSymbol CreateTempLocal(string prefix, out int tempCount) {
                string name;
                tempCount = 0;

                do {
                    name = $"{prefix}{tempCount++}";
                } while (binder.LookupLocal(SyntaxFactory.Identifier(name)) is not null);

                return new SynthesizedDataContainerSymbol(
                    binder.containingMember,
                    new TypeWithAnnotations(method.returnType),
                    SynthesizedLocalKind.ExpansionTemp,
                    name
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
            var rewriter = new ExpansionBodyRewriter(binder, inMethodBinder, method, arguments, diagnostics);
            var rewritten = new List<BoundStatement>();

            if (rewriter._returnLocal is not null)
                rewritten.Add(BoundFactory.LocalDeclaration(body.syntax, rewriter._returnLocal, null));

            foreach (var (parameter, local) in rewriter._parameterMap)
                rewritten.Add(BoundFactory.LocalDeclaration(body.syntax, local, arguments[parameter.ordinal]));

            rewritten.AddRange(((BoundBlockStatement)rewriter.ExpandBlockStatement(body)[0]).statements);

            if (rewriter._returnLocal is not null)
                rewritten.Add(BoundFactory.Label(body.syntax, rewriter._successLabel));

            returnLocal = rewriter._returnLocal;
            return rewritten;
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
                // TODO Ref kind support
                RefKind.None,
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
            var inserted = _inMethodBinder.BindInsertStatementContinued(
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
            if (_parameterMap.TryGetValue(expression.parameter, out var local))
                replacement = BoundFactory.Local(expression.syntax, local);
            else
                replacement = expression;

            return [];
        }
    }
}

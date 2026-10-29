using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal partial class Binder {
    private sealed class ExpansionBodyRewriter : BoundTreeExpander {
        private readonly SyntaxNode _callNode;
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
            SyntaxNode callNode,
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
            _callNode = callNode;

            if (!method.returnType.IsVoidType()) {
                _returnLocal = CreateTempLocal("<return_local>", method.returnType, method.refKind, out var tempCount);
                _binder.AddLocalToScope(_returnLocal);
                _successLabel = new SynthesizedLabelSymbol($"<ReturnLabel>{tempCount - 1}");
            }

            foreach (var parameter in method.parameters) {
                if (parameter.isConstExpr)
                    continue;

                var parameterLocal = CreateTempLocal(parameter.name, parameter.type, parameter.refKind, out _);
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
        }

        private protected override MethodSymbol _container { get; set => field = null; }

        internal static List<BoundStatement> Rewrite(
            SyntaxNode callNode,
            Binder binder,
            Binder inMethodBinder,
            MethodSymbol method,
            ImmutableArray<BoundExpression> arguments,
            BoundBlockStatement body,
            BelteDiagnosticQueue diagnostics,
            out DataContainerSymbol returnLocal) {
            var rewriter = new ExpansionBodyRewriter(
                callNode,
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
                var location = new CompoundTextLocation(
                    ((ReturnStatementSyntax)statement.syntax).keyword.location,
                    _callNode
                );

                _diagnostics.Push(Error.UnexpectedReturnValue(location));

                return [];
            } else if (_returnLocal is not null && statement.expression is null) {
                var location = new CompoundTextLocation(
                    ((ReturnStatementSyntax)statement.syntax).keyword.location,
                    _callNode
                );

                _diagnostics.Push(Error.MissingReturnValue(location));

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
            } else if (expression.parameter.isConstExpr) {
                replacement = expression;
            } else {
                var old = expression.parameter;
                var parameterLocal = CreateTempLocal(old.name, old.type, old.refKind, out var _);
                _parameterMap.Add(old, parameterLocal);
                _binder.AddLocalToScope(parameterLocal);
                replacement = BoundFactory.Local(expression.syntax, parameterLocal);
            }

            return [];
        }

        private protected override List<BoundStatement> ExpandDataContainerExpression(
            BoundDataContainerExpression expression,
            out BoundExpression replacement,
            UseKind _) {
            if (_localMap.TryGetValue(expression.dataContainer, out var local)) {
                replacement = BoundFactory.Local(expression.syntax, local);
            } else if (expression.dataContainer.isConstExpr || _localMap.ContainsValue(expression.dataContainer)) {
                replacement = expression;
            } else {
                var old = expression.dataContainer;
                Debug.Assert(!_localMap.ContainsValue(old));
                var newLocal = CreateTempLocal(old.name, old.type, old.refKind, out var _);
                _localMap.Add(old, newLocal);
                _binder.AddLocalToScope(newLocal);
                replacement = BoundFactory.Local(expression.syntax, newLocal);
            }

            return [];
        }

        private protected override List<BoundStatement> ExpandLocalDeclarationStatement(
            BoundLocalDeclarationStatement statement) {
            if (_localMap.TryGetValue(statement.declaration.dataContainer, out var local)) {
                return base.ExpandLocalDeclarationStatement(
                    BoundFactory.LocalDeclaration(statement.syntax, local, statement.declaration.initializer)
                );
            } else if (statement.declaration.dataContainer.isConstExpr ||
                _localMap.ContainsValue(statement.declaration.dataContainer)) {
                return base.ExpandLocalDeclarationStatement(statement);
            } else {
                var old = statement.declaration.dataContainer;
                Debug.Assert(!_localMap.ContainsValue(old));
                var newLocal = CreateTempLocal(old.name, old.type, old.refKind, out _);
                _localMap.Add(old, newLocal);
                _binder.AddLocalToScope(newLocal);

                return base.ExpandLocalDeclarationStatement(
                    BoundFactory.LocalDeclaration(statement.syntax, newLocal, statement.declaration.initializer)
                );
            }
        }

        private protected override List<BoundStatement> ExpandLateIdentifierName(
            BoundLateIdentifierName expression,
            out BoundExpression replacement,
            UseKind _) {
            var name = ((LateIdentifierName)expression.syntax).name;
            var rewrittenName = (NameSyntax)name.WithCompoundLocation(_callNode);
            rewrittenName = (NameSyntax)rewrittenName.green.CreateRed(name.parent, name.position);

            var targetType = expression.type;
            var tempDiagnostics = BelteDiagnosticQueue.GetInstance();
            // First try attempts to bind the name to a local defined in the outer method (i.e. the one calling the expand method)
            var bound = _binder.BindExpression(rewrittenName, tempDiagnostics);

            if (bound.hasErrors) {
                tempDiagnostics.Clear();
                // The second try attempts to bind the name to a local defined inside the expand method (hence no compound location)
                var secondTry = _currentBodyBinder.BindExpression(name, tempDiagnostics);

                if (secondTry.hasErrors) {
                    _diagnostics.PushRangeAndFree(tempDiagnostics);
                    replacement = secondTry;
                    return [];
                }

                bound = secondTry;
            }

            _diagnostics.PushRangeAndFree(tempDiagnostics);

            if (bound.kind is not BoundKind.DataContainerExpression and not BoundKind.ParameterExpression) {
                string kind;

                switch (bound.kind) {
                    case BoundKind.FieldAccessExpression:
                        kind = MessageID.IDS_SK_FIELD.Localize();
                        break;
                    case BoundKind.MethodGroup:
                        kind = MessageID.IDS_SK_METHOD.Localize();
                        break;
                    case BoundKind.PropertyAccessExpression:
                        kind = MessageID.IDS_SK_PROPERTY.Localize();
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(bound.kind);
                }

                var location = new CompoundTextLocation(expression.syntax.location, _callNode);

                _diagnostics.Push(Error.LateIdentifierMustBeLocalOrParameter(location, name.ToString(), kind));

                replacement = _binder.ErrorExpression(expression.syntax);
                return [];
            } else {
                if ((bound.expressionSymbol is ParameterSymbol p &&
                    (p.refKind != RefKind.None || p.isConst || p.isConstExpr)) ||
                    (bound.expressionSymbol is DataContainerSymbol d &&
                    (d.refKind != RefKind.None || !d.isWritableVariable))) {
                    var location = new CompoundTextLocation(expression.syntax.location, _callNode);

                    _diagnostics.Push(Error.LateIdentifierMustBeNonRefAndVariable(location));

                    replacement = _binder.ErrorExpression(expression.syntax);
                    return [];
                }
            }

            var conversion = _binder.conversions.ClassifyConversionFromExpression(
                bound,
                targetType,
                isChecked: false
            );

            if (!conversion.isIdentity) {
                var location = new CompoundTextLocation(expression.syntax.location, _callNode);

                _diagnostics.Push(Error.LateIdentifierMustBeMatchType(location, targetType, bound.type));

                replacement = _binder.ErrorExpression(expression.syntax);
                return [];
            }

            return ExpandExpression(bound, out replacement);
        }

        private DataContainerSymbol CreateTempLocal(
            string prefix,
            TypeSymbol type,
            RefKind refKind,
            out int tempCount) {
            var name = prefix;
            tempCount = 0;

            while (_binder.LookupLocal(SyntaxFactory.Identifier(name)) is not null)
                name = $"{prefix}{tempCount++}";

            return new SynthesizedDataContainerSymbol(
                _binder.containingMember,
                new TypeWithAnnotations(type),
                SynthesizedLocalKind.ExpansionTemp,
                name,
                refKind
            );
        }
    }
}

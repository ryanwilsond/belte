using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Lowering;

/// <summary>
/// Expands expressions to make them simpler to handle by the <see cref="Lowerer" />.
/// </summary>
internal sealed class Expander : BoundTreeExpander {
    private readonly List<string> _localNames = [];
    private readonly MethodSymbol _container;

    private int _tempCount = 0;
    private int _compoundAssignmentDepth = 0;
    private int _operatorDepth = 0;

    internal Expander(MethodSymbol container) {
        _container = container;
    }

    internal BoundStatement Expand(BoundStatement statement) {
        return Simplify(statement.syntax, ExpandStatement(statement));
    }

    private protected override List<BoundStatement> ExpandLocalDeclarationStatement(
        BoundLocalDeclarationStatement statement) {
        _localNames.Add(statement.declaration.dataContainer.name);
        return base.ExpandLocalDeclarationStatement(statement);
    }

    private protected override List<BoundStatement> ExpandCompoundAssignmentOperator(
        BoundCompoundAssignmentOperator expression,
        out BoundExpression replacement) {
        _compoundAssignmentDepth++;
        var syntax = expression.syntax;

        if (_compoundAssignmentDepth > 1) {
            var statements = ExpandExpression(expression.left, out var newLeft);
            statements.AddRange(ExpandExpression(expression.right, out var newRight));

            statements.Add(
                new BoundExpressionStatement(
                    syntax,
                    new BoundCompoundAssignmentOperator(
                        syntax,
                        newLeft,
                        newRight,
                        expression.op,
                        expression.leftPlaceholder,
                        expression.leftConversion,
                        expression.finalPlaceholder,
                        expression.finalConversion,
                        expression.resultKind,
                        expression.originalUserDefinedOperators,
                        expression.type
                    )
                )
            );

            replacement = newLeft;
            _compoundAssignmentDepth--;
            return statements;
        }

        var baseStatements = base.ExpandCompoundAssignmentOperator(expression, out replacement);
        _compoundAssignmentDepth--;
        return baseStatements;
    }

    private protected override List<BoundStatement> ExpandNullCoalescingAssignmentOperator(
        BoundNullCoalescingAssignmentOperator expression,
        out BoundExpression replacement) {
        _compoundAssignmentDepth++;
        var syntax = expression.syntax;

        if (_compoundAssignmentDepth > 1) {
            var statements = ExpandExpression(expression.left, out var newLeft);
            statements.AddRange(ExpandExpression(expression.right, out var newRight));

            statements.Add(
                new BoundExpressionStatement(
                    syntax,
                    new BoundNullCoalescingAssignmentOperator(
                        syntax,
                        newLeft,
                        newRight,
                        expression.type
                    )
                )
            );

            replacement = newLeft;
            _compoundAssignmentDepth--;
            return statements;
        }

        var baseStatements = base.ExpandNullCoalescingAssignmentOperator(expression, out replacement);
        _compoundAssignmentDepth--;
        return baseStatements;
    }

    private protected override List<BoundStatement> ExpandCallExpression(
        BoundCallExpression expression,
        out BoundExpression replacement) {
        var syntax = expression.syntax;

        if (_operatorDepth > 0) {
            var statements = ExpandCallExpressionInternal(expression, out var callReplacement);
            var tempLocal = GenerateTempLocal(expression.type);

            statements.Add(new BoundLocalDeclarationStatement(
                syntax,
                new BoundDataContainerDeclaration(syntax, tempLocal, callReplacement)
            ));

            replacement = new BoundDataContainerExpression(syntax, tempLocal, null, tempLocal.type);

            return statements;
        }

        return ExpandCallExpressionInternal(expression, out replacement);
    }

    private List<BoundStatement> ExpandCallExpressionInternal(
        BoundCallExpression expression,
        out BoundExpression replacement) {
        /*
        TODO What did this do
        if (_transpilerMode && expression.method.containingType.Equals(StandardLibrary.Math)) {
            var statements = ExpandExpression(expression.expression, out var expressionReplacement);
            var replacementArguments = ArrayBuilder<BoundExpression>.GetInstance();

            foreach (var argument in expression.arguments) {
                var tempLocal = GenerateTempLocal(argument.type);
                statements.AddRange(ExpandExpression(argument, out var argumentReplacement));
                statements.Add(new BoundLocalDeclarationStatement(
                    new BoundDataContainerDeclaration(tempLocal, argumentReplacement)
                ));

                replacementArguments.Add(new BoundDataContainerExpression(tempLocal));
            }

            replacement = new BoundCallExpression(
                expressionReplacement,
                expression.method,
                replacementArguments.ToImmutableAndFree()
            );

            return statements;
        }
        */

        return base.ExpandCallExpression(expression, out replacement);
    }

    private protected override List<BoundStatement> ExpandBinaryOperator(
        BoundBinaryOperator expression,
        out BoundExpression replacement) {
        _operatorDepth++;

        if (_operatorDepth > 1) {
            var syntax = expression.syntax;
            var statements = ExpandExpression(expression.left, out var newLeft);
            statements.AddRange(ExpandExpression(expression.right, out var newRight));

            var tempLocal = GenerateTempLocal(expression.type);

            statements.Add(
                new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(
                    syntax,
                    tempLocal,
                    new BoundBinaryOperator(
                        syntax,
                        newLeft,
                        newRight,
                        expression.operatorKind,
                        expression.method,
                        expression.constantValue,
                        expression.type
                    )
                ))
            );

            replacement = new BoundDataContainerExpression(syntax, tempLocal, null, tempLocal.type);
            _operatorDepth--;
            return statements;
        }

        var baseStatements = base.ExpandBinaryOperator(expression, out replacement);
        _operatorDepth--;
        return baseStatements;
    }

    private protected override List<BoundStatement> ExpandCastExpression(
        BoundCastExpression expression,
        out BoundExpression replacement) {
        _operatorDepth++;

        if (_operatorDepth > 1) {
            var syntax = expression.syntax;
            var statements = ExpandExpression(expression.operand, out var newOperand);
            var tempLocal = GenerateTempLocal(expression.type);

            statements.Add(
                new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(
                    syntax,
                    tempLocal,
                    new BoundCastExpression(
                        syntax,
                        newOperand,
                        expression.conversion,
                        expression.constantValue,
                        expression.type
                    )
                ))
            );

            replacement = new BoundDataContainerExpression(syntax, tempLocal, null, tempLocal.type);
            _operatorDepth--;
            return statements;
        }

        var baseStatements = base.ExpandCastExpression(expression, out replacement);
        _operatorDepth--;
        return baseStatements;
    }

    private protected override List<BoundStatement> ExpandIncrementOperator(
        BoundIncrementOperator expression,
        out BoundExpression replacement) {
        if (expression.operatorKind.Operator() is UnaryOperatorKind.PrefixDecrement
                                               or UnaryOperatorKind.PrefixIncrement) {
            return base.ExpandIncrementOperator(expression, out replacement);
        }

        var syntax = expression.syntax;

        var statements = ExpandExpression(expression.operand, out var newOperand);
        var tempLocal = GenerateTempLocal(expression.type);

        statements.AddRange([
            new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(syntax, tempLocal, newOperand)),
            new BoundExpressionStatement(syntax, expression)
        ]);

        replacement = new BoundDataContainerExpression(syntax, tempLocal, null, tempLocal.type);
        return statements;
    }

    private protected override List<BoundStatement> ExpandConditionalOperator(
        BoundConditionalOperator expression,
        out BoundExpression replacement) {
        _operatorDepth++;
        var syntax = expression.syntax;

        if (_operatorDepth > 1) {
            var statements = ExpandExpression(expression.condition, out var newCondition);
            statements.AddRange(ExpandExpression(expression.trueExpression, out var newTrueExpression));
            statements.AddRange(ExpandExpression(expression.falseExpression, out var newFalseExpression));

            var tempLocal = GenerateTempLocal(expression.type);

            statements.Add(
                new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(
                    syntax,
                    tempLocal,
                    new BoundConditionalOperator(
                        syntax,
                        newCondition,
                        expression.isRef,
                        newTrueExpression,
                        newFalseExpression,
                        null,
                        expression.type
                    )
                ))
            );

            replacement = new BoundDataContainerExpression(syntax, tempLocal, null, tempLocal.type);
            _operatorDepth--;
            return statements;
        }

        var baseStatements = base.ExpandConditionalOperator(expression, out replacement);
        _operatorDepth--;
        return baseStatements;
    }

    private protected override List<BoundStatement> ExpandInitializerDictionary(
        BoundInitializerDictionary expression,
        out BoundExpression replacement) {
        // TODO Add a way where if _operatorDepth == 0 a temp local isn't made if this is a variable initializer
        var syntax = expression.syntax;
        var dictionaryType = expression.type as NamedTypeSymbol;
        var tempLocal = GenerateTempLocal(expression.type);
        var statements = new List<BoundStatement>() {
            new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(
                syntax,
                tempLocal,
                new BoundObjectCreationExpression(
                    syntax,
                    dictionaryType.constructors[0],
                    [],
                    [],
                    [],
                    default,
                    false,
                    expression.type
                )
            ))
        };

        var method = dictionaryType.GetMembers("Add").Single() as MethodSymbol;

        foreach (var pair in expression.items) {
            statements.Add(new BoundExpressionStatement(syntax, new BoundCallExpression(
                syntax,
                new BoundDataContainerExpression(syntax, tempLocal, null, tempLocal.type),
                method,
                [pair.Item1, pair.Item2],
                [RefKind.Ref, RefKind.Ref],
                default,
                LookupResultKind.Viable,
                method.returnType
            )));
        }

        replacement = new BoundDataContainerExpression(syntax, tempLocal, null, tempLocal.type);
        return statements;
    }

    private protected override List<BoundStatement> ExpandConditionalAccessExpression(
        BoundConditionalAccessExpression expression,
        out BoundExpression replacement) {
        var syntax = expression.syntax;
        var receiver = expression.receiver;
        var access = expression.accessExpression;
        var tempLocal = GenerateTempLocal(receiver.type);
        var statements = ExpandExpression(receiver, out var receiverReplacement);

        statements.Add(new BoundLocalDeclarationStatement(syntax,
            new BoundDataContainerDeclaration(syntax, tempLocal, receiverReplacement))
        );

        var receiverLocal = new BoundDataContainerExpression(syntax, tempLocal, null, tempLocal.type);

        BoundExpression newAccess;

        if (access is BoundFieldAccessExpression f) {
            newAccess = new BoundFieldAccessExpression(syntax, receiverLocal, f.field, f.constantValue, f.type);
        } else if (access is BoundArrayAccessExpression a) {
            statements.AddRange(ExpandExpression(a.index, out var indexReplacement));
            newAccess = new BoundArrayAccessExpression(syntax, receiverLocal, indexReplacement, null, a.type);
        } else {
            throw ExceptionUtilities.Unreachable();
        }

        replacement = new BoundConditionalOperator(
            syntax,
            HasValue(syntax, receiverLocal),
            false,
            newAccess,
            new BoundLiteralExpression(syntax, new ConstantValue(null), access.type),
            null,
            access.type
        );

        return statements;
    }

    private SynthesizedDataContainerSymbol GenerateTempLocal(TypeSymbol type) {
        string name;

        do {
            name = $"temp{_tempCount++}";
        } while (_localNames.Contains(name));

        return new SynthesizedDataContainerSymbol(_container, new TypeWithAnnotations(type), name);
    }
}

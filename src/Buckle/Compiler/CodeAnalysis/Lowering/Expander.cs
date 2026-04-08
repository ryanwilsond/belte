using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Libraries;
using Buckle.Utilities;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Lowering;

/// <summary>
/// Expands expressions to make them simpler to handle by the <see cref="Lowerer" />.
/// </summary>
internal sealed class Expander : BoundTreeExpander {
    private int _compoundAssignmentDepth = 0;
    private int _operatorDepth = 0;
    private int _conditionalDepth = 0;
    private int _accessDepth = 0;

    internal Expander(MethodSymbol container) {
        _container = container;
    }

    private protected override MethodSymbol _container { get; set; }

    internal BoundStatement Expand(BoundStatement statement) {
        return Simplify(statement.syntax, ExpandStatement(statement));
    }

    private protected override List<BoundStatement> ExpandCascadeListExpression(
        BoundCascadeListExpression expression,
        out BoundExpression replacement) {
        var syntax = expression.syntax;
        var statements = ExpandExpression(expression.receiver, out var newReceiver);
        var tempLocal = GenerateTempLocal(expression.Type());

        statements.Add(
            new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(
                syntax,
                tempLocal,
                newReceiver
            ))
        );

        for (var i = 0; i < expression.cascades.Length; i++) {
            var cascade = expression.cascades[i];
            var isConditional = expression.conditionals[i];
            // TODO How to represent conditional receiver on call?

            switch (cascade.kind) {
                case BoundKind.CallExpression: {
                        var call = (BoundCallExpression)cascade;
                        var replacementReceiver = Local(syntax, tempLocal);
                        statements.AddRange(ExpandExpressionList(call.arguments, out var arguments));

                        statements.Add(
                            new BoundExpressionStatement(syntax,
                                call.Update(
                                    replacementReceiver,
                                    call.method,
                                    arguments,
                                    call.argumentRefKinds,
                                    call.defaultArguments,
                                    call.resultKind,
                                    call.type
                                )
                            )
                        );
                    }

                    break;
                case BoundKind.CompoundAssignmentOperator: {
                        var assignment = (BoundCompoundAssignmentOperator)cascade;
                        var leftAccess = (BoundFieldAccessExpression)assignment.left;
                        statements.AddRange(ExpandExpression(assignment.right, out var right));

                        statements.Add(
                            new BoundExpressionStatement(syntax,
                                assignment.Update(
                                    MakeReplacementReceiver(syntax, isConditional, tempLocal, leftAccess),
                                    right,
                                    assignment.op,
                                    assignment.leftPlaceholder,
                                    assignment.leftConversion,
                                    assignment.finalPlaceholder,
                                    assignment.finalConversion,
                                    assignment.resultKind,
                                    assignment.originalUserDefinedOperators,
                                    assignment.type
                                )
                            )
                        );
                    }

                    break;
                case BoundKind.NullCoalescingAssignmentOperator: {
                        var assignment = (BoundNullCoalescingAssignmentOperator)cascade;
                        var leftAccess = (BoundFieldAccessExpression)assignment.left;
                        statements.AddRange(ExpandExpression(assignment.right, out var right));

                        statements.Add(
                            new BoundExpressionStatement(syntax,
                                assignment.Update(
                                    MakeReplacementReceiver(syntax, isConditional, tempLocal, leftAccess),
                                    right,
                                    assignment.isPropagation,
                                    assignment.type
                                )
                            )
                        );
                    }

                    break;
                case BoundKind.AssignmentOperator: {
                        var assignment = (BoundAssignmentOperator)cascade;
                        var leftAccess = (BoundFieldAccessExpression)assignment.left;
                        statements.AddRange(ExpandExpression(assignment.right, out var right));

                        statements.Add(
                            new BoundExpressionStatement(syntax,
                                assignment.Update(
                                    MakeReplacementReceiver(syntax, isConditional, tempLocal, leftAccess),
                                    right,
                                    assignment.isRef,
                                    assignment.type
                                )
                            )
                        );
                    }

                    break;
                default:
                    throw ExceptionUtilities.Unreachable();
            }
        }

        replacement = Local(syntax, tempLocal);
        return statements;

        static BoundExpression MakeReplacementReceiver(
            SyntaxNode syntax,
            bool isConditional,
            SynthesizedDataContainerSymbol tempLocal,
            BoundFieldAccessExpression leftAccess) {
            return isConditional
                ? new BoundConditionalAccessExpression(
                    syntax,
                    Local(syntax, tempLocal),
                    leftAccess,
                    leftAccess.type)
                : new BoundFieldAccessExpression(
                    syntax,
                    Local(syntax, tempLocal),
                    leftAccess.field,
                    leftAccess.constantValue,
                    leftAccess.type
                );
        }
    }

    private protected override List<BoundStatement> ExpandLocalFunctionStatement(
        BoundLocalFunctionStatement statement) {
        var oldContainer = _container;
        _container = statement.symbol;
        var newStatements = base.ExpandLocalFunctionStatement(statement);
        _container = oldContainer;
        return newStatements;
    }

    private protected override List<BoundStatement> ExpandLocalDeclarationStatement(
        BoundLocalDeclarationStatement statement) {
        _localNames.Add(statement.declaration.dataContainer.name);
        return base.ExpandLocalDeclarationStatement(statement);
    }

    private protected override List<BoundStatement> ExpandFieldAccessExpression(
        BoundFieldAccessExpression expression,
        out BoundExpression replacement) {
        if (expression.field.isStatic || expression.receiver.type.IsVerifierValue())
            return base.ExpandFieldAccessExpression(expression, out replacement);

        var type = expression.receiver.Type();
        var syntax = expression.syntax;

        var savedAccessDepth = _accessDepth;
        _accessDepth++;

        if (type.IsNullableType() && type.GetNullableUnderlyingType().IsStructType()) {
            var underlyingType = type.GetNullableUnderlyingType();

            var statements = ExpandExpression(expression.receiver, out var newReceiver);

            newReceiver = Lowerer.CreateNullableGetValueCall(syntax, newReceiver, underlyingType);
            var tempLocal = GenerateTempLocal(type);

            statements.AddRange(
                new BoundLocalDeclarationStatement(syntax,
                    new BoundDataContainerDeclaration(syntax, tempLocal, newReceiver)
                )
            );

            replacement = new BoundFieldAccessExpression(syntax,
                Local(syntax, tempLocal),
                expression.field,
                expression.constantValue,
                expression.field.type
            );

            _accessDepth = savedAccessDepth;
            return statements;
        }

        if (_conditionalDepth > 0 && _accessDepth <= 1) {
            var statements = ExpandExpression(expression.receiver, out var newReceiver);
            var tempLocal = GenerateTempLocal(expression.Type());

            statements.Add(
                new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(
                    syntax,
                    tempLocal,
                    new BoundFieldAccessExpression(
                        syntax,
                        newReceiver,
                        expression.field,
                        expression.constantValue,
                        expression.Type()
                    )
                ))
            );

            replacement = Local(syntax, tempLocal);

            _accessDepth = savedAccessDepth;
            return statements;
        }

        _accessDepth--;
        return base.ExpandFieldAccessExpression(expression, out replacement);
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
                        expression.Type()
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

    private protected override List<BoundStatement> ExpandNullCoalescingOperator(
        BoundNullCoalescingOperator expression,
        out BoundExpression replacement) {
        _operatorDepth++;
        _conditionalDepth++;

        var baseStatements = base.ExpandNullCoalescingOperator(expression, out replacement);

        _operatorDepth--;
        _conditionalDepth--;
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
                        expression.isPropagation,
                        expression.Type()
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

        if (_conditionalDepth > 0) {
            var statements = ExpandCallExpressionInternal(expression, out var callReplacement);
            var tempLocal = GenerateTempLocal(expression.Type());

            statements.Add(new BoundLocalDeclarationStatement(
                syntax,
                new BoundDataContainerDeclaration(syntax, tempLocal, callReplacement)
            ));

            replacement = Local(syntax, tempLocal);

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
        var savedConditionalDepth = _conditionalDepth;
        var syntax = expression.syntax;

        if (expression.left.Type().IsNullableType() || expression.right.Type().IsNullableType())
            _conditionalDepth++;

        if (_conditionalDepth > 1) {
            var statements = ExpandExpression(expression.left, out var newLeft);
            statements.AddRange(ExpandExpression(expression.right, out var newRight));

            var tempLocal = GenerateTempLocal(expression.Type());

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
                        expression.Type()
                    )
                ))
            );

            replacement = Local(syntax, tempLocal);
            _operatorDepth--;
            _conditionalDepth = savedConditionalDepth;
            return statements;
        }

        var baseStatements = base.ExpandBinaryOperator(expression, out replacement);
        _operatorDepth--;
        _conditionalDepth = savedConditionalDepth;
        return baseStatements;
    }

    private protected override List<BoundStatement> ExpandUnaryOperator(
        BoundUnaryOperator expression,
        out BoundExpression replacement) {
        _operatorDepth++;
        var savedConditionalDepth = _conditionalDepth;

        if (expression.operand.Type().IsNullableType())
            _conditionalDepth++;

        var baseStatements = base.ExpandUnaryOperator(expression, out replacement);
        _operatorDepth--;
        _conditionalDepth = savedConditionalDepth;
        return baseStatements;
    }

    private protected override List<BoundStatement> ExpandCastExpression(
        BoundCastExpression expression,
        out BoundExpression replacement) {
        _operatorDepth++;
        var savedConditionalDepth = _conditionalDepth;

        if (expression.operand.Type().IsNullableType() && expression.Type().IsNullableType())
            _conditionalDepth++;

        if (_conditionalDepth > 1 &&
            (expression.Type().IsNullableType() || expression.operand.Type().IsNullableType()) &&
            expression.conversion.underlyingConversions != default &&
            expression.conversion.kind is ConversionKind.ImplicitNullable or ConversionKind.ExplicitNullable &&
            !expression.operand.Type().Equals(expression.Type())) {
            var syntax = expression.syntax;
            var statements = ExpandExpression(expression.operand, out var newOperand);
            var tempLocal = GenerateTempLocal(expression.Type());

            statements.Add(
                new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(
                    syntax,
                    tempLocal,
                    new BoundCastExpression(
                        syntax,
                        newOperand,
                        expression.conversion,
                        expression.constantValue,
                        expression.Type()
                    )
                ))
            );

            replacement = Local(syntax, tempLocal);
            _operatorDepth--;
            _conditionalDepth = savedConditionalDepth;
            return statements;
        }

        var baseStatements = base.ExpandCastExpression(expression, out replacement);
        _operatorDepth--;
        _conditionalDepth = savedConditionalDepth;
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

        replacement = Local(syntax, tempLocal);
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

            var tempLocal = GenerateTempLocal(expression.Type());

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
                        expression.Type()
                    )
                ))
            );

            replacement = Local(syntax, tempLocal);
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
        var dictionaryType = (NamedTypeSymbol)expression.StrippedType();
        var tempLocal = GenerateTempLocal(expression.Type());
        var statements = new List<BoundStatement>() {
            new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(
                syntax,
                tempLocal,
                new BoundObjectCreationExpression(
                    syntax,
                    dictionaryType.instanceConstructors[0],
                    [],
                    [],
                    [],
                    default,
                    false,
                    expression.Type()
                )
            ))
        };

        var method = dictionaryType.GetMembers("Add").Single() as MethodSymbol;

        foreach (var pair in expression.items) {
            statements.Add(new BoundExpressionStatement(syntax, new BoundCallExpression(
                syntax,
                Local(syntax, tempLocal),
                method,
                [pair.Item1, pair.Item2],
                [RefKind.None, RefKind.None],
                default,
                LookupResultKind.Viable,
                method.returnType
            )));
        }

        replacement = Local(syntax, tempLocal);
        return statements;
    }

    private protected override List<BoundStatement> ExpandConditionalAccessExpression(
        BoundConditionalAccessExpression expression,
        out BoundExpression replacement) {
        var syntax = expression.syntax;
        var receiver = expression.receiver;
        var access = expression.accessExpression;
        var tempLocal = GenerateTempLocal(receiver.Type());
        var statements = ExpandExpression(receiver, out var receiverReplacement);

        statements.Add(new BoundLocalDeclarationStatement(syntax,
            new BoundDataContainerDeclaration(syntax, tempLocal, receiverReplacement))
        );

        var receiverLocal = Local(syntax, tempLocal);

        BoundExpression newAccess;

        if (access is BoundFieldAccessExpression f) {
            newAccess = new BoundFieldAccessExpression(syntax, receiverLocal, f.field, f.constantValue, f.Type());
        } else if (access is BoundArrayAccessExpression a) {
            statements.AddRange(ExpandExpression(a.index, out var indexReplacement));
            newAccess = new BoundArrayAccessExpression(syntax, receiverLocal, indexReplacement, null, a.Type());
        } else {
            throw ExceptionUtilities.Unreachable();
        }

        replacement = new BoundConditionalOperator(
            syntax,
            HasValue(syntax, receiverLocal),
            false,
            newAccess,
            new BoundLiteralExpression(syntax, ConstantValue.Null, access.Type()),
            null,
            access.Type()
        );

        return statements;
    }

    private protected override List<BoundStatement> ExpandInterpolatedStringExpression(
        BoundInterpolatedStringExpression expression,
        out BoundExpression replacement) {
        var syntax = expression.syntax;
        var stringType = CorLibrary.GetSpecialType(SpecialType.String);
        var nullableStringType = CorLibrary.GetNullableType(SpecialType.String);
        var statements = new List<BoundStatement>();
        var tempLocal = GenerateTempLocal(stringType);
        replacement = Local(syntax, tempLocal);
        statements.Add(new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(syntax,
            tempLocal,
            Literal(syntax, string.Empty, stringType)
        )));

        // ? Null turns into empty strings instead of nulling the entire result

        foreach (var content in expression.contents) {
            BoundExpression right;

            if (content.constantValue?.specialType == SpecialType.String) {
                right = Literal(syntax, content.constantValue.value, stringType);
            } else {
                statements.AddRange(ExpandExpression(content, out var replacementContent));

                if (replacementContent.IsLiteralNull())
                    continue;

                if (replacementContent.StrippedType().specialType == SpecialType.String) {
                    right = replacementContent;
                } else if (replacementContent.Type().IsVerifierValue()) {
                    if (!replacementContent.Type().IsNullableType()) {
                        right = CreateCast(syntax, stringType, replacementContent);
                    } else {
                        var conversion = Conversion.Classify(replacementContent.StrippedType(), stringType);
                        right = new BoundConditionalOperator(syntax,
                            new BoundIsOperator(syntax,
                                replacementContent,
                                Literal(syntax, null, nullableStringType),
                                false,
                                null,
                                CorLibrary.GetSpecialType(SpecialType.Bool)
                            ),
                            false,
                            Literal(syntax, string.Empty, stringType),
                            Cast(syntax,
                                stringType,
                                Lowerer.CreateNullableGetValueCall(syntax, replacementContent, replacementContent.StrippedType()),
                                conversion,
                                null
                            ),
                            null,
                            stringType
                        );
                    }
                } else {
                    var toString = (MethodSymbol)CorLibrary.GetSpecialType(SpecialType.Object)
                        .GetMembers("ToString").Single(m => m is MethodSymbol);

                    var toStringTemp = GenerateTempLocal(nullableStringType);
                    right = Local(syntax, toStringTemp);

                    statements.Add(new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(syntax,
                        toStringTemp,
                        new BoundConditionalOperator(syntax,
                            new BoundIsOperator(syntax,
                                replacementContent,
                                Literal(syntax, null, nullableStringType),
                                false,
                                null,
                                CorLibrary.GetSpecialType(SpecialType.Bool)
                            ),
                            false,
                            Literal(syntax, null, nullableStringType),
                            new BoundCallExpression(syntax,
                                replacementContent,
                                toString,
                                [],
                                [],
                                BitVector.Empty,
                                LookupResultKind.Viable,
                                nullableStringType
                            ),
                            null,
                            nullableStringType
                        )
                    )));
                }
            }

            if (right.Type().IsNullableType()) {
                right = new BoundNullCoalescingOperator(syntax,
                    right,
                    Literal(syntax, string.Empty, stringType),
                    false,
                    null,
                    stringType
                );
            }

            statements.Add(new BoundExpressionStatement(syntax, Assignment(syntax,
                replacement,
                Binary(syntax,
                    replacement,
                    BinaryOperatorKind.StringConcatenation,
                    right,
                    stringType
                ),
                false,
                stringType
            )));
        }

        return statements;
    }
}

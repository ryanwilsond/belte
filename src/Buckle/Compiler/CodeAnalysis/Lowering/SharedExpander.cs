using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Lowering;

internal class SharedExpander : BoundTreeExpander {
    private protected readonly BelteDiagnosticQueue _diagnostics;

    internal SharedExpander(MethodSymbol container, BelteDiagnosticQueue diagnostics) {
        _container = container;
        _diagnostics = diagnostics;
    }

    private protected override MethodSymbol _container { get; set; }

    internal BoundStatement Expand(BoundStatement statement) {
        return Simplify(statement.syntax, ExpandStatement(statement));
    }

    private protected override List<BoundStatement> ExpandExpression(
        BoundExpression expression,
        out BoundExpression replacement,
        UseKind useKind = UseKind.Value) {
        if (expression.constantValue is not null) {
            replacement = Lowerer.VisitConstant(expression);
            return [];
        }

        return base.ExpandExpression(expression, out replacement, useKind);
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

    private protected override List<BoundStatement> ExpandCastExpression(
        BoundCastExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        if (expression.conversion.kind is ConversionKind.ImplicitNullToPointer) {
            replacement = expression;
            return [];
        }

        return base.ExpandCastExpression(expression, out replacement, useKind);
    }

    private protected override List<BoundStatement> ExpandCascadeListExpression(
        BoundCascadeListExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var syntax = expression.syntax;
        var statements = ExpandExpression(expression.receiver, out var newReceiver, UseKind.Writable);
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

            switch (cascade.kind) {
                case BoundKind.ConditionalAccessExpression: {
                        var condAccess = (BoundConditionalAccessExpression)cascade;

                        statements.AddRange(
                            ExpandStatement(new BoundExpressionStatement(syntax,
                                condAccess.Update(
                                    Local(syntax, tempLocal),
                                    condAccess.accessExpression,
                                    condAccess.type
                                )
                            ))
                        );
                    }

                    break;
                case BoundKind.CallExpression: {
                        var call = (BoundCallExpression)cascade;
                        var replacementReceiver = Local(syntax, tempLocal);
                        statements.AddRange(ExpandArgumentList(call.arguments, out var arguments));

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
                        var leftAccess = isConditional
                            ? (BoundFieldAccessExpression)(assignment.left as BoundConditionalAccessExpression)
                                .accessExpression
                            : (BoundFieldAccessExpression)assignment.left;

                        statements.AddRange(
                            ExpandStatement(new BoundExpressionStatement(syntax,
                                assignment.Update(
                                    MakeReplacementReceiver(syntax, isConditional, tempLocal, leftAccess),
                                    assignment.right,
                                    assignment.op,
                                    assignment.leftPlaceholder,
                                    assignment.leftConversion,
                                    assignment.finalPlaceholder,
                                    assignment.finalConversion,
                                    assignment.resultKind,
                                    assignment.originalUserDefinedOperators,
                                    assignment.type
                                )
                            ))
                        );
                    }

                    break;
                case BoundKind.NullCoalescingAssignmentOperator: {
                        var assignment = (BoundNullCoalescingAssignmentOperator)cascade;
                        var leftAccess = isConditional
                            ? (BoundFieldAccessExpression)(assignment.left as BoundConditionalAccessExpression)
                                .accessExpression
                            : (BoundFieldAccessExpression)assignment.left;

                        statements.AddRange(
                            ExpandStatement(new BoundExpressionStatement(syntax,
                                assignment.Update(
                                    MakeReplacementReceiver(syntax, isConditional, tempLocal, leftAccess),
                                    assignment.right,
                                    assignment.isPropagation,
                                    assignment.type
                                )
                            ))
                        );
                    }

                    break;
                case BoundKind.AssignmentOperator: {
                        var assignment = (BoundAssignmentOperator)cascade;
                        var leftAccess = isConditional
                            ? (BoundFieldAccessExpression)(assignment.left as BoundConditionalAccessExpression)
                                .accessExpression
                            : (BoundFieldAccessExpression)assignment.left;

                        statements.AddRange(
                            ExpandStatement(new BoundExpressionStatement(syntax,
                                assignment.Update(
                                    MakeReplacementReceiver(syntax, isConditional, tempLocal, leftAccess),
                                    assignment.right,
                                    assignment.isRef,
                                    assignment.type
                                )
                            ))
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

    private protected override List<BoundStatement> ExpandInterpolatedStringExpression(
        BoundInterpolatedStringExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
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
                if (content.IsLiteralNull())
                    continue;

                statements.AddRange(ExpandExpression(content, out var replacementContent, UseKind.StableValue));

                if (replacementContent.StrippedType().specialType == SpecialType.String) {
                    right = replacementContent;
                } else if (replacementContent.Type().IsVerifierValue()) {
                    if (!replacementContent.Type().IsNullableType()) {
                        var conversion = Conversion.Classify(replacementContent.Type(), stringType);

                        if (!conversion.exists) {
                            _diagnostics.Push(
                                Error.CannotConvert(syntax.location, replacementContent.Type(), stringType)
                            );
                        }

                        right = Cast(syntax, stringType, replacementContent, conversion, null);
                    } else {
                        var conversion = Conversion.Classify(replacementContent.StrippedType(), stringType);

                        if (!conversion.exists) {
                            _diagnostics.Push(
                                Error.CannotConvert(syntax.location, replacementContent.StrippedType(), stringType)
                            );
                        }

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
                                new BoundNullAssertOperator(syntax,
                                    replacementContent,
                                    false,
                                    null,
                                    replacementContent.StrippedType()
                                ),
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
                statements.AddRange(ExpandExpression(new BoundNullCoalescingOperator(syntax,
                    right,
                    Literal(syntax, string.Empty, stringType),
                    false,
                    null,
                    stringType
                ), out right, UseKind.Value));
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

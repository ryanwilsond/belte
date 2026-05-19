using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Lowering;

internal class SharedExpander : BoundTreeExpander {
    private protected readonly BelteDiagnosticQueue _diagnostics;

    internal SharedExpander(MethodSymbol container, BelteDiagnosticQueue diagnostics) {
        _container = container;
        _diagnostics = diagnostics;
    }

    private protected override MethodSymbol _container { get; set; }

    internal BoundBlockStatement Expand(BoundBlockStatement statement) {
        _localNames.AddRange(statement.locals.Select(l => l.name));
        return (BoundBlockStatement)Simplify(statement.syntax, ExpandStatement(statement));
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

    private protected override List<BoundStatement> ExpandUsingStatement(BoundUsingStatement statement) {
        /*

        using (<declaration>)
            <body>

        ---->

        <declaration>
        try {
            <body>
        } finally {
            <declaration.dispose>
        }

        */
        var syntax = statement.syntax;
        var symbol = statement.declaration.declaration.dataContainer;
        var disposeMethod = statement.declaration.disposeMethod;

        var statements = ExpandLocalDeclarationStatement(
            new BoundLocalDeclarationStatement(syntax, statement.declaration.declaration, false, null)
        );

        var bodyStatements = ExpandStatement(statement.body);

        statements.Add(CreateUsingTry(syntax, bodyStatements.ToImmutableArray(), symbol, disposeMethod));

        return statements;
    }

    internal BoundTryStatement CreateUsingTry(
        SyntaxNode syntax,
        ImmutableArray<BoundStatement> tryBody,
        DataContainerSymbol local,
        MethodSymbol disposeMethod) {
        ImmutableArray<BoundStatement> finallyBody;

        BoundExpression call = new BoundCallExpression(
            syntax,
            Local(syntax, local),
            disposeMethod,
            [],
            [],
            BitVector.Empty,
            LookupResultKind.Empty,
            disposeMethod.returnType
        );

        if (local.type.IsNullableType()) {
            var breakLabel = GenerateLabel();

            finallyBody = [
                GotoIf(syntax, breakLabel, IsNull(syntax, Local(syntax, local))),
                Statement(syntax, call),
                Label(syntax, breakLabel)
            ];
        } else {
            finallyBody = [Statement(syntax, call)];
        }

        return new BoundTryStatement(syntax,
            new BoundBlockStatement(syntax, tryBody, [], []),
            null,
            new BoundBlockStatement(syntax, finallyBody, [], [])
        );
    }

    private protected override List<BoundStatement> ExpandExpressionStatement(BoundExpressionStatement statement) {
        /*

        ----> (... ? <call> : null) where <call> returns void

        goto break if ...
        <call>
        break:

        */
        var statements = ExpandExpression(statement.expression, out var replacement, UseKind.None);

        if (statements.Count == 0 && statement.expression == replacement)
            return [statement];

        // TODO Could this instead be moved into conditional access operator detecting UseKind.None?
        if (replacement is BoundConditionalOperator c && c.trueExpression.type.IsVoidType())
            return RewriteVoidTernaryCall(c);

        if (replacement is not null)
            statements.Add(statement.Update(replacement));

        return statements;
    }

    internal List<BoundStatement> RewriteVoidTernaryCall(BoundConditionalOperator conditional) {
        if (!conditional.falseExpression.IsLiteralNull())
            throw ExceptionUtilities.Unreachable();

        var syntax = conditional.syntax;

        var breakLabel = GenerateLabel();

        var statements = new List<BoundStatement> {
            GotoIfNot(syntax, breakLabel, conditional.condition),
            Statement(syntax, conditional.trueExpression),
            Label(syntax, breakLabel)
        };

        return statements;
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
        UseKind _) {
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
                            ExpandStatement(Statement(syntax,
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
                            Statement(syntax,
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
                            ExpandStatement(Statement(syntax,
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
                            ExpandStatement(Statement(syntax,
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
                            ExpandStatement(Statement(syntax,
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
        UseKind _) {
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

            statements.Add(Statement(syntax, Assignment(syntax,
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

    private protected override List<BoundStatement> ExpandWithStatement(BoundWithStatement statement) {
        /*

        with (<assignments>) {
            <body>
        }

        ---->

        temp0 = <assignment0.left>
        <assignment0>
        ...

        <body>

        <assignment0.left> = temp0
        ...

        ----> surround with try

        temp0 = <assignment0.left>
        <assignment0>
        ...

        try {
            <body>
        } finally {
            <assignment0.left> = temp0
            ...
        }

        */
        var syntax = statement.syntax;

        var lefts = statement.assignments.SelectAsArray(a => GetLeft(a));
        var temps = lefts.SelectAsArray(l => GenerateTempLocal(l.Item1.type) as DataContainerSymbol);

        var statements = CreateWithPrologue(syntax, lefts, temps, statement.assignments, out var newLefts);

        if (statement.wrapWithTry) {
            var tryBody = Block(syntax, ExpandStatement(statement.body).ToArray());
            var finallyBody = Block(syntax, CreateWithEpilogue(syntax, newLefts, temps).ToArray());
            statements.Add(new BoundTryStatement(syntax, tryBody, null, finallyBody));
        } else {
            statements.AddRange(ExpandStatement(statement.body));
            statements.AddRange(CreateWithEpilogue(syntax, newLefts, temps));
        }

        return statements;
    }

    private protected override List<BoundStatement> ExpandWithExpression(
        BoundWithExpression expression,
        out BoundExpression replacement,
        UseKind _) {
        /*

        with (<assignments>) <body>

        ---->

        temp0 = <assignment0.left>
        <assignment0>
        ...

        tempN = <body>

        <assignment0.left> = temp0
        ...

        tempN

        */
        var syntax = expression.syntax;
        var body = expression.body;

        var lefts = expression.assignments.SelectAsArray(a => GetLeft(a));
        var temps = lefts.SelectAsArray(l => GenerateTempLocal(l.Item1.type) as DataContainerSymbol);

        var tempN = GenerateTempLocal(body.type);

        var statements = CreateWithPrologue(syntax, lefts, temps, expression.assignments, out var newLefts);

        statements.AddRange(ExpandExpression(body, out var newBody));
        statements.Add(LocalDeclaration(syntax, tempN, newBody));

        statements.AddRange(CreateWithEpilogue(syntax, newLefts, temps));

        replacement = Local(syntax, tempN);
        return statements;
    }

    private List<BoundStatement> CreateWithPrologue(
        SyntaxNode syntax,
        ImmutableArray<(BoundExpression, bool)> lefts,
        ImmutableArray<DataContainerSymbol> temps,
        ImmutableArray<BoundExpression> assignments,
        out ImmutableArray<(BoundExpression, bool)> newLefts) {
        var statements = new List<BoundStatement>();
        var builder = ArrayBuilder<(BoundExpression, bool)>.GetInstance(lefts.Length);

        for (var i = 0; i < assignments.Length; i++) {
            var (left, isRef) = lefts[i];
            var temp = temps[i];
            var assignment = assignments[i];

            statements.AddRange(ExpandExpression(left, out var newLeft, UseKind.Writable));
            statements.Add(LocalDeclaration(syntax, temp, newLeft));
            statements.AddRange(RecreateAssignment(syntax, newLeft, assignment));

            builder.Add((newLeft, isRef));
        }

        newLefts = builder.ToImmutableAndFree();
        return statements;
    }

    private List<BoundStatement> CreateWithEpilogue(
        SyntaxNode syntax,
        ImmutableArray<(BoundExpression, bool)> lefts,
        ImmutableArray<DataContainerSymbol> temps) {
        var statements = new List<BoundStatement>();

        for (var i = temps.Length - 1; i >= 0; i--) {
            var (left, isRef) = lefts[i];
            var temp = temps[i];
            statements.Add(Statement(syntax, Assignment(syntax, left, Local(syntax, temp), isRef, temp.type)));
        }

        return statements;
    }

    private static (BoundExpression, bool) GetLeft(BoundExpression expression) {
        switch (expression.kind) {
            case BoundKind.AssignmentOperator:
                var assignment = (BoundAssignmentOperator)expression;
                return (assignment.left, assignment.isRef);
            case BoundKind.CompoundAssignmentOperator:
                return (((BoundCompoundAssignmentOperator)expression).left, false);
            case BoundKind.NullCoalescingAssignmentOperator:
                return (((BoundNullCoalescingAssignmentOperator)expression).left, false);
            default:
                throw ExceptionUtilities.UnexpectedValue(expression.kind);
        }
    }

    private List<BoundStatement> RecreateAssignment(
        SyntaxNode syntax,
        BoundExpression newLeft,
        BoundExpression expression) {
        switch (expression.kind) {
            case BoundKind.AssignmentOperator: {
                    var assignment = (BoundAssignmentOperator)expression;
                    var newAssignment = new BoundAssignmentOperator(
                        syntax,
                        newLeft,
                        assignment.right,
                        assignment.isRef,
                        assignment.type
                    );

                    var statements = ExpandExpression(newAssignment, out var replacement);
                    statements.Add(Statement(syntax, replacement));
                    return statements;
                }
            case BoundKind.CompoundAssignmentOperator: {
                    var assignment = (BoundCompoundAssignmentOperator)expression;
                    var newAssignment = new BoundCompoundAssignmentOperator(
                        syntax,
                        newLeft,
                        assignment.right,
                        assignment.op,
                        assignment.leftPlaceholder,
                        assignment.leftConversion,
                        assignment.finalPlaceholder,
                        assignment.finalConversion,
                        assignment.resultKind,
                        assignment.originalUserDefinedOperators,
                        assignment.type
                    );

                    var statements = ExpandExpression(newAssignment, out var replacement);
                    statements.Add(Statement(syntax, replacement));
                    return statements;
                }
            case BoundKind.NullCoalescingAssignmentOperator: {
                    var assignment = (BoundNullCoalescingAssignmentOperator)expression;
                    var newAssignment = new BoundNullCoalescingAssignmentOperator(
                        syntax,
                        newLeft,
                        assignment.right,
                        assignment.isPropagation,
                        assignment.type
                    );

                    var statements = ExpandExpression(newAssignment, out var replacement);
                    statements.Add(Statement(syntax, replacement));
                    return statements;
                }
            default:
                throw ExceptionUtilities.UnexpectedValue(expression.kind);
        }
    }
}

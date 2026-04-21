using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Rewrites BoundStatements and all child BoundStatements, allowing expansion.
/// </summary>
internal abstract partial class BoundTreeExpander {
    private protected readonly List<string> _localNames = [];

    private protected int _tempCount = 0;
    private protected int _labelCount = 0;

    private protected abstract MethodSymbol _container { get; set; }

    private protected static BoundStatement Simplify(SyntaxNode syntax, List<BoundStatement> statements) {
        if (statements.Count == 1)
            return statements[0];
        else
            return Block(syntax, statements.ToArray());
    }

    private protected SynthesizedLabelSymbol GenerateLabel(string suffix = null) {
        return new SynthesizedLabelSymbol($"ExpLabel{++_labelCount}{suffix}");
    }

    private protected SynthesizedDataContainerSymbol GenerateTempLocal(TypeSymbol type) {
        string name;

        do {
            name = $"temp{_tempCount++}";
        } while (_localNames.Contains(name));

        return new SynthesizedDataContainerSymbol(
            _container,
            new TypeWithAnnotations(type),
            SynthesizedLocalKind.ExpanderTemp,
            name
        );
    }

    private protected virtual List<BoundStatement> ExpandStatement(BoundStatement statement) {
        return statement.kind switch {
            BoundKind.NopStatement => ExpandNopStatement((BoundNopStatement)statement),
            BoundKind.BlockStatement => ExpandBlockStatement((BoundBlockStatement)statement),
            BoundKind.LocalDeclarationStatement => ExpandLocalDeclarationStatement((BoundLocalDeclarationStatement)statement),
            BoundKind.IfStatement => ExpandIfStatement((BoundIfStatement)statement),
            BoundKind.NullBindingStatement => ExpandNullBindingStatement((BoundNullBindingStatement)statement),
            BoundKind.WhileStatement => ExpandWhileStatement((BoundWhileStatement)statement),
            BoundKind.ForStatement => ExpandForStatement((BoundForStatement)statement),
            BoundKind.ForEachStatement => ExpandForEachStatement((BoundForEachStatement)statement),
            BoundKind.ExpressionStatement => ExpandExpressionStatement((BoundExpressionStatement)statement),
            BoundKind.LabelStatement => ExpandLabelStatement((BoundLabelStatement)statement),
            BoundKind.GotoStatement => ExpandGotoStatement((BoundGotoStatement)statement),
            BoundKind.ConditionalGotoStatement => ExpandConditionalGotoStatement((BoundConditionalGotoStatement)statement),
            BoundKind.DoWhileStatement => ExpandDoWhileStatement((BoundDoWhileStatement)statement),
            BoundKind.ReturnStatement => ExpandReturnStatement((BoundReturnStatement)statement),
            BoundKind.TryStatement => ExpandTryStatement((BoundTryStatement)statement),
            BoundKind.BreakStatement => ExpandBreakStatement((BoundBreakStatement)statement),
            BoundKind.ContinueStatement => ExpandContinueStatement((BoundContinueStatement)statement),
            BoundKind.ErrorStatement => ExpandErrorStatement((BoundErrorStatement)statement),
            BoundKind.LocalFunctionStatement => ExpandLocalFunctionStatement((BoundLocalFunctionStatement)statement),
            BoundKind.SequencePoint => ExpandSequencePoint((BoundSequencePoint)statement),
            BoundKind.SequencePointWithLocation => ExpandSequencePointWithLocation((BoundSequencePointWithLocation)statement),
            BoundKind.SwitchStatement => ExpandSwitchStatement((BoundSwitchStatement)statement),
            BoundKind.InlineILStatement => ExpandInlineILStatement((BoundInlineILStatement)statement),
            BoundKind.SwitchDispatch => ExpandSwitchDispatch((BoundSwitchDispatch)statement),
            _ => throw ExceptionUtilities.UnexpectedValue(statement.kind),
        };
    }

    private protected virtual List<BoundStatement> ExpandErrorStatement(BoundErrorStatement statement) {
        // Even though there is potential for expanding the childBoundNodes, it will be an error anyways so why bother
        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandSwitchStatement(BoundSwitchStatement statement) {
        var sections = new List<BoundSwitchSection>();

        foreach (var section in statement.switchSections) {
            var statements = new List<BoundStatement>();

            foreach (var childStatement in section.statements)
                statements.AddRange(ExpandStatement(childStatement));

            sections.Add(section.Update(section.locals, section.switchLabels, section.statements));
        }

        var outerStatements = new List<BoundStatement>();

        outerStatements.AddRange(ExpandExpression(statement.expression, out var replacementExpression));
        outerStatements.Add(statement.Update(
            replacementExpression,
            statement.innerLocals,
            statement.innerLocalFunctions,
            sections.ToImmutableArray(),
            statement.reachabilityDecisionDag,
            statement.defaultLabel,
            statement.breakLabel
        ));

        return outerStatements;
    }

    private protected virtual List<BoundStatement> ExpandNopStatement(BoundNopStatement statement) {
        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandSwitchDispatch(BoundSwitchDispatch statement) {
        var statements = ExpandExpression(statement.expression, out var replacementExpression);
        statements.Add(statement.Update(replacementExpression, statement.cases, statement.defaultLabel));
        return statements;
    }

    private protected virtual List<BoundStatement> ExpandInlineILStatement(BoundInlineILStatement statement) {
        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandSequencePoint(BoundSequencePoint statement) {
        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandSequencePointWithLocation(
        BoundSequencePointWithLocation statement) {
        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandLocalFunctionStatement(BoundLocalFunctionStatement statement) {
        var newBody = (BoundBlockStatement)ExpandBlockStatement(statement.body)[0];
        return [new BoundLocalFunctionStatement(statement.syntax, statement.symbol, newBody)];
    }

    private protected virtual List<BoundStatement> ExpandBlockStatement(BoundBlockStatement statement) {
        var statements = new List<BoundStatement>();

        foreach (var childStatement in statement.statements)
            statements.AddRange(ExpandStatement(childStatement));

        return [new BoundBlockStatement(
            statement.syntax,
            statements.ToImmutableArray(),
            statement.locals,
            statement.localFunctions
        )];
    }

    private protected virtual List<BoundStatement> ExpandLocalDeclarationStatement(
        BoundLocalDeclarationStatement statement) {
        var statements = ExpandExpression(statement.declaration.initializer, out var replacement);
        var syntax = statement.syntax;

        if (statements.Count > 0 || statement.declaration.initializer != replacement) {
            statements.Add(new BoundLocalDeclarationStatement(
                syntax,
                new BoundDataContainerDeclaration(syntax, statement.declaration.dataContainer, replacement)
            ));

            return statements;
        }

        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandIfStatement(BoundIfStatement statement) {
        var statements = ExpandExpression(statement.condition, out var conditionReplacement);
        var syntax = statement.syntax;

        statements.Add(
            new BoundIfStatement(
                syntax,
                conditionReplacement,
                Simplify(syntax, ExpandStatement(statement.consequence)),
                statement.alternative is not null ? Simplify(syntax, ExpandStatement(statement.alternative)) : null
            )
        );

        return statements;
    }

    private protected virtual List<BoundStatement> ExpandWhileStatement(BoundWhileStatement statement) {
        var statements = ExpandExpression(statement.condition, out var conditionReplacement);
        var syntax = statement.syntax;

        statements.Add(
            new BoundWhileStatement(
                syntax,
                statement.locals,
                conditionReplacement,
                Simplify(syntax, ExpandStatement(statement.body)),
                statement.breakLabel,
                statement.continueLabel
            )
        );

        return statements;
    }

    private protected virtual List<BoundStatement> ExpandForStatement(BoundForStatement statement) {
        // For loops have to be expanded after they have been lowered
        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandForEachStatement(BoundForEachStatement statement) {
        // For each loops have to be expanded after they have been lowered
        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandNullBindingStatement(BoundNullBindingStatement statement) {
        // Null-binding contract statements have to be expanded after they have been lowered
        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandExpressionStatement(BoundExpressionStatement statement) {
        var statements = ExpandExpression(statement.expression, out var replacement);

        if (statements.Count != 0 || statement.expression != replacement) {
            if (replacement is not null)
                statements.Add(new BoundExpressionStatement(statement.syntax, replacement));

            return statements;
        }

        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandLabelStatement(BoundLabelStatement statement) {
        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandGotoStatement(BoundGotoStatement statement) {
        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandConditionalGotoStatement(BoundConditionalGotoStatement statement) {
        var statements = ExpandExpression(statement.condition, out var conditionReplacement);

        if (statements.Count > 0 || statement.condition != conditionReplacement) {
            statements.Add(statement.Update(statement.label, conditionReplacement, statement.jumpIfTrue));
            return statements;
        }

        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandDoWhileStatement(BoundDoWhileStatement statement) {
        var statements = ExpandExpression(statement.condition, out var conditionReplacement);
        var syntax = statement.syntax;

        statements.Add(
            new BoundDoWhileStatement(
                syntax,
                statement.locals,
                conditionReplacement,
                Simplify(syntax, ExpandStatement(statement.body)),
                statement.breakLabel,
                statement.continueLabel
            )
        );

        return statements;
    }

    private protected virtual List<BoundStatement> ExpandReturnStatement(BoundReturnStatement statement) {
        if (statement.expression is null)
            return [statement];

        var statements = ExpandExpression(statement.expression, out var replacement);

        if (statements.Count != 0 || statement.expression != replacement) {
            statements.Add(new BoundReturnStatement(statement.syntax, statement.refKind, replacement));
            return statements;
        }

        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandTryStatement(BoundTryStatement statement) {
        var syntax = statement.syntax;

        return [
            new BoundTryStatement(
                syntax,
                Simplify(syntax, ExpandStatement(statement.body)) as BoundBlockStatement,
                statement.catchBody is not null ?
                    Simplify(syntax, ExpandStatement(statement.catchBody)) as BoundBlockStatement
                    : null,
                statement.finallyBody is not null ?
                    Simplify(syntax, ExpandStatement(statement.finallyBody)) as BoundBlockStatement
                    : null
            )
        ];
    }

    private protected virtual List<BoundStatement> ExpandBreakStatement(BoundBreakStatement statement) {
        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandContinueStatement(BoundContinueStatement statement) {
        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandExpression(
        BoundExpression expression,
        out BoundExpression replacement,
        UseKind useKind = UseKind.Value) {
        if (expression.constantValue is not null) {
            replacement = expression;
            return [];
        }

        return expression.kind switch {
            BoundKind.LiteralExpression => ExpandLiteralExpression((BoundLiteralExpression)expression, out replacement, useKind),
            BoundKind.DefaultExpression => ExpandDefaultExpression((BoundDefaultExpression)expression, out replacement, useKind),
            BoundKind.InitializerList => ExpandInitializerList((BoundInitializerList)expression, out replacement, useKind),
            BoundKind.InitializerDictionary => ExpandInitializerDictionary((BoundInitializerDictionary)expression, out replacement, useKind),
            BoundKind.DataContainerExpression => ExpandDataContainerExpression((BoundDataContainerExpression)expression, out replacement, useKind),
            BoundKind.AssignmentOperator => ExpandAssignmentOperator((BoundAssignmentOperator)expression, out replacement, useKind),
            BoundKind.UnaryOperator => ExpandUnaryOperator((BoundUnaryOperator)expression, out replacement, useKind),
            BoundKind.IncrementOperator => ExpandIncrementOperator((BoundIncrementOperator)expression, out replacement, useKind),
            BoundKind.BinaryOperator => ExpandBinaryOperator((BoundBinaryOperator)expression, out replacement, useKind),
            BoundKind.AsOperator => ExpandAsOperator((BoundAsOperator)expression, out replacement, useKind),
            BoundKind.IsOperator => ExpandIsOperator((BoundIsOperator)expression, out replacement, useKind),
            BoundKind.NullCoalescingOperator => ExpandNullCoalescingOperator((BoundNullCoalescingOperator)expression, out replacement, useKind),
            BoundKind.NullCoalescingAssignmentOperator => ExpandNullCoalescingAssignmentOperator((BoundNullCoalescingAssignmentOperator)expression, out replacement, useKind),
            BoundKind.NullAssertOperator => ExpandNullAssertOperator((BoundNullAssertOperator)expression, out replacement, useKind),
            BoundKind.NullErasureOperator => ExpandNullErasureOperator((BoundNullErasureOperator)expression, out replacement, useKind),
            BoundKind.AddressOfOperator => ExpandAddressOfOperator((BoundAddressOfOperator)expression, out replacement, useKind),
            BoundKind.PointerIndirectionOperator => ExpandPointerIndirectionOperator((BoundPointerIndirectionOperator)expression, out replacement, useKind),
            BoundKind.ErrorExpression => ExpandErrorExpression((BoundErrorExpression)expression, out replacement, useKind),
            BoundKind.CallExpression => ExpandCallExpression((BoundCallExpression)expression, out replacement, useKind),
            BoundKind.CastExpression => ExpandCastExpression((BoundCastExpression)expression, out replacement, useKind),
            BoundKind.ArrayAccessExpression => ExpandArrayAccessExpression((BoundArrayAccessExpression)expression, out replacement, useKind),
            BoundKind.IndexerAccessExpression => ExpandIndexerAccessExpression((BoundIndexerAccessExpression)expression, out replacement, useKind),
            BoundKind.PointerIndexAccessExpression => ExpandPointerIndexAccessExpression((BoundPointerIndexAccessExpression)expression, out replacement, useKind),
            BoundKind.CompoundAssignmentOperator => ExpandCompoundAssignmentOperator((BoundCompoundAssignmentOperator)expression, out replacement, useKind),
            BoundKind.ReferenceExpression => ExpandReferenceExpression((BoundReferenceExpression)expression, out replacement, useKind),
            BoundKind.TypeOfExpression => ExpandTypeOfExpression((BoundTypeOfExpression)expression, out replacement, useKind),
            BoundKind.ConditionalOperator => ExpandConditionalOperator((BoundConditionalOperator)expression, out replacement, useKind),
            BoundKind.ObjectCreationExpression => ExpandObjectCreationExpression((BoundObjectCreationExpression)expression, out replacement, useKind),
            BoundKind.ArrayCreationExpression => ExpandArrayCreationExpression((BoundArrayCreationExpression)expression, out replacement, useKind),
            BoundKind.FieldAccessExpression => ExpandFieldAccessExpression((BoundFieldAccessExpression)expression, out replacement, useKind),
            BoundKind.ConditionalAccessExpression => ExpandConditionalAccessExpression((BoundConditionalAccessExpression)expression, out replacement, useKind),
            BoundKind.ThisExpression => ExpandThisExpression((BoundThisExpression)expression, out replacement, useKind),
            BoundKind.BaseExpression => ExpandBaseExpression((BoundBaseExpression)expression, out replacement, useKind),
            BoundKind.ThrowExpression => ExpandThrowExpression((BoundThrowExpression)expression, out replacement, useKind),
            BoundKind.TypeExpression => ExpandTypeExpression((BoundTypeExpression)expression, out replacement, useKind),
            BoundKind.NamespaceExpression => ExpandNamespaceExpression((BoundNamespaceExpression)expression, out replacement, useKind),
            BoundKind.ParameterExpression => ExpandParameterExpression((BoundParameterExpression)expression, out replacement, useKind),
            BoundKind.MethodGroup => ExpandMethodGroup((BoundMethodGroup)expression, out replacement, useKind),
            BoundKind.FunctionPointerLoad => ExpandFunctionPointerLoad((BoundFunctionPointerLoad)expression, out replacement, useKind),
            BoundKind.FunctionPointerCallExpression => ExpandFunctionPointerCallExpression((BoundFunctionPointerCallExpression)expression, out replacement, useKind),
            BoundKind.UnconvertedNullptrExpression => ExpandUnconvertedNullptrExpression((BoundUnconvertedNullptrExpression)expression, out replacement, useKind),
            BoundKind.CompileTimeExpression => ExpandCompileTimeExpression((BoundCompileTimeExpression)expression, out replacement, useKind),
            BoundKind.SizeOfOperator => ExpandSizeOfOperator((BoundSizeOfOperator)expression, out replacement, useKind),
            BoundKind.CascadeListExpression => ExpandCascadeListExpression((BoundCascadeListExpression)expression, out replacement, useKind),
            BoundKind.StackSlotExpression => ExpandStackSlotExpression((BoundStackSlotExpression)expression, out replacement, useKind),
            BoundKind.FieldSlotExpression => ExpandFieldSlotExpression((BoundFieldSlotExpression)expression, out replacement, useKind),
            BoundKind.StackAllocExpression => ExpandStackAllocExpression((BoundStackAllocExpression)expression, out replacement, useKind),
            BoundKind.ConvertedStackAllocExpression => ExpandConvertedStackAllocExpression((BoundConvertedStackAllocExpression)expression, out replacement, useKind),
            BoundKind.InterpolatedStringExpression => ExpandInterpolatedStringExpression((BoundInterpolatedStringExpression)expression, out replacement, useKind),
            BoundKind.FunctionLoad => ExpandFunctionLoad((BoundFunctionLoad)expression, out replacement, useKind),
            _ => throw ExceptionUtilities.UnexpectedValue(expression.kind),
        };
    }

    private protected virtual List<BoundStatement> ExpandFunctionLoad(
        BoundFunctionLoad expression,
        out BoundExpression replacement,
        UseKind useKind) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandInterpolatedStringExpression(
        BoundInterpolatedStringExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        List<BoundStatement> statements = [];
        var newContents = ArrayBuilder<BoundExpression>.GetInstance();

        foreach (var content in expression.contents) {
            statements.AddRange(ExpandExpression(content, out var replacementContent));
            newContents.Add(replacementContent);
        }

        replacement = expression.Update(newContents.ToImmutableAndFree(), expression.constantValue, expression.type);
        return statements;
    }

    private protected virtual List<BoundStatement> ExpandCascadeListExpression(
        BoundCascadeListExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        List<BoundStatement> statements;
        BoundExpression newReceiver = null;

        if (expression.receiver is not null)
            statements = ExpandExpression(expression.receiver, out newReceiver);
        else
            statements = [];

        statements.AddRange(ExpandExpressionList(expression.cascades, out var newCascades));

        replacement = expression.Update(
            newReceiver,
            newCascades,
            expression.conditionals,
            expression.type
        );

        return statements;
    }

    private protected virtual List<BoundStatement> ExpandSizeOfOperator(
        BoundSizeOfOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandStackSlotExpression(
        BoundStackSlotExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandFieldSlotExpression(
        BoundFieldSlotExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandStackAllocExpression(
        BoundStackAllocExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandConvertedStackAllocExpression(
        BoundConvertedStackAllocExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandMethodGroup(
        BoundMethodGroup expression,
        out BoundExpression replacement,
        UseKind useKind) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandCompileTimeExpression(
        BoundCompileTimeExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandUnconvertedNullptrExpression(
        BoundUnconvertedNullptrExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandFunctionPointerLoad(
        BoundFunctionPointerLoad expression,
        out BoundExpression replacement,
        UseKind useKind) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandFunctionPointerCallExpression(
        BoundFunctionPointerCallExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        List<BoundStatement> statements;
        BoundExpression newInvokedExpression = null;

        if (expression.invokedExpression is not null)
            statements = ExpandExpression(expression.invokedExpression, out newInvokedExpression);
        else
            statements = [];

        statements.AddRange(ExpandExpressionList(expression.arguments, out var newArguments));

        replacement = expression.Update(
            newInvokedExpression,
            newArguments,
            expression.argumentRefKindsOpt,
            expression.resultKind,
            expression.type
        );

        return statements;
    }

    private protected virtual List<BoundStatement> ExpandParameterExpression(
        BoundParameterExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandTypeExpression(
        BoundTypeExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandNamespaceExpression(
        BoundNamespaceExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandThisExpression(
        BoundThisExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandBaseExpression(
        BoundBaseExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandThrowExpression(
        BoundThrowExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = ExpandExpression(expression.expression, out var newExpression);

        if (statements.Count != 0 || newExpression != expression.expression) {
            replacement = expression.Update(
                newExpression,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandBinaryOperator(
        BoundBinaryOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = ExpandExpression(expression.left, out var newLeft);
        statements.AddRange(ExpandExpression(expression.right, out var newRight));

        if (statements.Count != 0 || expression.left != newLeft || expression.right != newRight) {
            replacement = expression.Update(
                newLeft,
                newRight,
                expression.operatorKind,
                expression.method,
                expression.constantValue,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandAsOperator(
        BoundAsOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = ExpandExpression(expression.left, out var newLeft);
        statements.AddRange(ExpandExpression(expression.right, out var newRight));

        if (statements.Count != 0 || expression.left != newLeft || expression.right != newRight) {
            replacement = expression.Update(
                newLeft,
                newRight,
                expression.operandPlaceholder,
                expression.operandConversion,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandIsOperator(
        BoundIsOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = ExpandExpression(expression.left, out var newLeft);
        var newRight = expression.right;

        if (!newRight.IsLiteralNull())
            statements.AddRange(ExpandExpression(expression.right, out newRight));

        if (statements.Count != 0 || expression.left != newLeft || expression.right != newRight) {
            replacement = expression.Update(
                newLeft,
                newRight,
                expression.isNot,
                expression.constantValue,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandNullCoalescingOperator(
        BoundNullCoalescingOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = ExpandExpression(expression.left, out var newLeft);
        statements.AddRange(ExpandExpression(expression.right, out var newRight));

        if (statements.Count != 0 || expression.left != newLeft || expression.right != newRight) {
            replacement = expression.Update(
                newLeft,
                newRight,
                expression.isPropagation,
                expression.constantValue,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandNullCoalescingAssignmentOperator(
        BoundNullCoalescingAssignmentOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = ExpandExpression(expression.left, out var newLeft, UseKind.Writable);
        statements.AddRange(ExpandExpression(expression.right, out var newRight));

        if (statements.Count != 0 || expression.left != newLeft || expression.right != newRight) {
            replacement = expression.Update(newLeft, newRight, expression.isPropagation, expression.type);
            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandInitializerList(
        BoundInitializerList expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = new List<BoundStatement>();
        var replacementItems = ArrayBuilder<BoundExpression>.GetInstance();

        foreach (var item in expression.items) {
            statements.AddRange(ExpandExpression(item, out var itemReplacement));
            replacementItems.Add(itemReplacement);
        }

        replacement = expression.Update(replacementItems.ToImmutableAndFree(), expression.type);
        return statements;
    }

    private protected virtual List<BoundStatement> ExpandInitializerDictionary(
        BoundInitializerDictionary expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = new List<BoundStatement>();
        var replacementItems = ArrayBuilder<(BoundExpression, BoundExpression)>.GetInstance();

        foreach (var item in expression.items) {
            statements.AddRange(ExpandExpression(item.Item1, out var item1Replacement));
            statements.AddRange(ExpandExpression(item.Item2, out var item2Replacement));
            replacementItems.Add((item1Replacement, item2Replacement));
        }

        replacement = expression.Update(replacementItems.ToImmutableAndFree(), expression.type);
        return statements;
    }

    private protected virtual List<BoundStatement> ExpandLiteralExpression(
        BoundLiteralExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandDefaultExpression(
        BoundDefaultExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandDataContainerExpression(
        BoundDataContainerExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandAssignmentOperator(
        BoundAssignmentOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = ExpandExpression(expression.left, out var newLeft, UseKind.Writable);
        statements.AddRange(ExpandExpression(expression.right, out var newRight));

        if (statements.Count != 0 || expression.left != newLeft || expression.right != newRight) {
            replacement = expression.Update(newLeft, newRight, expression.isRef, expression.type);
            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandUnaryOperator(
        BoundUnaryOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = ExpandExpression(expression.operand, out var newOperand);

        if (statements.Count != 0 || expression.operand != newOperand) {
            replacement = expression.Update(
                newOperand,
                expression.operatorKind,
                expression.method,
                expression.constantValue,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandIncrementOperator(
        BoundIncrementOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = ExpandExpression(expression.operand, out var newOperand, UseKind.Writable);

        if (statements.Count != 0 || expression.operand != newOperand) {
            replacement = expression.Update(
                expression.operatorKind,
                newOperand,
                expression.method,
                expression.operandPlaceholder,
                expression.operandConversion,
                expression.resultPlaceholder,
                expression.resultConversion,
                expression.resultKind,
                expression.originalUserDefinedOperators,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandNullAssertOperator(
        BoundNullAssertOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = ExpandExpression(expression.operand, out var newOperand);

        if (statements.Count != 0 || expression.operand != newOperand) {
            replacement = expression.Update(
                newOperand,
                expression.throwIfNull,
                expression.constantValue,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandNullErasureOperator(
        BoundNullErasureOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = ExpandExpression(expression.operand, out var newOperand);

        if (statements.Count != 0 || expression.operand != newOperand) {
            replacement = expression.Update(
                newOperand,
                expression.defaultValue,
                expression.constantValue,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandAddressOfOperator(
        BoundAddressOfOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = ExpandExpression(expression.operand, out var newOperand);

        if (statements.Count != 0 || expression.operand != newOperand) {
            replacement = expression.Update(
                newOperand,
                expression.isLoweredFixedField,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandPointerIndirectionOperator(
        BoundPointerIndirectionOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = ExpandExpression(expression.operand, out var newOperand);

        if (statements.Count != 0 || expression.operand != newOperand) {
            replacement = expression.Update(
                newOperand,
                expression.refersToLocation,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandErrorExpression(
        BoundErrorExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandCallExpression(
        BoundCallExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        List<BoundStatement> statements;
        BoundExpression newReceiver = null;

        if (expression.receiver is not null)
            statements = ExpandExpression(expression.receiver, out newReceiver);
        else
            statements = [];

        statements.AddRange(ExpandExpressionList(expression.arguments, out var newArguments));

        replacement = expression.Update(
            newReceiver,
            expression.method,
            newArguments,
            expression.argumentRefKinds,
            expression.defaultArguments,
            expression.resultKind,
            expression.type
        );

        return statements;
    }

    private protected List<BoundStatement> ExpandExpressionList(
        ImmutableArray<BoundExpression> expressions,
        out ImmutableArray<BoundExpression> replacement,
        UseKind useKind = UseKind.Value) {
        var statements = new List<BoundStatement>();
        var replacementExpressions = ArrayBuilder<BoundExpression>.GetInstance();

        foreach (var expression in expressions) {
            statements.AddRange(ExpandExpression(expression, out var newExpression));
            replacementExpressions.Add(newExpression);
        }

        replacement = replacementExpressions.ToImmutableAndFree();
        return statements;
    }

    private protected virtual List<BoundStatement> ExpandCastExpression(
        BoundCastExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = ExpandExpression(expression.operand, out var newOperand);

        if (statements.Count != 0 || expression.operand != newOperand) {
            replacement = expression.Update(
                newOperand,
                expression.conversion,
                expression.constantValue,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandArrayAccessExpression(
        BoundArrayAccessExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = ExpandExpression(expression.receiver, out var newOperand);
        statements.AddRange(ExpandExpression(expression.index, out var newIndex));

        if (statements.Count != 0 || expression.receiver != newOperand || expression.index != newIndex) {
            replacement = expression.Update(newOperand, newIndex, expression.constantValue, expression.type);
            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandIndexerAccessExpression(
        BoundIndexerAccessExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = ExpandExpression(expression.receiver, out var newOperand);
        statements.AddRange(ExpandExpression(expression.index, out var newIndex));

        if (statements.Count != 0 || expression.receiver != newOperand || expression.index != newIndex) {
            replacement = expression.Update(
                newOperand,
                newIndex,
                expression.method,
                expression.constantValue,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandPointerIndexAccessExpression(
        BoundPointerIndexAccessExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = ExpandExpression(expression.receiver, out var newOperand);
        statements.AddRange(ExpandExpression(expression.index, out var newIndex));

        if (statements.Count != 0 || expression.receiver != newOperand || expression.index != newIndex) {
            replacement = expression.Update(
                newOperand,
                newIndex,
                expression.refersToLocation,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandCompoundAssignmentOperator(
        BoundCompoundAssignmentOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = ExpandExpression(expression.left, out var newLeft, UseKind.Writable);
        statements.AddRange(ExpandExpression(expression.right, out var newRight));

        if (statements.Count != 0 || expression.left != newLeft || expression.right != newRight) {
            replacement = expression.Update(
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
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandReferenceExpression(
        BoundReferenceExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandTypeOfExpression(
        BoundTypeOfExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandConditionalOperator(
        BoundConditionalOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = ExpandExpression(expression.condition, out var newCondition);
        statements.AddRange(ExpandExpression(expression.trueExpression, out var newTrueExpression));
        statements.AddRange(ExpandExpression(expression.falseExpression, out var newFalseExpression));

        if (statements.Count != 0 || expression.condition != newCondition ||
            expression.trueExpression != newTrueExpression || expression.falseExpression != newFalseExpression) {
            replacement = expression.Update(
                newCondition,
                expression.isRef,
                newTrueExpression,
                newFalseExpression,
                expression.constantValue,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandObjectCreationExpression(
        BoundObjectCreationExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = ExpandExpressionList(expression.arguments, out var newArguments);

        replacement = expression.Update(
            expression.constructor,
            newArguments,
            expression.argumentRefKinds,
            expression.argsToParams,
            expression.defaultArguments,
            expression.wasTargetTyped,
            expression.type
        );

        return statements;
    }

    private protected virtual List<BoundStatement> ExpandArrayCreationExpression(
        BoundArrayCreationExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        if (expression.initializer is null) {
            replacement = expression;
            return [];
        }

        var statements = ExpandExpression(expression.initializer, out var newInitializer);
        replacement = expression.Update(expression.sizes, (BoundInitializerList)newInitializer, expression.type);
        return statements;
    }

    private protected virtual List<BoundStatement> ExpandFieldAccessExpression(
        BoundFieldAccessExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        if (!expression.field.isStatic) {
            // Structs are special case because they have limited expansion ability (they are non-nullable)
            // And because they are passed by value so in something like `a.b.c = x`, we can't hoist anything
            var isTrueStructReceiver = expression.receiver.Type().IsStructType() &&
                !(expression.receiver is BoundCallExpression c && c.receiver.StrippedType().IsStructType());

            var statements = ExpandExpression(
                expression.receiver,
                out var newReceiver,
                isTrueStructReceiver ? UseKind.Writable : UseKind.StableValue
            );

            if (statements.Count != 0 || expression.receiver != newReceiver) {
                replacement = expression.Update(
                    newReceiver,
                    expression.field,
                    expression.constantValue,
                    expression.type
                );

                return statements;
            }
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandConditionalAccessExpression(
        BoundConditionalAccessExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = ExpandExpression(expression.receiver, out var newReceiver, UseKind.StableValue);
        statements.AddRange(ExpandExpression(expression.accessExpression, out var newAccess));

        if (statements.Count != 0 || expression.receiver != newReceiver || expression.accessExpression != newAccess) {
            replacement = expression.Update(newReceiver, newAccess, expression.type);
            return statements;
        }

        replacement = expression;
        return [];
    }
}

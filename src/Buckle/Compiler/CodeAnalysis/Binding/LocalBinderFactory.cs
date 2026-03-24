using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class LocalBinderFactory : SyntaxWalker {
    private readonly Dictionary<SyntaxNode, Binder> _map;
    private readonly SyntaxNode _root;
    private Symbol _containingMember;
    private Binder _enclosing;

    private LocalBinderFactory(Symbol containingMemberOrLambda, SyntaxNode root, Binder enclosing) {
        _map = new Dictionary<SyntaxNode, Binder>(ReferenceEqualityComparer.Instance);
        _containingMember = containingMemberOrLambda;
        _enclosing = enclosing;
        _root = root;
    }

    internal override void VisitCompilationUnit(CompilationUnitSyntax node) {
        foreach (var member in node.members) {
            if (member.kind == SyntaxKind.GlobalStatement)
                Visit(member);
        }
    }

    internal override void DefaultVisit(SyntaxNode node) {
        base.DefaultVisit(node);
    }

    internal static Dictionary<SyntaxNode, Binder> BuildMap(
        Symbol containingMember,
        SyntaxNode syntax,
        Binder enclosing,
        Action<Binder, SyntaxNode> binderUpdatedHandler = null) {
        var builder = new LocalBinderFactory(containingMember, syntax, enclosing);

        if (syntax is ExpressionSyntax expressionSyntax) {
            enclosing = new ExpressionVariableBinder(syntax, enclosing);

            if (binderUpdatedHandler is not null)
                binderUpdatedHandler(enclosing, syntax);

            builder.AddToMap(syntax, enclosing);
            builder.Visit(expressionSyntax, enclosing);
        } else if (syntax.kind != SyntaxKind.BlockStatement && syntax is StatementSyntax statement) {
            enclosing = builder.GetBinderForPossibleEmbeddedStatement(
                statement,
                enclosing,
                out var embeddedScopeDesignator
            );

            if (binderUpdatedHandler is not null)
                binderUpdatedHandler(enclosing, embeddedScopeDesignator);

            if (embeddedScopeDesignator is not null)
                builder.AddToMap(embeddedScopeDesignator, enclosing);

            builder.Visit(statement, enclosing);
        } else {
            if (binderUpdatedHandler is not null)
                binderUpdatedHandler(enclosing, null);

            builder.Visit((BelteSyntaxNode)syntax, enclosing);
        }

        return builder._map;
    }

    private void Visit(BelteSyntaxNode syntax, Binder enclosing) {
        if (_enclosing == enclosing) {
            Visit(syntax);
        } else {
            var oldEnclosing = _enclosing;
            _enclosing = enclosing;
            Visit(syntax);
            _enclosing = oldEnclosing;
        }
    }

    private void VisitRankSpecifiers(TypeSyntax type, Binder enclosing) {
        type.VisitRankSpecifiers((rankSpecifier, args) => {
            args.localBinderFactory.Visit(rankSpecifier.size, args.binder);
        }, (localBinderFactory: this, binder: enclosing));
    }

    private void AddToMap(SyntaxNode node, Binder binder) {
        _map[node] = binder;
    }

    private Binder GetBinderForPossibleEmbeddedStatement(
        StatementSyntax statement,
        Binder enclosing,
        out BelteSyntaxNode embeddedScopeDesignator) {
        switch (statement.kind) {
            case SyntaxKind.LocalDeclarationStatement:
            case SyntaxKind.LocalFunctionStatement:
            case SyntaxKind.ExpressionStatement:
            case SyntaxKind.IfStatement:
            case SyntaxKind.ReturnStatement:
                embeddedScopeDesignator = statement;
                return new EmbeddedStatementBinder(enclosing, statement);
            default:
                embeddedScopeDesignator = null;
                return enclosing;
        }
    }

    private Binder GetBinderForPossibleEmbeddedStatement(StatementSyntax statement, Binder enclosing) {
        enclosing = GetBinderForPossibleEmbeddedStatement(statement, enclosing, out var embeddedScopeDesignator);

        if (embeddedScopeDesignator is not null)
            AddToMap(embeddedScopeDesignator, enclosing);

        return enclosing;
    }

    internal override void VisitMethodDeclaration(MethodDeclarationSyntax node) {
        Visit(node.body);
    }

    internal override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node) {
        var enclosing = new ExpressionVariableBinder(node, _enclosing);
        AddToMap(node, enclosing);

        Visit(node.constructorInitializer, enclosing);
        Visit(node.body, enclosing);
    }

    internal override void VisitClassDeclaration(ClassDeclarationSyntax node) {
        VisitTypeDeclaration(node);
    }

    private void VisitTypeDeclaration(TypeDeclarationSyntax node) { }

    internal override void VisitOperatorDeclaration(OperatorDeclarationSyntax node) {
        Visit(node.body);
    }

    internal override void VisitCallExpression(CallExpressionSyntax node) {
        if (ReceiverIsInvocation(node, out var nested)) {
            var invocations = ArrayBuilder<CallExpressionSyntax>.GetInstance();

            invocations.Push(node);

            node = nested;

            while (ReceiverIsInvocation(node, out nested)) {
                invocations.Push(node);
                node = nested;
            }

            Visit(node.expression);

            do {
                Visit(node.argumentList);
            } while (invocations.TryPop(out node!));

            invocations.Free();
        } else {
            Visit(node.expression);
            Visit(node.argumentList);
        }

        return;

        static bool ReceiverIsInvocation(CallExpressionSyntax node, out CallExpressionSyntax nested) {
            if (node.expression is MemberAccessExpressionSyntax { expression: CallExpressionSyntax receiver }) {
                nested = receiver;
                return true;
            }

            nested = null;
            return false;
        }
    }

    internal override void VisitAttribute(AttributeSyntax node) {
        var attrBinder = new ExpressionVariableBinder(node, _enclosing);
        AddToMap(node, attrBinder);

        if (node.argumentList?.arguments?.Count > 0) {
            foreach (ArgumentSyntax argument in node.argumentList.arguments)
                Visit(argument.expression, attrBinder);
        }
    }

    internal override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node) {
        var oldMethod = _containingMember;
        var binder = _enclosing;
        var match = FindLocalFunction(node, _enclosing);

        if (match is not null) {
            _containingMember = match;

            binder = match.isTemplateMethod
                ? new WithMethodTemplateParametersBinder(match, _enclosing)
                : _enclosing;

            binder = new InMethodBinder(match, binder);
        }

        var blockBody = node.body;

        if (blockBody is not null)
            Visit(blockBody, binder);

        _containingMember = oldMethod;
    }

    private static LocalFunctionSymbol FindLocalFunction(LocalFunctionStatementSyntax node, Binder enclosing) {
        LocalFunctionSymbol match = null;

        var possibleScopeBinder = enclosing;

        while (possibleScopeBinder is not null && !possibleScopeBinder.isLocalFunctionsScopeBinder)
            possibleScopeBinder = possibleScopeBinder.next;

        if (possibleScopeBinder is not null) {
            foreach (var candidate in possibleScopeBinder.localFunctions) {
                if (candidate.location.Equals(node.identifier.location))
                    match = candidate;
            }
        }

        return match;
    }

    internal override void VisitNameOfExpression(NameOfExpressionSyntax node) {
        var nameOfBinder = new NameofBinder(node.name, _enclosing, null, null);
        AddToMap(node, nameOfBinder);
        Visit(node.name, nameOfBinder);
    }

    internal override void VisitEqualsValueClause(EqualsValueClauseSyntax node) {
        var valueBinder = new ExpressionVariableBinder(node, _enclosing);
        AddToMap(node, valueBinder);
        Visit(node.value, valueBinder);
    }

    internal override void VisitConstructorInitializer(ConstructorInitializerSyntax node) {
        var binder = _enclosing.WithAdditionalFlags(BinderFlags.ConstructorInitializer);
        AddToMap(node, binder);
        VisitConstructorInitializerArgumentList(node, node.argumentList, binder);
    }

    private void VisitConstructorInitializerArgumentList(
        BelteSyntaxNode node,
        ArgumentListSyntax argumentList,
        Binder binder) {
        if (argumentList is not null) {
            if (_root == node) {
                binder = new ExpressionVariableBinder(argumentList, binder);
                AddToMap(argumentList, binder);
            }

            Visit(argumentList, binder);
        }
    }

    internal override void VisitGlobalStatement(GlobalStatementSyntax node) {
        Visit(node.statement);
    }

    internal override void VisitBlockStatement(BlockStatementSyntax node) {
        var blockBinder = new BlockBinder(_enclosing, node);
        AddToMap(node, blockBinder);

        foreach (var statement in node.statements)
            Visit(statement, blockBinder);
    }

    internal override void VisitWhileStatement(WhileStatementSyntax node) {
        var whileBinder = new WhileBinder(_enclosing, node);
        AddToMap(node, whileBinder);

        Visit(node.condition, whileBinder);
        VisitPossibleEmbeddedStatement(node.body, whileBinder);
    }

    internal override void VisitDoWhileStatement(DoWhileStatementSyntax node) {
        var whileBinder = new WhileBinder(_enclosing, node);
        AddToMap(node, whileBinder);

        Visit(node.condition, whileBinder);
        VisitPossibleEmbeddedStatement(node.body, whileBinder);
    }

    internal override void VisitForStatement(ForStatementSyntax node) {
        Binder binder = new ForLoopBinder(_enclosing, node);
        AddToMap(node, binder);

        VisitPossibleEmbeddedStatement(node.initializer, binder);

        var condition = node.condition;

        if (condition is not null) {
            binder = new ExpressionVariableBinder(condition, binder);
            AddToMap(condition, binder);
            Visit(condition, binder);
        }

        var step = node.step;

        if (step is not null) {
            var incrementorsBinder = new ExpressionVariableBinder(step, binder);
            AddToMap(step, incrementorsBinder);
            Visit(step, incrementorsBinder);
        }

        VisitPossibleEmbeddedStatement(node.body, binder);
    }

    internal override void VisitIfStatement(IfStatementSyntax node) {
        var enclosing = _enclosing;

        while (true) {
            Visit(node.condition, enclosing);
            VisitPossibleEmbeddedStatement(node.then, enclosing);

            if (node.elseClause is null)
                break;

            var elseStatementSyntax = node.elseClause.body;

            if (elseStatementSyntax is IfStatementSyntax ifStatementSyntax) {
                node = ifStatementSyntax;
                enclosing = GetBinderForPossibleEmbeddedStatement(node, enclosing);
            } else {
                VisitPossibleEmbeddedStatement(elseStatementSyntax, enclosing);
                break;
            }
        }
    }

    internal override void VisitElseClause(ElseClauseSyntax node) {
        VisitPossibleEmbeddedStatement(node.body, _enclosing);
    }

    internal override void VisitTryStatement(TryStatementSyntax node) {
        if (node.catchClause is not null) {
            Visit(node.body, _enclosing.WithAdditionalFlags(BinderFlags.InTryBlockOfTryCatch));
            Visit(node.catchClause, _enclosing);
        } else {
            Visit(node.body, _enclosing);
        }

        if (node.finallyClause is not null)
            Visit(node.finallyClause, _enclosing);
    }

    internal override void VisitCatchClause(CatchClauseSyntax node) {
        var clauseBinder = new CatchClauseBinder(_enclosing, node);
        AddToMap(node, clauseBinder);
        Visit(node.body, clauseBinder);
    }

    internal override void VisitFinallyClause(FinallyClauseSyntax node) {
        var additionalFlags = BinderFlags.InFinallyBlock;

        if (_enclosing.flags.Includes(BinderFlags.InCatchBlock))
            additionalFlags |= BinderFlags.InNestedFinallyBlock;

        Visit(node.body, _enclosing.WithAdditionalFlags(additionalFlags));
    }

    internal override void VisitExpressionStatement(ExpressionStatementSyntax node) {
        Visit(node.expression, _enclosing);
    }

    internal override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node) {
        VisitRankSpecifiers(node.declaration.type, _enclosing);
        Visit(node.declaration);
    }

    internal override void VisitVariableDeclaration(VariableDeclarationSyntax node) {
        // TODO Looks like we're referencing difference source versions creating a conflict in how
        // TODO this specific problem is addressed
        // TODO Confirm this mismatch doesn't appear elsewhere
        // if (node.initializer is { } initializer) {
        //     var enclosing = _enclosing;

        //     if (node.parent is LocalDeclarationStatementSyntax { isConstExpr: true }) {
        //         enclosing = new LocalInProgressBinder(initializer, _enclosing);
        //         AddToMap(initializer, enclosing);
        //     }

        //     Visit(initializer.value, enclosing);
        // }
        Visit(node.initializer?.value);
    }

    internal override void VisitReturnStatement(ReturnStatementSyntax node) {
        if (node.expression is not null)
            Visit(node.expression, _enclosing);
    }

    internal override void VisitThrowExpression(ThrowExpressionSyntax node) {
        if (node.expression is not null)
            Visit(node.expression, _enclosing);
    }

    internal override void VisitBinaryExpression(BinaryExpressionSyntax node) {
        while (true) {
            Visit(node.right);

            if (node.left is not BinaryExpressionSyntax binOp) {
                Visit(node.left);
                break;
            }

            node = binOp;
        }
    }

    private void VisitPossibleEmbeddedStatement(StatementSyntax statement, Binder enclosing) {
        if (statement is not null) {
            enclosing = GetBinderForPossibleEmbeddedStatement(statement, enclosing);
            Visit(statement, enclosing);
        }
    }
}

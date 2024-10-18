using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

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
        Symbol containingMemberOrLambda,
        SyntaxNode syntax,
        Binder enclosing,
        Action<Binder, SyntaxNode> binderUpdatedHandler = null) {
        var builder = new LocalBinderFactory(containingMemberOrLambda, syntax, enclosing);

        if (syntax is ExpressionSyntax expressionSyntax) {
            enclosing = new ExpressionVariableBinder(syntax, enclosing);

            if (binderUpdatedHandler is not null)
                binderUpdatedHandler(enclosing, syntax);

            builder.AddToMap(syntax, enclosing);
            builder.Visit(expressionSyntax, enclosing);
        } else if (syntax.kind != SyntaxKind.BlockStatement && syntax is StatementSyntax statement) {
            enclosing = builder.GetBinderForPossibleEmbeddedStatement(statement, enclosing, out var embeddedScopeDesignator);

            if (binderUpdatedHandler is not null)
                binderUpdatedHandler(enclosing, embeddedScopeDesignator);

            if (embeddedScopeDesignator != null) {
                builder.AddToMap(embeddedScopeDesignator, enclosing);
            }

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
                // TODO Should throws be statements instead of expressions?
                // case SyntaxKind.ThrowStatement:
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

    // TODO Finish this class
}

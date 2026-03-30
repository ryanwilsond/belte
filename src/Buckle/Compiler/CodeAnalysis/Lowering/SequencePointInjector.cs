using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class SequencePointInjector : BoundTreeRewriter {
    internal static BoundBlockStatement Lower(BoundBlockStatement statement) {
        var lowerer = new SequencePointInjector();
        return (BoundBlockStatement)lowerer.Visit(statement);
    }

    internal override BoundNode VisitNopStatement(BoundNopStatement node) {
        return AddSequencePoint(node);
    }

    internal override BoundNode VisitBreakStatement(BoundBreakStatement node) {
        return AddSequencePoint(node);
    }

    internal override BoundNode VisitContinueStatement(BoundContinueStatement node) {
        return AddSequencePoint(node);
    }

    internal override BoundNode VisitGotoStatement(BoundGotoStatement node) {
        return AddSequencePoint(node);
    }

    internal override BoundNode VisitConditionalGotoStatement(BoundConditionalGotoStatement node) {
        return AddSequencePoint(node);
    }

    internal override BoundNode VisitLocalDeclarationStatement(BoundLocalDeclarationStatement node) {
        return AddSequencePoint(node);
    }

    internal override BoundNode VisitReturnStatement(BoundReturnStatement node) {
        if (node.syntax.kind != SyntaxKind.ReturnStatement) {
            if (node.syntax.kind == SyntaxKind.BlockStatement) {
                return new BoundSequencePointWithLocation(
                    node.syntax,
                    node,
                    ((BlockStatementSyntax)node.syntax).closeBrace.location
                );
            }

            // Compiler generated within a compiler generated body (an implicit constructor for example)
            return node;
        }

        return AddSequencePoint(node);
    }

    internal override BoundNode VisitExpressionStatement(BoundExpressionStatement node) {
        if (node.IsConstructorInitializer()) {
            var syntax = node.syntax;

            switch (syntax) {
                case ConstructorDeclarationSyntax ctorDecl:
                    TextLocation location;

                    if (ctorDecl.modifiers.Any(SyntaxKind.StaticKeyword)) {
                        var start = ctorDecl.body.openBrace.span.start;
                        var end = ctorDecl.body.openBrace.span.end;
                        location = FromBounds(syntax, start, end);
                    } else {
                        location = CreateLocation(ctorDecl.modifiers, ctorDecl.constructorKeyword, ctorDecl.parameterList.closeParenthesis);
                    }

                    return new BoundSequencePointWithLocation(ctorDecl, node, location);
                case ConstructorInitializerSyntax ctorInit:
                    return new BoundSequencePointWithLocation(
                        ctorInit,
                        node,
                        FromBounds(syntax, ctorInit.thisOrBaseKeyword.span.start, ctorInit.argumentList.closeParenthesis.span.end)
                    );
                case TypeDeclarationSyntax typeDecl:
                    return new BoundSequencePointWithLocation(
                        typeDecl,
                        node,
                        FromBounds(typeDecl, typeDecl.identifier.span.start, typeDecl.identifier.span.end)
                    );
                case CompilationUnitSyntax:
                    return node;
                default:
                    throw ExceptionUtilities.UnexpectedValue(syntax.kind);
            }
        }

        return AddSequencePoint(node);
    }

    private static TextLocation FromBounds(SyntaxNode node, int start, int end) {
        return new TextLocation(node.syntaxTree.text, TextSpan.FromBounds(start, end));
    }

    private static BoundStatement AddSequencePoint(BoundStatement node) {
        return new BoundSequencePoint(node.syntax, node);
    }

    private static TextLocation CreateLocation(
        SyntaxTokenList startOpt,
        SyntaxNodeOrToken startFallbackOpt,
        SyntaxNodeOrToken endOpt) {
        SyntaxNodeOrToken node;
        int startPos;

        if (startOpt.Count > 0)
            node = startOpt.First();
        else if (startFallbackOpt is not null)
            node = startFallbackOpt;
        else
            node = endOpt;

        startPos = node.span.start;

        int endPos;

        if (endOpt is not null)
            endPos = GetEndPosition(endOpt);
        else
            endPos = GetEndPosition(startFallbackOpt);

        return new TextLocation(node.syntaxTree.text, TextSpan.FromBounds(startPos, endPos));
    }

    private static int GetEndPosition(SyntaxNodeOrToken nodeOrToken) {
        return nodeOrToken.AsNode(out var node) ? node.GetLastToken().span.end : nodeOrToken.span.end;
    }
}

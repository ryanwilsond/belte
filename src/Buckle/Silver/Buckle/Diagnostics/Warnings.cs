using System;
using System.Linq;
using Buckle.CodeAnalysis.Text;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.Diagnostics;

internal static class Warning {
    public static Diagnostic UnreachableCode(Node node) {
        switch (node.type) {
            case SyntaxType.BLOCK:
                var firstStatement = ((BlockStatement)node).statements.FirstOrDefault();
                // Report just for non empty blocks.
                if (firstStatement != null)
                    return UnreachableCode(firstStatement);

                return null;
            case SyntaxType.VARIABLE_DECLARATION_STATEMENT:
                return UnreachableCode(((VariableDeclarationStatement)node).typeClause.location);
            case SyntaxType.IF_STATEMENT:
                return UnreachableCode(((IfStatement)node).ifKeyword.location);
            case SyntaxType.WHILE_STATEMENT:
                return UnreachableCode(((WhileStatement)node).keyword.location);
            case SyntaxType.DO_WHILE_STATEMENT:
                return UnreachableCode(((DoWhileStatement)node).doKeyword.location);
            case SyntaxType.FOR_KEYWORD:
                return UnreachableCode(((ForStatement)node).keyword.location);
            case SyntaxType.BREAK_STATEMENT:
                return UnreachableCode(((BreakStatement)node).keyword.location);
            case SyntaxType.CONTINUE_STATEMENT:
                return UnreachableCode(((ContinueStatement)node).keyword.location);
            case SyntaxType.RETURN_STATEMENT:
                return UnreachableCode(((ReturnStatement)node).keyword.location);
            case SyntaxType.EXPRESSION_STATEMENT:
                var expression = ((ExpressionStatement)node).expression;
                return UnreachableCode(expression);
            case SyntaxType.CALL_EXPRESSION:
                return UnreachableCode(((CallExpression)node).identifier.location);
            default:
                throw new Exception($"UnreachableCode: unexpected syntax '{node.type}'");
        }
    }

    internal static Diagnostic UnreachableCode(TextLocation location) {
        var message = "unreachable code";
        return new Diagnostic(DiagnosticType.Warning, location, message);
    }

    internal static Diagnostic AlwaysValue(TextLocation location, object value) {
        var message = $"expression will always result in '{value}'";
        return new Diagnostic(DiagnosticType.Warning, location, message);
    }
}

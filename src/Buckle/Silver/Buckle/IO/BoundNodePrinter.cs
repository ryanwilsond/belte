using System;
using System.CodeDom.Compiler;
using System.IO;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.IO;

internal static class BoundNodePrinter {
    public static void WriteTo(this BoundNode node, TextWriter writer) {
        if (writer is IndentedTextWriter iw)
            WriteTo(node, iw);
        else
            WriteTo(node, new IndentedTextWriter(writer));
    }

    public static void WriteTypeClause(BoundTypeClause type, IndentedTextWriter writer) {
        writer.WriteType(type.BaseType().ToString());
        var brackets = "";
        for (int i=0; i<type.dimensions; i++)
            brackets += "[]";
        writer.WritePunctuation(brackets);
    }

    public static void WriteTypeClause(BoundTypeClause type, TextWriter writer) {
        writer.WriteType(type.BaseType().ToString());
        var brackets = "";
        for (int i=0; i<type.dimensions; i++)
            brackets += "[]";
        writer.WritePunctuation(brackets);
    }

    public static void WriteTo(this BoundNode node, IndentedTextWriter writer) {
        switch (node.type) {
            case BoundNodeType.UnaryExpression:
                WriteUnaryExpression((BoundUnaryExpression)node, writer);
                break;
            case BoundNodeType.LiteralExpression:
                if (node is BoundInitializerListExpression il)
                    WriteInitializerListExpression(il, writer);
                else
                    WriteLiteralExpression((BoundLiteralExpression)node, writer);
                break;
            case BoundNodeType.BinaryExpression:
                WriteBinaryExpression((BoundBinaryExpression)node, writer);
                break;
            case BoundNodeType.VariableExpression:
                WriteVariableExpression((BoundVariableExpression)node, writer);
                break;
            case BoundNodeType.AssignmentExpression:
                WriteAssignmentExpression((BoundAssignmentExpression)node, writer);
                break;
            case BoundNodeType.EmptyExpression:
                WriteEmptyExpression((BoundEmptyExpression)node, writer);
                break;
            case BoundNodeType.ErrorExpression:
                WriteErrorExpression((BoundErrorExpression)node, writer);
                break;
            case BoundNodeType.CallExpression:
                WriteCallExpression((BoundCallExpression)node, writer);
                break;
            case BoundNodeType.CastExpression:
                WriteCastExpression((BoundCastExpression)node, writer);
                break;
            case BoundNodeType.NopStatement:
                WriteNopStatement((BoundNopStatement)node, writer);
                break;
            case BoundNodeType.BlockStatement:
                WriteBlockStatement((BoundBlockStatement)node, writer);
                break;
            case BoundNodeType.ExpressionStatement:
                WriteExpressionStatement((BoundExpressionStatement)node, writer);
                break;
            case BoundNodeType.VariableDeclarationStatement:
                WriteVariableDeclarationStatement((BoundVariableDeclarationStatement)node, writer);
                break;
            case BoundNodeType.IfStatement:
                WriteIfStatement((BoundIfStatement)node, writer);
                break;
            case BoundNodeType.WhileStatement:
                WriteWhileStatement((BoundWhileStatement)node, writer);
                break;
            case BoundNodeType.ForStatement:
                WriteForStatement((BoundForStatement)node, writer);
                break;
            case BoundNodeType.GotoStatement:
                WriteGotoStatement((BoundGotoStatement)node, writer);
                break;
            case BoundNodeType.LabelStatement:
                WriteLabelStatement((BoundLabelStatement)node, writer);
                break;
            case BoundNodeType.ConditionalGotoStatement:
                WriteConditionalGotoStatement((BoundConditionalGotoStatement)node, writer);
                break;
            case BoundNodeType.DoWhileStatement:
                WriteDoWhileStatement((BoundDoWhileStatement)node, writer);
                break;
            case BoundNodeType.ReturnStatement:
                WriteReturnStatement((BoundReturnStatement)node, writer);
                break;
            case BoundNodeType.TryStatement:
                WriteTryStatement((BoundTryStatement)node, writer);
                break;
            default:
                throw new Exception($"unexpected node '{node.type}'");
        }
    }

    private static void WriteTryStatement(BoundTryStatement node, IndentedTextWriter writer) {
        writer.WriteKeyword(SyntaxType.TRY_KEYWORD);
        writer.WriteSpace();
        WriteBlockStatement(node.body, writer, false);

        if (node.catchBody != null) {
            writer.WriteSpace();
            writer.WriteKeyword(SyntaxType.CATCH_KEYWORD);
            writer.WriteSpace();
            WriteBlockStatement(node.catchBody, writer, false);
        }

        if (node.finallyBody != null) {
            writer.WriteSpace();
            writer.WriteKeyword(SyntaxType.FINALLY_KEYWORD);
            writer.WriteSpace();
            WriteBlockStatement(node.finallyBody, writer, false);
        }

        writer.WriteLine();
    }

    private static void WriteNopStatement(BoundNopStatement node, IndentedTextWriter writer) {
        writer.WriteKeyword("nop");
        writer.WriteLine();
    }

    private static void WriteReturnStatement(BoundReturnStatement node, IndentedTextWriter writer) {
        writer.WriteKeyword(SyntaxType.RETURN_KEYWORD);

        if (node.expression != null) {
            writer.WriteSpace();
            node.expression.WriteTo(writer);
        }

        writer.WriteLine();
    }

    private static void WriteNestedStatement(this IndentedTextWriter writer, BoundStatement node) {
        var needsIndentation = !(node is BoundBlockStatement);

        if (needsIndentation)
            writer.Indent++;

        node.WriteTo(writer);

        if (needsIndentation)
            writer.Indent--;
    }

    private static void WriteNestedExpression(
        this IndentedTextWriter writer, int parentPrecedence, BoundExpression expression) {
        if (expression is BoundUnaryExpression u)
            writer.WriteNestedExpression(parentPrecedence, SyntaxFacts.GetUnaryPrecedence(u.op.type), u);
        else if (expression is BoundBinaryExpression b)
            writer.WriteNestedExpression(parentPrecedence, SyntaxFacts.GetBinaryPrecedence(b.op.type), b);
        else
            expression.WriteTo(writer);
    }

    private static void WriteNestedExpression(
        this IndentedTextWriter writer, int parentPrecedence, int currentPrecedence, BoundExpression expression) {
        var needsParenthesis = parentPrecedence >= currentPrecedence;

        if (needsParenthesis)
            writer.WritePunctuation(SyntaxType.OPEN_PAREN_TOKEN);

        expression.WriteTo(writer);

        if (needsParenthesis)
            writer.WritePunctuation(SyntaxType.CLOSE_PAREN_TOKEN);
    }

    private static void WriteDoWhileStatement(BoundDoWhileStatement node, IndentedTextWriter writer) {
        writer.WriteKeyword(SyntaxType.DO_KEYWORD);
        writer.WriteSpace();
        writer.WritePunctuation(SyntaxType.OPEN_BRACE_TOKEN);
        writer.WriteLine();
        writer.WriteNestedStatement(node.body);
        writer.WritePunctuation(SyntaxType.CLOSE_BRACE_TOKEN);
        writer.WriteSpace();
        writer.WriteKeyword(SyntaxType.WHILE_KEYWORD);
        writer.WriteSpace();
        writer.WritePunctuation(SyntaxType.OPEN_PAREN_TOKEN);
        node.condition.WriteTo(writer);
        writer.WritePunctuation(SyntaxType.CLOSE_PAREN_TOKEN);
        writer.WriteLine();
    }

    private static void WriteConditionalGotoStatement(
        BoundConditionalGotoStatement node, IndentedTextWriter writer) {
        writer.WriteKeyword("goto");
        writer.WriteSpace();
        writer.WriteIdentifier(node.label.name);
        writer.WriteSpace();
        writer.WriteKeyword(node.jumpIfTrue ? "if" : "unless");
        writer.WriteSpace();
        node.condition.WriteTo(writer);
        writer.WriteLine();
    }

    private static void WriteLabelStatement(BoundLabelStatement node, IndentedTextWriter writer) {
        var unindent = writer.Indent > 0;
        if (unindent)
            writer.Indent--;

        writer.WritePunctuation(node.label.name);
        writer.WritePunctuation(":");
        writer.WriteLine();

        if (unindent)
            writer.Indent++;
    }

    private static void WriteGotoStatement(BoundGotoStatement node, IndentedTextWriter writer) {
        writer.WriteKeyword("goto");
        writer.WriteSpace();
        writer.WriteIdentifier(node.label.name);
        writer.WriteLine();
    }

    private static void WriteForStatement(BoundForStatement node, IndentedTextWriter writer) {
        writer.WriteKeyword(SyntaxType.FOR_KEYWORD);
        writer.WriteSpace();
        writer.WritePunctuation(SyntaxType.OPEN_PAREN_TOKEN);
        node.initializer.WriteTo(writer);
        writer.WriteSpace();
        node.condition.WriteTo(writer);
        writer.WriteSpace();
        node.step.WriteTo(writer);
        writer.WritePunctuation(SyntaxType.CLOSE_PAREN_TOKEN);
        writer.WriteSpace();
        writer.WritePunctuation(SyntaxType.OPEN_BRACE_TOKEN);
        writer.WriteLine();
        writer.WriteNestedStatement(node.body);
        writer.WritePunctuation(SyntaxType.CLOSE_BRACE_TOKEN);
        writer.WriteLine();
    }

    private static void WriteWhileStatement(BoundWhileStatement node, IndentedTextWriter writer) {
        writer.WriteKeyword(SyntaxType.WHILE_KEYWORD);
        writer.WriteSpace();
        writer.WritePunctuation(SyntaxType.OPEN_PAREN_TOKEN);
        node.condition.WriteTo(writer);
        writer.WritePunctuation(SyntaxType.CLOSE_PAREN_TOKEN);
        writer.WriteSpace();
        writer.WritePunctuation(SyntaxType.OPEN_BRACE_TOKEN);
        writer.WriteLine();
        writer.WriteNestedStatement(node.body);
        writer.WritePunctuation(SyntaxType.CLOSE_BRACE_TOKEN);
        writer.WriteLine();
    }

    private static void WriteIfStatement(BoundIfStatement node, IndentedTextWriter writer) {
        writer.WriteKeyword(SyntaxType.IF_KEYWORD);
        writer.WriteSpace();
        writer.WritePunctuation(SyntaxType.OPEN_PAREN_TOKEN);
        node.condition.WriteTo(writer);
        writer.WritePunctuation(SyntaxType.CLOSE_PAREN_TOKEN);
        writer.WriteSpace();
        writer.WritePunctuation(SyntaxType.OPEN_BRACE_TOKEN);
        writer.WriteLine();
        writer.WriteNestedStatement(node.then);
        writer.WritePunctuation(SyntaxType.CLOSE_BRACE_TOKEN);

        if (node.elseStatement != null) {
            writer.WriteSpace();
            writer.WriteKeyword(SyntaxType.ELSE_KEYWORD);
            writer.WriteSpace();
            writer.WritePunctuation(SyntaxType.OPEN_BRACE_TOKEN);
            writer.WriteLine();
            writer.WriteNestedStatement(node.elseStatement);
            writer.WritePunctuation(SyntaxType.CLOSE_BRACE_TOKEN);
        }

        writer.WriteLine();
    }

    private static void WriteVariableDeclarationStatement(
        BoundVariableDeclarationStatement node, IndentedTextWriter writer) {
        WriteTypeClause(node.variable.typeClause, writer);
        writer.WriteSpace();
        writer.WriteIdentifier(node.variable.name);
        writer.WriteSpace();
        writer.WritePunctuation(SyntaxType.EQUALS_TOKEN);
        writer.WriteSpace();
        node.initializer.WriteTo(writer);
        writer.WriteLine();
    }

    private static void WriteExpressionStatement(BoundExpressionStatement node, IndentedTextWriter writer) {
        if (node.expression is BoundEmptyExpression)
            return;

        node.expression.WriteTo(writer);
        writer.WriteLine();
    }

    private static void WriteBlockStatement(BoundBlockStatement node, IndentedTextWriter writer, bool newLine = true) {
        writer.WritePunctuation(SyntaxType.OPEN_BRACE_TOKEN);
        writer.WriteLine();
        writer.Indent++;

        foreach (var s in node.statements)
            s.WriteTo(writer);

        writer.Indent--;
        writer.WritePunctuation(SyntaxType.CLOSE_BRACE_TOKEN);

        if (newLine)
            writer.WriteLine();
    }

    private static void WriteCastExpression(BoundCastExpression node, IndentedTextWriter writer) {
        writer.WriteType(node.typeClause.lType.name);
        writer.WritePunctuation(SyntaxType.OPEN_PAREN_TOKEN);
        node.expression.WriteTo(writer);
        writer.WritePunctuation(SyntaxType.CLOSE_PAREN_TOKEN);
    }

    private static void WriteCallExpression(BoundCallExpression node, IndentedTextWriter writer) {
        writer.WriteIdentifier(node.function.name);
        writer.WritePunctuation(SyntaxType.OPEN_PAREN_TOKEN);

        var isFirst = true;
        foreach (var argument in node.arguments) {
            if (isFirst) {
                isFirst = false;
            } else {
                writer.WritePunctuation(SyntaxType.COMMA_TOKEN);
                writer.WriteSpace();
            }

            argument.WriteTo(writer);
        }

        writer.WritePunctuation(SyntaxType.CLOSE_PAREN_TOKEN);
    }

    private static void WriteInitializerListExpression(BoundInitializerListExpression node, IndentedTextWriter writer) {
        writer.WritePunctuation(SyntaxType.OPEN_BRACE_TOKEN);

        var isFirst = true;
        foreach (var item in node.items) {
            if (isFirst) {
                isFirst = false;
            } else {
                writer.WritePunctuation(SyntaxType.COMMA_TOKEN);
                writer.WriteSpace();
            }

            item.WriteTo(writer);
        }

        writer.WritePunctuation(SyntaxType.CLOSE_BRACE_TOKEN);
    }

    private static void WriteErrorExpression(BoundErrorExpression node, IndentedTextWriter writer) {
        writer.WriteKeyword("?");
    }

    private static void WriteEmptyExpression(BoundEmptyExpression node, IndentedTextWriter writer) { }

    private static void WriteAssignmentExpression(BoundAssignmentExpression node, IndentedTextWriter writer) {
        writer.WriteIdentifier(node.variable.name);
        writer.WriteSpace();
        writer.WritePunctuation(SyntaxType.EQUALS_TOKEN);
        writer.WriteSpace();
        node.expression.WriteTo(writer);
    }

    private static void WriteVariableExpression(BoundVariableExpression node, IndentedTextWriter writer) {
        writer.WriteIdentifier(node.variable.name);
    }

    private static void WriteBinaryExpression(BoundBinaryExpression node, IndentedTextWriter writer) {
        var op = SyntaxFacts.GetText(node.op.type);
        var precedence = SyntaxFacts.GetBinaryPrecedence(node.op.type);

        writer.WriteNestedExpression(precedence, node.left);
        writer.WriteSpace();
        writer.WritePunctuation(op);
        writer.WriteSpace();
        writer.WriteNestedExpression(precedence, node.right);
    }

    private static void WriteLiteralExpression(BoundLiteralExpression node, IndentedTextWriter writer) {
        if (node.value == null) {
            writer.WriteKeyword(SyntaxType.NULL_KEYWORD);
            return;
        }

        var value = node.value.ToString();

        if (node.typeClause.lType == TypeSymbol.Bool) {
            writer.WriteKeyword(value);
        } else if (node.typeClause.lType == TypeSymbol.Int) {
            writer.WriteNumber(value);
        } else if (node.typeClause.lType == TypeSymbol.String) {
            value = "\"" + value.Replace("\"", "\"\"") + "\"";
            writer.WriteString(value);
        } else if (node.typeClause.lType == TypeSymbol.Decimal) {
            writer.WriteNumber(value);
        } else {
            throw new Exception($"unexpected type '{node.typeClause.lType}'");
        }
    }

    private static void WriteUnaryExpression(BoundUnaryExpression node, IndentedTextWriter writer) {
        var precedence = SyntaxFacts.GetUnaryPrecedence(node.op.type);

        writer.WritePunctuation(node.op.type);
        writer.WriteNestedExpression(precedence, node.operand);
    }
}

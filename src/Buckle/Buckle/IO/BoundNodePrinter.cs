using System.CodeDom.Compiler;
using System.IO;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.IO;

/// <summary>
/// Writes user friendly representation of BoundNodes.
/// </summary>
internal static class BoundNodePrinter {
    /// <summary>
    /// Writes a single <see cref="BoundNode" />.
    /// </summary>
    /// <param name="node"><see cref="BoundNode" /> to print (not modified).</param>
    /// <param name="writer">Where to write to (out).</param>
    internal static void WriteTo(this BoundNode node, TextWriter writer) {
        if (writer is IndentedTextWriter iw)
            WriteTo(node, iw);
        else
            WriteTo(node, new IndentedTextWriter(writer));
    }

    /// <summary>
    /// Writes a single <see cref="BoundType" />.
    /// </summary>
    /// <param name="type"><see cref="BoundType" /> to print (not modified).</param>
    /// <param name="writer">Where to write to (out).</param>
    internal static void WriteType(BoundType type, TextWriter writer) {
        writer.WriteType(type.BaseType().ToString());
        var brackets = "";

        for (int i=0; i<type.dimensions; i++)
            brackets += "[]";

        writer.WritePunctuation(brackets);
    }

    /// <summary>
    /// Writes a single <see cref="BoundNode" /> using an IndentedTextWriter.
    /// </summary>
    /// <param name="node"><see cref="BoundNode" /> to print (not modified).</param>
    /// <param name="writer">Where to write to with indentation (out).</param>
    internal static void WriteTo(this BoundNode node, IndentedTextWriter writer) {
        switch (node.kind) {
            case BoundNodeKind.NopStatement:
                WriteNopStatement((BoundNopStatement)node, writer);
                break;
            case BoundNodeKind.BlockStatement:
                WriteBlockStatement((BoundBlockStatement)node, writer);
                break;
            case BoundNodeKind.ExpressionStatement:
                WriteExpressionStatement((BoundExpressionStatement)node, writer);
                break;
            case BoundNodeKind.VariableDeclarationStatement:
                WriteVariableDeclarationStatement((BoundVariableDeclarationStatement)node, writer);
                break;
            case BoundNodeKind.IfStatement:
                WriteIfStatement((BoundIfStatement)node, writer);
                break;
            case BoundNodeKind.WhileStatement:
                WriteWhileStatement((BoundWhileStatement)node, writer);
                break;
            case BoundNodeKind.ForStatement:
                WriteForStatement((BoundForStatement)node, writer);
                break;
            case BoundNodeKind.GotoStatement:
                WriteGotoStatement((BoundGotoStatement)node, writer);
                break;
            case BoundNodeKind.LabelStatement:
                WriteLabelStatement((BoundLabelStatement)node, writer);
                break;
            case BoundNodeKind.ConditionalGotoStatement:
                WriteConditionalGotoStatement((BoundConditionalGotoStatement)node, writer);
                break;
            case BoundNodeKind.DoWhileStatement:
                WriteDoWhileStatement((BoundDoWhileStatement)node, writer);
                break;
            case BoundNodeKind.ReturnStatement:
                WriteReturnStatement((BoundReturnStatement)node, writer);
                break;
            case BoundNodeKind.TryStatement:
                WriteTryStatement((BoundTryStatement)node, writer);
                break;
            case BoundNodeKind.TernaryExpression:
                WriteTernaryExpression((BoundTernaryExpression)node, writer);
                break;
            case BoundNodeKind.IndexExpression:
                WriteIndexExpression((BoundIndexExpression)node, writer);
                break;
            case BoundNodeKind.ReferenceExpression:
                WriteReferenceExpression((BoundReferenceExpression)node, writer);
                break;
            case BoundNodeKind.UnaryExpression:
                WriteUnaryExpression((BoundUnaryExpression)node, writer);
                break;
            case BoundNodeKind.LiteralExpression:
                if (node is BoundInitializerListExpression il)
                    WriteInitializerListExpression(il, writer);
                else
                    WriteLiteralExpression((BoundLiteralExpression)node, writer);
                break;
            case BoundNodeKind.BinaryExpression:
                WriteBinaryExpression((BoundBinaryExpression)node, writer);
                break;
            case BoundNodeKind.VariableExpression:
                WriteVariableExpression((BoundVariableExpression)node, writer);
                break;
            case BoundNodeKind.AssignmentExpression:
                WriteAssignmentExpression((BoundAssignmentExpression)node, writer);
                break;
            case BoundNodeKind.EmptyExpression:
                WriteEmptyExpression((BoundEmptyExpression)node, writer);
                break;
            case BoundNodeKind.ErrorExpression:
                WriteErrorExpression((BoundErrorExpression)node, writer);
                break;
            case BoundNodeKind.CallExpression:
                WriteCallExpression((BoundCallExpression)node, writer);
                break;
            case BoundNodeKind.CastExpression:
                WriteCastExpression((BoundCastExpression)node, writer);
                break;
            case BoundNodeKind.TypeOfExpression:
                WriteTypeOfExpression((BoundTypeOfExpression)node, writer);
                break;
            case BoundNodeKind.ConstructorExpression:
                WriteConstructorExpression((BoundConstructorExpression)node, writer);
                break;
            case BoundNodeKind.MemberAccessExpression:
                WriteMemberAccessExpression((BoundMemberAccessExpression)node, writer);
                break;
            default:
                throw new BelteInternalException($"WriteTo: unexpected node '{node.kind}'");
        }
    }

    private static void WriteTryStatement(BoundTryStatement node, IndentedTextWriter writer) {
        writer.WriteKeyword(SyntaxKind.TryKeyword);
        writer.WriteSpace();
        WriteBlockStatement(node.body, writer, false);

        if (node.catchBody != null) {
            writer.WriteSpace();
            writer.WriteKeyword(SyntaxKind.CatchKeyword);
            writer.WriteSpace();
            WriteBlockStatement(node.catchBody, writer, false);
        }

        if (node.finallyBody != null) {
            writer.WriteSpace();
            writer.WriteKeyword(SyntaxKind.FinallyKeyword);
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
        writer.WriteKeyword(SyntaxKind.ReturnKeyword);

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

    private static void WriteDoWhileStatement(BoundDoWhileStatement node, IndentedTextWriter writer) {
        writer.WriteKeyword(SyntaxKind.DoKeyword);
        writer.WriteSpace();
        writer.WritePunctuation(SyntaxKind.OpenBraceToken);
        writer.WriteLine();
        writer.WriteNestedStatement(node.body);
        writer.WritePunctuation(SyntaxKind.CloseBraceToken);
        writer.WriteSpace();
        writer.WriteKeyword(SyntaxKind.WhileKeyword);
        writer.WriteSpace();
        writer.WritePunctuation(SyntaxKind.OpenParenToken);
        node.condition.WriteTo(writer);
        writer.WritePunctuation(SyntaxKind.CloseParenToken);
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
        writer.WriteKeyword(SyntaxKind.ForKeyword);
        writer.WriteSpace();
        writer.WritePunctuation(SyntaxKind.OpenParenToken);
        node.initializer.WriteTo(writer);
        writer.WriteSpace();
        node.condition.WriteTo(writer);
        writer.WriteSpace();
        node.step.WriteTo(writer);
        writer.WritePunctuation(SyntaxKind.CloseParenToken);
        writer.WriteSpace();
        writer.WritePunctuation(SyntaxKind.OpenBraceToken);
        writer.WriteLine();
        writer.WriteNestedStatement(node.body);
        writer.WritePunctuation(SyntaxKind.CloseBraceToken);
        writer.WriteLine();
    }

    private static void WriteWhileStatement(BoundWhileStatement node, IndentedTextWriter writer) {
        writer.WriteKeyword(SyntaxKind.WhileKeyword);
        writer.WriteSpace();
        writer.WritePunctuation(SyntaxKind.OpenParenToken);
        node.condition.WriteTo(writer);
        writer.WritePunctuation(SyntaxKind.CloseParenToken);
        writer.WriteSpace();
        writer.WritePunctuation(SyntaxKind.OpenBraceToken);
        writer.WriteLine();
        writer.WriteNestedStatement(node.body);
        writer.WritePunctuation(SyntaxKind.CloseBraceToken);
        writer.WriteLine();
    }

    private static void WriteIfStatement(BoundIfStatement node, IndentedTextWriter writer) {
        writer.WriteKeyword(SyntaxKind.IfKeyword);
        writer.WriteSpace();
        writer.WritePunctuation(SyntaxKind.OpenParenToken);
        node.condition.WriteTo(writer);
        writer.WritePunctuation(SyntaxKind.CloseParenToken);
        writer.WriteSpace();
        writer.WritePunctuation(SyntaxKind.OpenBraceToken);
        writer.WriteLine();
        writer.WriteNestedStatement(node.then);
        writer.WritePunctuation(SyntaxKind.CloseBraceToken);

        if (node.elseStatement != null) {
            writer.WriteSpace();
            writer.WriteKeyword(SyntaxKind.ElseKeyword);
            writer.WriteSpace();
            writer.WritePunctuation(SyntaxKind.OpenBraceToken);
            writer.WriteLine();
            writer.WriteNestedStatement(node.elseStatement);
            writer.WritePunctuation(SyntaxKind.CloseBraceToken);
        }

        writer.WriteLine();
    }

    private static void WriteVariableDeclarationStatement(
        BoundVariableDeclarationStatement node, IndentedTextWriter writer) {
        WriteType(node.variable.type, writer);
        writer.WriteSpace();
        writer.WriteIdentifier(node.variable.name);
        writer.WriteSpace();
        writer.WritePunctuation(SyntaxKind.EqualsToken);
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
        writer.WritePunctuation(SyntaxKind.OpenBraceToken);
        writer.WriteLine();
        writer.Indent++;

        foreach (var s in node.statements)
            s.WriteTo(writer);

        writer.Indent--;
        writer.WritePunctuation(SyntaxKind.CloseBraceToken);

        if (newLine)
            writer.WriteLine();
    }

    private static void WriteMemberAccessExpression(BoundMemberAccessExpression node, IndentedTextWriter writer) {
        node.operand.WriteTo(writer);
        writer.WritePunctuation(SyntaxKind.PeriodToken);
        writer.WriteIdentifier(node.member.name);
    }

    private static void WriteConstructorExpression(BoundConstructorExpression node, IndentedTextWriter writer) {
        node.symbol.WriteTo(writer);
        writer.WritePunctuation(SyntaxKind.OpenParenToken);
        writer.WritePunctuation(SyntaxKind.CloseParenToken);
    }

    private static void WriteTernaryExpression(BoundTernaryExpression node, IndentedTextWriter writer) {
        var precedence = SyntaxFacts.GetTernaryPrecedence(node.op.leftOpKind);

        writer.WriteNestedExpression(precedence, node.left);
        writer.WriteSpace();
        writer.WritePunctuation(node.op.leftOpKind);
        writer.WriteSpace();
        writer.WriteNestedExpression(precedence, node.center);
        writer.WriteSpace();
        writer.WritePunctuation(node.op.rightOpKind);
        writer.WriteSpace();
        writer.WriteNestedExpression(precedence, node.right);
    }

    private static void WriteTypeOfExpression(BoundTypeOfExpression node, IndentedTextWriter writer) {
        writer.WriteKeyword(SyntaxKind.TypeOfKeyword);
        writer.WritePunctuation(SyntaxKind.OpenParenToken);
        node.typeOfType.WriteTo(writer);
        writer.WritePunctuation(SyntaxKind.CloseParenToken);
    }

    private static void WriteIndexExpression(BoundIndexExpression node, IndentedTextWriter writer) {
        node.operand.WriteTo(writer);
        writer.WritePunctuation(SyntaxKind.OpenBracketToken);
        node.index.WriteTo(writer);
        writer.WritePunctuation(SyntaxKind.CloseBracketToken);
    }

    private static void WriteReferenceExpression(BoundReferenceExpression node, IndentedTextWriter writer) {
        writer.WriteKeyword(SyntaxKind.RefKeyword);
        writer.WriteSpace();
        node.variable.WriteTo(writer);
    }

    private static void WriteNestedExpression(
        this IndentedTextWriter writer, int parentPrecedence, BoundExpression expression) {
        var expr = expression;

        if (expression is BoundAssignmentExpression a)
            expr = a.right;

        if (expr is BoundUnaryExpression u)
            writer.WriteNestedExpression(parentPrecedence, SyntaxFacts.GetUnaryPrecedence(u.op.kind), expression);
        else if (expr is BoundBinaryExpression b)
            writer.WriteNestedExpression(parentPrecedence, SyntaxFacts.GetBinaryPrecedence(b.op.kind), expression);
        else if (expr is BoundTernaryExpression t)
            writer.WriteNestedExpression(
                parentPrecedence, SyntaxFacts.GetTernaryPrecedence(t.op.leftOpKind), expression);
        else
            expression.WriteTo(writer);
    }

    private static void WriteNestedExpression(
        this IndentedTextWriter writer, int parentPrecedence, int currentPrecedence, BoundExpression expression) {
        var needsParenthesis = parentPrecedence >= currentPrecedence;

        if (needsParenthesis)
            writer.WritePunctuation(SyntaxKind.OpenParenToken);

        expression.WriteTo(writer);

        if (needsParenthesis)
            writer.WritePunctuation(SyntaxKind.CloseParenToken);
    }

    private static void WriteCastExpression(BoundCastExpression node, IndentedTextWriter writer) {
        writer.WriteType(node.type.typeSymbol.name);
        writer.WritePunctuation(SyntaxKind.OpenParenToken);
        node.expression.WriteTo(writer);
        writer.WritePunctuation(SyntaxKind.CloseParenToken);
    }

    private static void WriteCallExpression(BoundCallExpression node, IndentedTextWriter writer) {
        writer.WriteIdentifier(node.function.name);
        writer.WritePunctuation(SyntaxKind.OpenParenToken);

        var isFirst = true;
        foreach (var argument in node.arguments) {
            if (isFirst) {
                isFirst = false;
            } else {
                writer.WritePunctuation(SyntaxKind.CommaToken);
                writer.WriteSpace();
            }

            argument.WriteTo(writer);
        }

        writer.WritePunctuation(SyntaxKind.CloseParenToken);
    }

    private static void WriteInitializerListExpression(BoundInitializerListExpression node, IndentedTextWriter writer) {
        writer.WritePunctuation(SyntaxKind.OpenBraceToken);

        var isFirst = true;
        foreach (var item in node.items) {
            if (isFirst) {
                isFirst = false;
            } else {
                writer.WritePunctuation(SyntaxKind.CommaToken);
                writer.WriteSpace();
            }

            item.WriteTo(writer);
        }

        writer.WritePunctuation(SyntaxKind.CloseBraceToken);
    }

    private static void WriteErrorExpression(BoundErrorExpression node, IndentedTextWriter writer) {
        writer.WriteKeyword("?");
    }

    private static void WriteEmptyExpression(BoundEmptyExpression node, IndentedTextWriter writer) { }

    private static void WriteAssignmentExpression(BoundAssignmentExpression node, IndentedTextWriter writer) {
        node.left.WriteTo(writer);
        writer.WriteSpace();
        writer.WritePunctuation(SyntaxKind.EqualsToken);
        writer.WriteSpace();
        node.right.WriteTo(writer);
    }

    private static void WriteVariableExpression(BoundVariableExpression node, IndentedTextWriter writer) {
        writer.WriteIdentifier(node.variable.name);
    }

    private static void WriteBinaryExpression(BoundBinaryExpression node, IndentedTextWriter writer) {
        var precedence = SyntaxFacts.GetBinaryPrecedence(node.op.kind);

        writer.WriteNestedExpression(precedence, node.left);
        writer.WriteSpace();
        writer.WritePunctuation(node.op.kind);
        writer.WriteSpace();
        writer.WriteNestedExpression(precedence, node.right);
    }

    private static void WriteLiteralExpression(BoundLiteralExpression node, IndentedTextWriter writer) {
        if (node.value == null) {
            writer.WriteKeyword(SyntaxKind.NullKeyword);
            return;
        }

        var value = node.value.ToString();

        if (node.type.typeSymbol == TypeSymbol.Bool) {
            writer.WriteKeyword(value);
        } else if (node.type.typeSymbol == TypeSymbol.Int) {
            writer.WriteNumber(value);
        } else if (node.type.typeSymbol == TypeSymbol.String) {
            value = "\"" + value.Replace("\"", "\"\"") + "\"";
            writer.WriteString(value);
        } else if (node.type.typeSymbol == TypeSymbol.Decimal) {
            writer.WriteNumber(value);
        } else {
            throw new BelteInternalException($"WriteLiteralExpression: unexpected type '{node.type.typeSymbol}'");
        }
    }

    private static void WriteUnaryExpression(BoundUnaryExpression node, IndentedTextWriter writer) {
        var precedence = SyntaxFacts.GetUnaryPrecedence(node.op.kind);

        writer.WritePunctuation(node.op.kind);
        writer.WriteNestedExpression(precedence, node.operand);
    }
}

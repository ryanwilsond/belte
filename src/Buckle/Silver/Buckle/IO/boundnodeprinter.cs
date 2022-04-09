using System;
using System.CodeDom.Compiler;
using System.IO;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.IO {

    internal static class BoundNodePrinter {
        public static void WriteTo(this BoundNode node, TextWriter writer) {
            if (writer is IndentedTextWriter iw)
                WriteTo(node, iw);
            else
                WriteTo(node, new IndentedTextWriter(writer));
        }

        public static void WriteTo(this BoundNode node, IndentedTextWriter writer) {
            switch (node.type) {
                case BoundNodeType.UnaryExpression:
                    WriteUnaryExpression((BoundUnaryExpression)node, writer);
                    break;
                case BoundNodeType.LiteralExpression:
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
                default:
                    throw new Exception($"unexpected node '{node.type}'");
            }
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
                writer.WritePunctuation(SyntaxType.LPAREN);

            expression.WriteTo(writer);

            if (needsParenthesis)
                writer.WritePunctuation(SyntaxType.RPAREN);
        }

        private static void WriteDoWhileStatement(BoundDoWhileStatement node, IndentedTextWriter writer) {
            writer.WriteKeyword(SyntaxType.DO_KEYWORD);
            writer.WriteSpace();
            writer.WritePunctuation(SyntaxType.LBRACE);
            writer.WriteLine();
            writer.WriteNestedStatement(node.body);
            writer.WritePunctuation(SyntaxType.RBRACE);
            writer.WriteSpace();
            writer.WriteKeyword(SyntaxType.WHILE_KEYWORD);
            writer.WriteSpace();
            writer.WritePunctuation(SyntaxType.LPAREN);
            node.condition.WriteTo(writer);
            writer.WritePunctuation(SyntaxType.RPAREN);
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
            writer.WritePunctuation(SyntaxType.LPAREN);
            node.initializer.WriteTo(writer);
            writer.WriteSpace();
            node.condition.WriteTo(writer);
            writer.WriteSpace();
            node.step.WriteTo(writer);
            writer.WritePunctuation(SyntaxType.RPAREN);
            writer.WriteSpace();
            writer.WritePunctuation(SyntaxType.LBRACE);
            writer.WriteLine();
            writer.WriteNestedStatement(node.body);
            writer.WritePunctuation(SyntaxType.RBRACE);
            writer.WriteLine();
        }

        private static void WriteWhileStatement(BoundWhileStatement node, IndentedTextWriter writer) {
            writer.WriteKeyword(SyntaxType.WHILE_KEYWORD);
            writer.WriteSpace();
            writer.WritePunctuation(SyntaxType.LPAREN);
            node.condition.WriteTo(writer);
            writer.WritePunctuation(SyntaxType.RPAREN);
            writer.WriteSpace();
            writer.WritePunctuation(SyntaxType.LBRACE);
            writer.WriteLine();
            writer.WriteNestedStatement(node.body);
            writer.WritePunctuation(SyntaxType.RBRACE);
            writer.WriteLine();
        }

        private static void WriteIfStatement(BoundIfStatement node, IndentedTextWriter writer) {
            writer.WriteKeyword(SyntaxType.IF_KEYWORD);
            writer.WriteSpace();
            writer.WritePunctuation(SyntaxType.LPAREN);
            node.condition.WriteTo(writer);
            writer.WritePunctuation(SyntaxType.RPAREN);
            writer.WriteSpace();
            writer.WritePunctuation(SyntaxType.LBRACE);
            writer.WriteLine();
            writer.WriteNestedStatement(node.then);
            writer.WritePunctuation(SyntaxType.RBRACE);

            if (node.elseStatement != null) {
                writer.WriteSpace();
                writer.WriteKeyword(SyntaxType.ELSE_KEYWORD);
                writer.WriteSpace();
                writer.WritePunctuation(SyntaxType.LBRACE);
                writer.WriteLine();
                writer.WriteNestedStatement(node.elseStatement);
                writer.WritePunctuation(SyntaxType.RBRACE);
            }

            writer.WriteLine();
        }

        private static void WriteVariableDeclarationStatement(
            BoundVariableDeclarationStatement node, IndentedTextWriter writer) {
            writer.WriteType(node.variable.lType.name);
            writer.WriteSpace();
            writer.WriteIdentifier(node.variable.name);
            writer.WriteSpace();
            writer.WritePunctuation(SyntaxType.EQUALS);
            writer.WriteSpace();
            node.initializer.WriteTo(writer);
            writer.WriteLine();
        }

        private static void WriteExpressionStatement(BoundExpressionStatement node, IndentedTextWriter writer) {
            if (node.expression is BoundEmptyExpression) return;
            node.expression.WriteTo(writer);
            writer.WriteLine();
        }

        private static void WriteBlockStatement(BoundBlockStatement node, IndentedTextWriter writer) {
            writer.WritePunctuation(SyntaxType.LBRACE);
            writer.WriteLine();
            writer.Indent++;

            foreach (var s in node.statements)
                s.WriteTo(writer);

            writer.Indent--;
            writer.WritePunctuation(SyntaxType.RBRACE);
            writer.WriteLine();
        }

        private static void WriteCastExpression(BoundCastExpression node, IndentedTextWriter writer) {
            writer.WriteType(node.lType.name);
            writer.WritePunctuation(SyntaxType.LPAREN);
            node.expression.WriteTo(writer);
            writer.WritePunctuation(SyntaxType.RPAREN);
        }

        private static void WriteCallExpression(BoundCallExpression node, IndentedTextWriter writer) {
            writer.WriteIdentifier(node.function.name);
            writer.WritePunctuation(SyntaxType.LPAREN);

            var isFirst = true;
            foreach (var argument in node.arguments) {
                if (isFirst) {
                    isFirst = false;
                } else {
                    writer.WritePunctuation(SyntaxType.COMMA);
                    writer.WriteSpace();
                }

                argument.WriteTo(writer);
            }

            writer.WritePunctuation(SyntaxType.RPAREN);
        }

        private static void WriteErrorExpression(BoundErrorExpression node, IndentedTextWriter writer) {
            writer.WriteKeyword("?");
        }

        private static void WriteEmptyExpression(BoundEmptyExpression node, IndentedTextWriter writer) { }

        private static void WriteAssignmentExpression(BoundAssignmentExpression node, IndentedTextWriter writer) {
            writer.WriteIdentifier(node.variable.name);
            writer.WriteSpace();
            writer.WritePunctuation(SyntaxType.EQUALS);
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
            var value = node.value.ToString();

            if (node.lType == TypeSymbol.Bool) {
                writer.WriteKeyword(value);
            } else if (node.lType == TypeSymbol.Int) {
                writer.WriteNumber(value);
            } else if (node.lType == TypeSymbol.String) {
                value = "\"" + value.Replace("\"", "\"\"") + "\"";
                writer.WriteString(value);
            } else {
                throw new Exception($"unexpected type '{node.lType}'");
            }
        }

        private static void WriteUnaryExpression(BoundUnaryExpression node, IndentedTextWriter writer) {
            var precedence = SyntaxFacts.GetUnaryPrecedence(node.op.type);

            writer.WritePunctuation(node.op.type);
            writer.WriteNestedExpression(precedence, node.operand);
        }
    }
}

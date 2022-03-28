using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax {

    internal enum SyntaxType {
        Invalid,
        // tokens
        EOF,
        WHITESPACE,
        IDENTIFIER,
        NUMBER,
        PLUS,
        MINUS,
        ASTERISK,
        SOLIDUS,
        LPAREN,
        RPAREN,
        EQUALS,
        BANG,
        DAMPERSAND,
        DPIPE,
        // DMINUS,
        // DPLUS,
        // DASTERISK,
        DEQUALS,
        BANGEQUALS,
        // expressions
        LITERAL_EXPR,
        BINARY_EXPR,
        UNARY_EXPR,
        PAREN_EXPR,
        NAME_EXPR,
        ASSIGN_EXPR,
        // keywords
        TRUE_KEYWORD,
        FALSE_KEYWORD,
        // other
        CompilationUnit,
    }

    internal abstract class Node {
        public abstract SyntaxType type { get; }
        public virtual TextSpan span {
            get {
                var first = GetChildren().First().span;
                var last = GetChildren().Last().span;
                return TextSpan.FromBounds(first.start, last.end);
            }
        }
        public IEnumerable<Node> GetChildren() {
            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties) {
                if (typeof(Node).IsAssignableFrom(property.PropertyType)) {
                    yield return (Node)property.GetValue(this);
                } else if (typeof(IEnumerable<Node>).IsAssignableFrom(property.PropertyType)) {
                    var values = (IEnumerable<Node>)property.GetValue(this);
                    foreach (var child in values) {
                        yield return child;
                    }
                }
            }
        }

        public void WriteTo(TextWriter writer) {
            PrettyPrint(writer, this);
        }

        private void PrettyPrint(TextWriter writer, Node node, string indent = "", bool islast=true) {
            bool isConsoleOut = writer == Console.Out;
            string marker = islast ? "└─" : "├─";

            if (isConsoleOut) Console.ForegroundColor = ConsoleColor.DarkGray;
            writer.Write($"{indent}{marker}");

            if (isConsoleOut) Console.ForegroundColor = node is Token ? ConsoleColor.DarkBlue : ConsoleColor.Cyan;
            writer.Write(node.type);

            if (node is Token t && t.value != null)
                writer.Write($" {t.value}");

            Console.ResetColor();
            writer.WriteLine();
            indent += islast ? "  " : "│ ";
            var lastChild = node.GetChildren().LastOrDefault();

            foreach (var child in node.GetChildren())
                PrettyPrint(writer, child, indent, child == lastChild);
        }

        public override string ToString() {
            using (var writer = new StringWriter()) {
                WriteTo(writer);
                return writer.ToString();
            }
        }
    }

    internal sealed class Token : Node {
        public override SyntaxType type { get; }
        public int pos { get; }
        public string text { get; }
        public object value { get; }
        public override TextSpan span => new TextSpan(pos, text?.Length ?? 0);

        public Token(SyntaxType type_, int pos_, string text_, object value_) {
            type = type_;
            pos = pos_;
            text = text_;
            value = value_;
        }
    }

    internal abstract class Expression : Node { }

    internal sealed class LiteralExpression : Expression {
        public Token token { get; }
        public object value { get; }
        public override SyntaxType type => SyntaxType.LITERAL_EXPR;

        public LiteralExpression(Token token_, object value_) {
            token = token_;
            value = value_;
        }

        public LiteralExpression(Token token_) : this(token_, token_.value) { }
    }

    internal sealed class BinaryExpression : Expression {
        public Expression left { get; }
        public Token op { get; }
        public Expression right { get; }
        public override SyntaxType type => SyntaxType.BINARY_EXPR;

        public BinaryExpression(Expression left_, Token op_, Expression right_) {
            left = left_;
            op = op_;
            right = right_;
        }
    }

    internal sealed class ParenExpression : Expression {
        public Token lparen { get; }
        public Expression expr { get; }
        public Token rparen { get; }
        public override SyntaxType type => SyntaxType.PAREN_EXPR;

        public ParenExpression(Token lparen_, Expression expr_, Token rparen_) {
            lparen = lparen_;
            expr = expr_;
            rparen = rparen_;
        }
    }

    internal sealed class UnaryExpression : Expression {
        public Token op { get; }
        public Expression operand { get; }
        public override SyntaxType type => SyntaxType.UNARY_EXPR;

        public UnaryExpression(Token op_, Expression operand_) {
            op = op_;
            operand = operand_;
        }
    }

    internal sealed class NameExpression : Expression {
        public Token id { get; }
        public override SyntaxType type => SyntaxType.NAME_EXPR;

        public NameExpression(Token id_) {
            id = id_;
        }
    }

    internal sealed class AssignmentExpression : Expression {
        public Token id { get; }
        public Token equals { get; }
        public Expression expr { get; }
        public override SyntaxType type => SyntaxType.ASSIGN_EXPR;

        public AssignmentExpression(Token id_, Token equals_, Expression expr_) {
            id = id_;
            equals = equals_;
            expr = expr_;
        }
    }
}

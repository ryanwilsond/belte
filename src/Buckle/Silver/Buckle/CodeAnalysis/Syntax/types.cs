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
        LBRACE,
        RBRACE,
        EQUALS,
        BANG,
        DAMPERSAND,
        DPIPE,
        SEMICOLON,
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
        // statements
        BLOCK_STATEMENT,
        EXPRESSION_STATEMENT,
        // other
        COMPILATION_UNIT,
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
}

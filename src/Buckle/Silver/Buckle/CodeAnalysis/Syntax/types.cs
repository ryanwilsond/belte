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
        SLASH,
        LPAREN,
        RPAREN,
        LBRACE,
        RBRACE,
        EQUALS,
        BANG,
        AMPERSAND,
        PIPE,
        TILDE,
        CARET,
        DAMPERSAND,
        DPIPE,
        SEMICOLON,
        COMMA,
        // DMINUS,
        // DPLUS,
        DASTERISK,
        DEQUALS,
        BANGEQUALS,
        LANGLEBRACKET,
        RANGLEBRACKET,
        SHIFTLEFT,
        SHIFTRIGHT,
        LESSEQUAL,
        GREATEQUAL,
        STRING,

        LITERAL_EXPR,
        BINARY_EXPR,
        UNARY_EXPR,
        PAREN_EXPR,
        NAME_EXPR,
        ASSIGN_EXPR,
        EMPTY_EXPR,
        CALL_EXPR,

        TRUE_KEYWORD,
        FALSE_KEYWORD,
        AUTO_KEYWORD,
        LET_KEYWORD,
        IF_KEYWORD,
        ELSE_KEYWORD,
        WHILE_KEYWORD,
        FOR_KEYWORD,
        DO_KEYWORD,

        BLOCK_STATEMENT,
        EXPRESSION_STATEMENT,
        VARIABLE_DECLARATION_STATEMENT,
        IF_STATEMENT,
        WHILE_STATEMENT,
        FOR_STATEMENT,
        DO_WHILE_STATEMENT,
        GLOBAL_STATEMENT,
        // other
        COMPILATION_UNIT,
        FUNCTION_DECLARATION,
        ELSE_CLAUSE,
        PARAMETER,
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
                    var child = (Node)property.GetValue(this);
                    if (child != null)
                        yield return child;
                } else if (typeof(IEnumerable<Node>).IsAssignableFrom(property.PropertyType)) {
                    var values = (IEnumerable<Node>)property.GetValue(this);
                    foreach (var child in values) {
                        if (child != null)
                            yield return child;
                    }
                }
            }
        }

        public void WriteTo(TextWriter writer) {
            PrettyPrint(writer, this);
        }

        private void PrettyPrint(TextWriter writer, Node node, string indent = "", bool isLast = true) {
            bool isConsoleOut = writer == Console.Out;
            string marker = isLast ? "└─" : "├─";

            if (isConsoleOut)
                Console.ForegroundColor = ConsoleColor.DarkGray;

            writer.Write($"{indent}{marker}");

            if (isConsoleOut)
                Console.ForegroundColor = node is Token ? ConsoleColor.DarkBlue : ConsoleColor.Cyan;

            writer.Write(node.type);

            if (node is Token t && t.value != null)
                writer.Write($" {t.value}");

            writer.WriteLine();

            if (isConsoleOut)
                Console.ResetColor();

            indent += isLast ? "  " : "│ ";
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

        public Token GetLastToken() {
            if (this is Token t)
                return t;

            return GetChildren().Last().GetLastToken();
        }
    }

    internal sealed class Token : Node {
        public override SyntaxType type { get; }
        public int position { get; }
        public string text { get; }
        public object value { get; }
        public bool isMissing => text == null;
        public override TextSpan span => new TextSpan(position, text?.Length ?? 0);

        public Token(SyntaxType type_, int position_, string text_, object value_) {
            type = type_;
            position = position_;
            text = text_;
            value = value_;
        }
    }
}

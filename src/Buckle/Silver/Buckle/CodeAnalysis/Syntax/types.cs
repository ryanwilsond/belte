using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System;
using Buckle.CodeAnalysis.Text;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax {

    internal enum SyntaxType {
        Invalid,

        EOF_TOKEN,
        WHITESPACE_TOKEN,
        IDENTIFIER_TOKEN,
        NUMBER_TOKEN,
        PLUS_TOKEN,
        MINUS_TOKEN,
        ASTERISK_TOKEN,
        SLASH_TOKEN,
        LPAREN_TOKEN,
        RPAREN_TOKEN,
        LBRACE_TOKEN,
        RBRACE_TOKEN,
        EQUALS_TOKEN,
        BANG_TOKEN,
        AMPERSAND_TOKEN,
        PIPE_TOKEN,
        TILDE_TOKEN,
        CARET_TOKEN,
        DAMPERSAND_TOKEN,
        DPIPE_TOKEN,
        SEMICOLON_TOKEN,
        COMMA_TOKEN,
        DASTERISK_TOKEN,
        DEQUALS_TOKEN,
        BANGEQUALS_TOKEN,
        LANGLEBRACKET_TOKEN,
        RANGLEBRACKET_TOKEN,
        SHIFTLEFT_TOKEN,
        SHIFTRIGHT_TOKEN,
        LESSEQUAL_TOKEN,
        GREATEQUAL_TOKEN,
        STRING_TOKEN,

        LITERAL_EXPRESSION,
        BINARY_EXPRESSION,
        UNARY_EXPRESSION,
        PAREN_EXPRESSION,
        NAME_EXPRESSION,
        ASSIGN_EXPRESSION,
        EMPTY_EXPRESSION,
        CALL_EXPRESSION,

        TRUE_KEYWORD,
        FALSE_KEYWORD,
        AUTO_KEYWORD,
        LET_KEYWORD,
        IF_KEYWORD,
        ELSE_KEYWORD,
        WHILE_KEYWORD,
        FOR_KEYWORD,
        DO_KEYWORD,
        BREAK_KEYWORD,
        CONTINUE_KEYWORD,
        RETURN_KEYWORD,

        BLOCK_STATEMENT,
        EXPRESSION_STATEMENT,
        VARIABLE_DECLARATION_STATEMENT,
        IF_STATEMENT,
        WHILE_STATEMENT,
        FOR_STATEMENT,
        DO_WHILE_STATEMENT,
        GLOBAL_STATEMENT,
        BREAK_STATEMENT,
        CONTINUE_STATEMENT,
        RETURN_STATEMENT,

        COMPILATION_UNIT,
        FUNCTION_DECLARATION,
        ELSE_CLAUSE,
        PARAMETER,
    }

    internal abstract class Node {
        public abstract SyntaxType type { get; }
        public SyntaxTree syntaxTree { get; }
        public virtual TextSpan span {
            get {
                var first = GetChildren().First().span;
                var last = GetChildren().Last().span;
                return TextSpan.FromBounds(first.start, last.end);
            }
        }

        protected Node(SyntaxTree syntaxTree_) {
            syntaxTree = syntaxTree_;
        }

        public TextLocation location => new TextLocation(syntaxTree.text, span);

        public abstract IEnumerable<Node> GetChildren();

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

        public Token(SyntaxTree syntaxTree, SyntaxType type_, int position_, string text_, object value_)
            : base(syntaxTree) {
            type = type_;
            position = position_;
            text = text_;
            value = value_;
        }

        public override IEnumerable<Node> GetChildren() {
            return Array.Empty<Node>();
        }
    }

    internal sealed partial class CompilationUnit : Node {
        public ImmutableArray<Member> members { get; }
        public Token endOfFile { get; }
        public override SyntaxType type => SyntaxType.COMPILATION_UNIT;

        public CompilationUnit(SyntaxTree syntaxTree, ImmutableArray<Member> members_, Token endOfFile_)
            : base(syntaxTree) {
            members = members_;
            endOfFile = endOfFile_;
        }
    }
}

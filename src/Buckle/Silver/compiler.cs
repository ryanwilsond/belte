using System;
using System.Linq;
using System.Collections.Generic;

namespace Buckle {

    public enum CompilerStage {
        raw,
        preprocessed,
        compiled,
        assembled,
        linked,
    }

    public struct FileContent {
        public List<string> lines;
        public List<byte> bytes;
    }

    public struct FileState {
        public string in_filename;
        public CompilerStage stage;
        public string out_filename;
        public FileContent file_content;
    }

    public struct CompilerState {
        public CompilerStage finish_stage;
        public string link_output_filename;
        public List<byte> link_output_content;
        public List<FileState> tasks;
    }

    public enum DiagnosticType {
        error,
        warning,
        fatal,
        unknown,
    }

    public class Diagnostic {
        public DiagnosticType type { get; }
        public string msg { get; }

        public Diagnostic(DiagnosticType type_, string msg_) {
            type = type_;
            msg = msg_;
        }
    }

    enum SyntaxType {
        Invalid,
        EOF,
        NUMBER,
        WHITESPACE,
        PLUS,
        MINUS,
        ASTERISK,
        SOLIDUS,
        LPAREN,
        RPAREN,
        NUMBER_EXPR,
        BINARY_EXPR,
        UNARY_EXPR,
        PAREN_EXPR,
    }

    abstract class Node {
        public abstract SyntaxType type { get; }
        public abstract List<Node> GetChildren();
    }

    class Token : Node {
        public override SyntaxType type { get; }
        public int pos { get; }
        public string text { get; }
        public object value { get; }

        public Token(SyntaxType type_, int pos_, string text_, object value_) {
            type = type_;
            pos = pos_;
            text = text_;
            value = value_;
        }

        public override List<Node> GetChildren() { return new List<Node>() { }; }
    }

    abstract class Expression : Node { }

    class NumberNode : Expression {
        public Token token { get; }
        public override SyntaxType type => SyntaxType.NUMBER_EXPR;

        public NumberNode(Token token_) {
            token = token_;
        }

        public override List<Node> GetChildren() { return new List<Node>() { token }; }
    }

    class BinaryExpression : Expression {
        public Expression left { get; }
        public Token op { get; }
        public Expression right { get; }
        public override SyntaxType type => SyntaxType.BINARY_EXPR;

        public BinaryExpression(Expression left_, Token op_, Expression right_) {
            left = left_;
            op = op_;
            right = right_;
        }

        public override List<Node> GetChildren() { return new List<Node>() { left, op, right }; }
    }

    class ParenExpression : Expression {
        public Token lparen { get; }
        public Expression expr { get; }
        public Token rparen { get; }
        public override SyntaxType type => SyntaxType.PAREN_EXPR;

        public ParenExpression(Token lparen_, Expression expr_, Token rparen_) {
            lparen = lparen_;
            expr = expr_;
            rparen = rparen_;
        }

        public override List<Node> GetChildren() { return new List<Node>() { lparen, expr, rparen }; }
    }

    class SyntaxTree {
        public Expression root { get; }
        public Token eof { get; }
        public List<Diagnostic> diagnostics;

        public SyntaxTree(Expression root_, Token eof_, List<Diagnostic> diagnostics_) {
            root = root_;
            eof = eof_;
            diagnostics = new List<Diagnostic>();
            diagnostics.AddRange(diagnostics_);
        }

        public static SyntaxTree Parse(string line) {
            Parser parser = new Parser(line);
            return parser.Parse();
        }
    }

    class Lexer {
        private readonly string text_;
        private int pos_;
        public List<Diagnostic> diagnostics;

        public Lexer(string text) {
            text_ = text;
            diagnostics = new List<Diagnostic>();
        }

        private char current {
            get {
                if (pos_ >= text_.Length) return '\0';
                return text_[pos_];
            }
        }

        private void Advance() { pos_++; }

        public Token Next() {
            if (pos_ >= text_.Length) return new Token(SyntaxType.EOF, pos_, "\0", null);

            if (char.IsDigit(current)) {
                int start = pos_;

                while (char.IsDigit(current))
                    Advance();

                int length = pos_ - start;
                string text = text_.Substring(start, length);

                if (!int.TryParse(text, out var value))
                    diagnostics.Add(new Diagnostic(DiagnosticType.error, $"'{value}' is not a valid integer"));

                return new Token(SyntaxType.NUMBER, start, text, value);
            } else if (char.IsWhiteSpace(current)) {
                int start = pos_;

                while (char.IsWhiteSpace(current))
                    Advance();

                int length = pos_ - start;
                string text = text_.Substring(start, length);
                return new Token(SyntaxType.WHITESPACE, start, text, null);
            } else if (current == '+') return new Token(SyntaxType.PLUS, pos_++, "+", null);
            else if (current == '-') return new Token(SyntaxType.MINUS, pos_++, "-", null);
            else if (current == '*') return new Token(SyntaxType.ASTERISK, pos_++, "*", null);
            else if (current == '/') return new Token(SyntaxType.SOLIDUS, pos_++, "/", null);
            else if (current == '(') return new Token(SyntaxType.LPAREN, pos_++, "(", null);
            else if (current == ')') return new Token(SyntaxType.RPAREN, pos_++, ")", null);

            diagnostics.Add(new Diagnostic(DiagnosticType.error, $"bad input character '{current}'"));
            return new Token(SyntaxType.Invalid, pos_++, text_.Substring(pos_-1,1), null);
        }
    }

    class Parser {
        private readonly Token[] tokens_;
        private int pos_;
        public List<Diagnostic> diagnostics;

        public Parser(string text) {
            diagnostics = new List<Diagnostic>();
            var tokens = new List<Token>();
            Lexer lexer = new Lexer(text);
            Token token;

            do {
                token = lexer.Next();

                if (token.type != SyntaxType.WHITESPACE && token.type != SyntaxType.Invalid)
                    tokens.Add(token);
            } while (token.type != SyntaxType.EOF);

            tokens_ = tokens.ToArray();
            diagnostics.AddRange(lexer.diagnostics);
        }

        public SyntaxTree Parse() {
            if (diagnostics.Any())
                return new SyntaxTree(null, null, diagnostics);

            var expr = ParseTerm();
            var eof = Match(SyntaxType.EOF);
            return new SyntaxTree(expr, eof, diagnostics);
        }

        public Expression ParseTerm() {
            var left = ParseFactor();

            while (current.type == SyntaxType.PLUS || current.type == SyntaxType.MINUS) {
                var op = Next();
                var right = ParseFactor();
                left = new BinaryExpression(left, op, right);
            }

            return left;
        }

        public Expression ParseFactor() {
            var left = ParsePrimaryExpression();

            while (current.type == SyntaxType.ASTERISK || current.type == SyntaxType.SOLIDUS) {
                var op = Next();
                var right = ParsePrimaryExpression();
                left = new BinaryExpression(left, op, right);
            }

            return left;
        }

        private Expression ParsePrimaryExpression() {
            if (current.type == SyntaxType.LPAREN) {
                var left = Next();
                var expr = ParseTerm();
                var right = Match(SyntaxType.RPAREN);
                return new ParenExpression(left, expr, right);
            }

            var token = Match(SyntaxType.NUMBER);
            return new NumberNode(token);
        }

        private Token Match(SyntaxType type) {
            if (current.type == type) return Next();
            diagnostics.Add(new Diagnostic(DiagnosticType.error, $"expected token of type '{type}', got '{current.type}'"));
            return new Token(type, current.pos, null, null);
        }

        private Token Next() {
            Token cur = current;
            pos_++;
            return cur;
        }

        private Token Peek(int offset) {
            int index = pos_ + offset;
            if (index >= tokens_.Length) return tokens_[tokens_.Length-1];
            return tokens_[index];
        }

        private Token current => Peek(0);
    }

    class Evaluator {
        private readonly Expression root_;
        public List<Diagnostic> diagnostics;

        public Evaluator(Expression root) {
            root_ = root;
            diagnostics = new List<Diagnostic>();
        }

        public int? Evaluate() { return EvaluteExpression(root_); }

        private int? EvaluteExpression(Expression node) {
            if (node is NumberNode n) {
                return (int)n.token.value;
            } else if (node is BinaryExpression b) {
                var left = EvaluteExpression(b.left);
                var right = EvaluteExpression(b.right);

                switch (b.op.type) {
                    case SyntaxType.PLUS: return left + right;
                    case SyntaxType.MINUS: return left - right;
                    case SyntaxType.ASTERISK: return left * right;
                    case SyntaxType.SOLIDUS: return left / right;
                    default:
                        diagnostics.Add(new Diagnostic(DiagnosticType.fatal, $"unknown binary operator '{b.op.type}'"));
                        return null;
                }
            } else if (node is ParenExpression p) {
                return EvaluteExpression(p.expr);
            }

            diagnostics.Add(new Diagnostic(DiagnosticType.fatal, $"unexpected node '{node.type}'"));
            return null;
        }
    }

    public class Compiler {
        const int SUCCESS_EXIT_CODE = 0;
        const int ERROR_EXIT_CODE = 1;
        const int FATAL_EXIT_CODE = 2;

        public CompilerState state;
        public string me;
        public List<Diagnostic> diagnostics;

        public Compiler() {
            diagnostics = new List<Diagnostic>();
        }

        private int CheckErrors() {
            foreach (Diagnostic diagnostic in diagnostics) {
                if (diagnostic.type == DiagnosticType.error) return ERROR_EXIT_CODE;
            }
            return SUCCESS_EXIT_CODE;
        }

        private void ExternalAssembler() {
            diagnostics.Add(new Diagnostic(DiagnosticType.warning, "assembling not supported (yet); skipping"));
        }

        private void ExternalLinker() {
            diagnostics.Add(new Diagnostic(DiagnosticType.warning, "linking not supported (yet); skipping"));
        }

        private void Preprocess() {
            diagnostics.Add(new Diagnostic(DiagnosticType.warning, "preprocessing not supported (yet); skipping"));
        }

        private void PrettyPrint(Node node, string indent = "", bool islast=true) {
            string marker = islast ? "└─" : "├─";
            Console.Write($"{indent}{marker}{node.type}");

            if (node is Token t && t.value != null)
                Console.Write($" {t.value}");

            Console.WriteLine();
            indent += islast ? "  " : "│ ";
            var lastChild = node.GetChildren().LastOrDefault();

            foreach (var child in node.GetChildren())
                PrettyPrint(child, indent, child == lastChild);
        }

        public delegate int ErrorHandle(Compiler compiler);

        private void Repl(ErrorHandle callback) {
            state.link_output_content = null;
            diagnostics.Clear();

            while (true) {
                Console.Write("> ");
                string line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) return;
                SyntaxTree tree = SyntaxTree.Parse(line);

                diagnostics.AddRange(tree.diagnostics);
                if (diagnostics.Any()) {
                    if (callback != null)
                        callback(this);
                    diagnostics.Clear();
                    continue;
                }

                ConsoleColor prev = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.DarkGray;
                PrettyPrint(tree.root);
                Console.ForegroundColor = prev;

                Evaluator eval = new Evaluator(tree.root);
                var result = eval.Evaluate();

                if (eval.diagnostics.Any()) {
                    if (callback != null)
                        callback(this);
                } else if (result != null) {
                    Console.WriteLine(result);
                }
            }
        }

        public int Compile(ErrorHandle callback=null) {
            int err;

            Preprocess();
            err = CheckErrors();
            if (err > 0) return err;

            if (state.finish_stage == CompilerStage.preprocessed)
                return SUCCESS_EXIT_CODE;

            // InternalCompiler();
            Repl(callback);
            err = CheckErrors();
            // if (err > 0) return err;
            return err;

            /*
            if (state.finish_stage == CompilerStage.compiled)
                return SUCCESS_EXIT_CODE;

            ExternalAssembler();
            err = CheckErrors();
            if (err > 0) return err;

            if (state.finish_stage == CompilerStage.assembled)
                return SUCCESS_EXIT_CODE;

            ExternalLinker();
            err = CheckErrors();
            if (err > 0) return err;

            if (state.finish_stage == CompilerStage.linked)
                return SUCCESS_EXIT_CODE;

            return FATAL_EXIT_CODE;
            // */
        }
    }
}

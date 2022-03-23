using System;
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

    enum TokenType {
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
    }

    class Token {
        public TokenType type { get; }
        public int pos { get; }
        public string text { get; }
        public object value { get; }

        public Token(TokenType type_, int pos_, string text_, object value_) {
            type = type_;
            pos = pos_;
            text = text_;
            value = value_;
        }
    }

    class Lexer {
        private readonly string text_;
        private int pos_;

        public Lexer(string text) {
            text_ = text;
        }

        private char current {
            get {
                if (pos_ >= text_.Length) return '\0';
                return text_[pos_];
            }
        }

        private void Advance() { pos_++; }

        public Token Next() {
            if (pos_ >= text_.Length) return new Token(TokenType.EOF, pos_, "\0", null);

            if (char.IsDigit(current)) {
                int start = pos_;

                while (char.IsDigit(current))
                    Advance();

                int length = pos_ - start;
                string text = text_.Substring(start, length);
                int.TryParse(text, out int value);
                return new Token(TokenType.NUMBER, start, text, value);
            } else if (char.IsWhiteSpace(current)) {
                int start = pos_;

                while (char.IsWhiteSpace(current))
                    Advance();

                int length = pos_ - start;
                string text = text_.Substring(start, length);
                return new Token(TokenType.WHITESPACE, start, text, null);
            } else if (current == '+') return new Token(TokenType.PLUS, pos_++, "+", null);
            else if (current == '-') return new Token(TokenType.MINUS, pos_++, "-", null);
            else if (current == '*') return new Token(TokenType.ASTERISK, pos_++, "*", null);
            else if (current == '/') return new Token(TokenType.SOLIDUS, pos_++, "/", null);
            else if (current == '(') return new Token(TokenType.LPAREN, pos_++, "(", null);
            else if (current == ')') return new Token(TokenType.RPAREN, pos_++, ")", null);

            return new Token(TokenType.Invalid, pos_++, text_.Substring(pos_-1,1), null);
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

        private void Repl() {
            state.link_output_content = null;

            while (true) {
                Console.Write("> ");
                string line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) return;
                Lexer lexer = new Lexer(line);

                while (true) {
                    Token token = lexer.Next();
                    if (token.type == TokenType.EOF) break;
                    Console.Write($"{token.type}: '{token.text}'");
                    if (token.value != null) Console.Write($" {token.value}");
                    Console.WriteLine();
                }

            }
        }

        public int Compile() {
            int err;

            Preprocess();
            err = CheckErrors();
            if (err > 0) return err;

            if (state.finish_stage == CompilerStage.preprocessed)
                return SUCCESS_EXIT_CODE;

            Repl();
            err = CheckErrors();
            if (err > 0) return err;

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
        }
    }
}

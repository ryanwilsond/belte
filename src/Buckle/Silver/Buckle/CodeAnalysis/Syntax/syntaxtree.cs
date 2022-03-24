using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax {

    internal class SyntaxTree {
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
}

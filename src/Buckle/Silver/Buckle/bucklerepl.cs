using System;
using System.Linq;
using Buckle.CodeAnalysis.Syntax;
using CommandLine;

namespace Buckle {
    internal sealed class BuckleReplState : ReplState {
        public bool showTree = false;
        public bool showProgram = false;
    }

    public sealed class BuckleRepl : Repl {
        internal override ReplState state_ { get; set; }
        internal BuckleReplState state { get { return (BuckleReplState)state_; } set { state_=value; } }

        public BuckleRepl(Compiler handle, ErrorHandle errorHandle) : base(handle, errorHandle) {
            initializerStatements_.Add("#clear");
            state = new BuckleReplState();
        }

        internal override void ResetState() {
            state.showTree = false;
            state.showProgram = false;
            base.ResetState();
        }

        protected override void EvaluateSubmission(string text) {
            var syntaxTree = SyntaxTree.Parse(text);

            var compilation = state.previous == null
                ? new Compilation(syntaxTree)
                : state.previous.ContinueWith(syntaxTree);

            if (state.showTree) syntaxTree.root.WriteTo(Console.Out);
            if (state.showProgram) compilation.EmitTree(Console.Out);

            var result = compilation.Evaluate(state.variables);

            handle.diagnostics.Move(result.diagnostics);
            if (handle.diagnostics.Any()) {
                if (errorHandle != null) {
                    handle.diagnostics = DiagnosticQueue.CleanDiagnostics(handle.diagnostics);
                    errorHandle(handle);
                } else {
                    handle.diagnostics.Clear();
                }
            } else {
                if (result.value != null) {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(result.value);
                    Console.ResetColor();
                }

                state.previous = compilation;
            }
        }

        protected override void RenderLine(string line) {
            var tokens = SyntaxTree.ParseTokens(line);
            for (int i=0; i<tokens.Length; i++) {
                var token = tokens[i];
                var isKeyword = token.type.ToString().EndsWith("_KEYWORD");
                var isNumber = token.type == SyntaxType.NUMBER;
                var isIdentifier = token.type == SyntaxType.IDENTIFIER;
                var isString = token.type == SyntaxType.STRING;
                var isType = (i < tokens.Length-2) && (tokens[i+1].type == SyntaxType.WHITESPACE) &&
                    (tokens[i+2].type == SyntaxType.IDENTIFIER) && isIdentifier;

                if (isKeyword)
                    Console.ForegroundColor = ConsoleColor.Blue;
                else if (isNumber)
                    Console.ForegroundColor = ConsoleColor.Cyan;
                else if (isString)
                    Console.ForegroundColor = ConsoleColor.Yellow;
                else if (isType)
                    Console.ForegroundColor = ConsoleColor.Blue;
                else if (!isIdentifier)
                    Console.ForegroundColor = ConsoleColor.DarkGray;

                Console.Write(token.text);
                Console.ResetColor();
            }
        }

        [MetaCommand("showTree", "Toggle to display parse tree of each input")]
        private void EvaluateShowTree() {
            state.showTree = !state.showTree;
            Console.WriteLine(state.showTree ? "Parse-trees visible" : "Parse-trees hidden");
        }

        [MetaCommand("showProgram", "Toggle to display intermediate representation of each input")]
        private void EvaluateShowProgram() {
            state.showProgram = !state.showProgram;
            Console.WriteLine(state.showProgram ? "Bound-trees visible" : "Bound-trees hidden");
        }

        [MetaCommand("clear", "Clears the screen")]
        private void EvaluateClear() {
            Console.Clear();
        }

        [MetaCommand("reset", "Clears previous submissions")]
        private void EvaluateReset() {
            ResetState();
            EvaluateClear();
        }

        protected override bool IsCompleteSubmission(string text) {
            if (string.IsNullOrEmpty(text)) return true;

            var twoBlankTines = text.Split(Environment.NewLine).Reverse()
                .TakeWhile(s => string.IsNullOrEmpty(s))
                .Take(2)
                .Count() == 2;

            if (twoBlankTines) return true;

            var tree = SyntaxTree.Parse(text);
            if (tree.root.members.Last().GetLastToken().isMissing) return false;

            return true;
        }
    }
}

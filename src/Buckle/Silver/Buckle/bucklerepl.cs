using System;
using System.Linq;
using Buckle.CodeAnalysis.Syntax;
using CommandLine;

namespace Buckle {

    public sealed class BuckleRepl : Repl {
        public BuckleRepl(Compiler handle, ErrorHandle errorHandle) : base(handle, errorHandle) {
            initializerStatements_.Add("#cls");
        }

        protected override void EvaluateSubmission(string text) {
            var syntaxTree = SyntaxTree.Parse(text);

            var compilation = state.previous == null
                ? new Compilation(syntaxTree)
                : state.previous.ContinueWith(syntaxTree);

            handle.state.sourceText = compilation.tree.text;

            if (state.showTree) syntaxTree.root.WriteTo(Console.Out);
            if (state.showProgram) compilation.EmitTree(Console.Out);

            var result = compilation.Evaluate(state.variables);

            handle.diagnostics.Move(result.diagnostics);
            if (handle.diagnostics.Any()) {
                if (errorHandle != null)
                    errorHandle(handle);
                else
                    handle.diagnostics.Clear();
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
            foreach (var token in tokens) {
                var isKeyword = token.type.ToString().EndsWith("_KEYWORD");
                var isNumber = token.type == SyntaxType.NUMBER;
                var isIdentifier = token.type == SyntaxType.IDENTIFIER;
                var isString = token.type == SyntaxType.STRING;

                if (isKeyword)
                    Console.ForegroundColor = ConsoleColor.Blue;
                else if (isNumber)
                    Console.ForegroundColor = ConsoleColor.Cyan;
                else if (isString)
                    Console.ForegroundColor = ConsoleColor.Yellow;
                else if (!isIdentifier)
                    Console.ForegroundColor = ConsoleColor.DarkGray;

                Console.Write(token.text);
                Console.ResetColor();
            }
        }

        protected override void EvaluateReplCommand(string line) {
            switch (line) {
                case "#showTree":
                    state.showTree = !state.showTree;
                    Console.WriteLine(state.showTree ? "Parse-trees visible" : "Parse-trees hidden");
                    break;
                case "#showProgram":
                    state.showProgram = !state.showProgram;
                    Console.WriteLine(state.showProgram ? "Bound-trees visible" : "Bound-trees hidden");
                    break;
                case "#clear":
                case "#cls":
                    Console.Clear();
                    break;
                case "#reset":
                    state.previous = null;
                    break;
                default:
                    base.EvaluateReplCommand(line);
                    break;
            }
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

using System;
using System.Collections.Generic;
using System.Text;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle {
    internal struct ReplState {
        public Compilation previous;
        public StringBuilder input;
        public bool showTree;
        public bool showProgram;
        public Dictionary<VariableSymbol, object> variables;
    }

    internal abstract class Repl {
        internal Compiler handle;
        internal Compiler.ErrorHandle errorHandle;
        internal ReplState state;

        public Repl(Compiler handle_, Compiler.ErrorHandle errorHandle_) {
            handle = handle_;
            errorHandle = errorHandle_;
            state = new ReplState();
        }

        private void ResetState() {
            state.showProgram = false;
            state.showTree = false;
            state.variables = new Dictionary<VariableSymbol, object>();
            state.previous = null;
            state.input = new StringBuilder();
        }

        public void Run() {
            ResetState();

            while (true) {
                Console.ForegroundColor = ConsoleColor.Green;

                if (state.input.Length == 0)
                    Console.Write("» ");
                else
                    Console.Write("· ");

                Console.ResetColor();

                string line = Console.ReadLine();
                bool isBlank = string.IsNullOrWhiteSpace(line);

                if (state.input.Length == 0) {
                    if (isBlank) {
                        break;
                    } else if (line.StartsWith('#')) {
                        EvaluateReplCommand(line);
                        continue;
                    }
                }

                state.input.AppendLine(line);
                string text = state.input.ToString();
                if (!IsCompleteSubmission(text) && !isBlank) continue;

                EvaluateSubmission(text);

                state.input.Clear();
            }
        }

        protected virtual void EvaluateReplCommand(string line) {
            handle.diagnostics.Push(DiagnosticType.Error, $"unknown repl command '{line}'");

            if (errorHandle != null)
                errorHandle(handle, "repl");
            else
                handle.diagnostics.Clear();
        }

        protected abstract bool IsCompleteSubmission(string text);
        protected abstract void EvaluateSubmission(string text);
    }

    internal sealed class BuckleRepl : Repl {
        public BuckleRepl(Compiler handle, Compiler.ErrorHandle errorHandle) : base(handle, errorHandle) {}

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
            if (handle.diagnostics.Any())  {
                if (errorHandle != null)
                    errorHandle(handle);
                else
                    handle.diagnostics.Clear();
            } else {
                if (result.value != null) {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine(result.value);
                    Console.ResetColor();
                }

                state.previous = compilation;
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
            if (string.IsNullOrEmpty(text)) return false;

            var tree = SyntaxTree.Parse(text);
            if (tree.diagnostics.Any()) return false;

            return true;
        }
    }
}

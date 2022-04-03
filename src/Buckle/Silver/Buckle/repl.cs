using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle {
    internal struct ReplState {
        public Compilation previous;
        public bool showTree;
        public bool showProgram;
        public Dictionary<VariableSymbol, object> variables;
        public bool done;
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
            state.done = false;
        }

        public void Run() {
            ResetState();

            while (true) {
                string text = EditSubmission();
                if (string.IsNullOrEmpty(text)) return;

                EvaluateSubmission(text);
            }
        }

        private sealed class SubmissionView {
            private readonly ObservableCollection<string> document_;
            private readonly int cursorTop_;
            private int renderedLineCount_;
            private int currentLineIndex_;
            private int curretCharacter_;

            public int currentLineIndex {
                get => currentLineIndex_;
                set {
                    if (currentLineIndex_ != value) {
                        currentLineIndex_ = value;
                        UpdateCursorPosition();
                    }
                }
            }
            public int currentCharacter {
                get => curretCharacter_;
                set {
                    if (curretCharacter_ != value) {
                        curretCharacter_ = value;
                        UpdateCursorPosition();
                    }
                }
            }

            public SubmissionView(ObservableCollection<string> document) {
                document_ = document;
                document_.CollectionChanged += SubmissionDocumentChanged;
                cursorTop_ = Console.CursorTop;
                Render();
            }

            private void SubmissionDocumentChanged(object sender, NotifyCollectionChangedEventArgs e) {
                Render();
            }

            private void Render() {
                Console.CursorVisible = false;
                int lineCount = 0;

                foreach (var line in document_) {
                    Console.SetCursorPosition(0, cursorTop_ + lineCount);
                    Console.ForegroundColor = ConsoleColor.Green;

                    if (lineCount == 0)
                        Console.Write("» ");
                    else
                        Console.Write("· ");

                    Console.ResetColor();
                    Console.WriteLine(line);
                    lineCount++;
                }

                var blankLineCount = renderedLineCount_ - lineCount;

                string blankLine = new string(' ', Console.WindowWidth);
                for (int i=0; i<blankLineCount; i++) {
                    Console.SetCursorPosition(0, cursorTop_ + lineCount + i);
                    Console.WriteLine(blankLine);
                }

                renderedLineCount_ = lineCount;
                Console.CursorVisible = true;
                UpdateCursorPosition();
            }

            private void UpdateCursorPosition() {
                Console.CursorTop = cursorTop_ + currentLineIndex_;
                Console.CursorLeft = 2 + curretCharacter_; // accounts for repl indentation
            }
        }

        private string EditSubmission() {
            state.done = false;

            var document = new ObservableCollection<string>() { "" };
            var view = new SubmissionView(document);

            while (!state.done) {
                var key = Console.ReadKey(true);
                HandleKey(key, document, view);
            }

            Console.WriteLine();

            return string.Join(Environment.NewLine, document);
        }

        private void HandleKey(ConsoleKeyInfo key, ObservableCollection<string> document, SubmissionView view) {
            if (key.Modifiers == default(ConsoleModifiers)) {
                switch (key.Key) {
                    case ConsoleKey.Enter:
                        HandleEnter(document, view);
                        break;
                    case ConsoleKey.LeftArrow:
                        HandleLeftArrow(document, view);
                        break;
                    case ConsoleKey.RightArrow:
                        HandleRightArrow(document, view);
                        break;
                    case ConsoleKey.UpArrow:
                        HandleUpArrow(document, view);
                        break;
                    case ConsoleKey.DownArrow:
                        HandleDownArrow(document, view);
                        break;
                    default:
                        break;
                }
            } else if (key.Modifiers == ConsoleModifiers.Control) {
                switch (key.Key) {
                    case ConsoleKey.Enter:
                        HandleControlEnter(document, view);
                        break;
                    default:
                        break;
                }
            }

            if (key.KeyChar >= ' ')
                HandleTyping(document, view, key.KeyChar.ToString());
        }

        private void HandleControlEnter(ObservableCollection<string> document, SubmissionView view) {
            state.done = true;
        }

        private void HandleTyping(ObservableCollection<string> document, SubmissionView view, string text) {
            var lineIndex = view.currentLineIndex;
            var start = view.currentCharacter;
            document[lineIndex] = document[lineIndex].Insert(start, text);
            view.currentCharacter += text.Length;
        }

        private void HandleDownArrow(ObservableCollection<string> document, SubmissionView view) {
            if (view.currentLineIndex < document.Count - 1)
                view.currentLineIndex++;
        }

        private void HandleUpArrow(ObservableCollection<string> document, SubmissionView view) {
            if (view.currentLineIndex > 0)
                view.currentLineIndex--;
        }

        private void HandleRightArrow(ObservableCollection<string> document, SubmissionView view) {
            var line = document[view.currentLineIndex];

            if (view.currentCharacter < line.Length - 1)
                view.currentCharacter++;
        }

        private void HandleLeftArrow(ObservableCollection<string> document, SubmissionView view) {
            if (view.currentCharacter > 0)
                view.currentCharacter--;
        }

        private void HandleEnter(ObservableCollection<string> document, SubmissionView view) {
            var submissionText = string.Join(Environment.NewLine, document);
            if (IsCompleteSubmission(submissionText)) {
                state.done = true;
                return;
            }

            document.Add(string.Empty);
            view.currentCharacter = 0;
            view.currentLineIndex = document.Count - 1;
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
            if (string.IsNullOrEmpty(text)) return true;

            var tree = SyntaxTree.Parse(text);
            if (tree.diagnostics.Any()) return false;

            return true;
        }
    }
}

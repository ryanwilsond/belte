using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Buckle;
using Buckle.CodeAnalysis.Symbols;
using System.Linq;

namespace CommandLine {
    internal struct ReplState {
        public Compilation previous;
        public bool showTree;
        public bool showProgram;
        public Dictionary<VariableSymbol, object> variables;
        public bool done;
    }

    public abstract class Repl {
        public delegate int ErrorHandle(Compiler compiler, string me = null);
        private List<string> submissionHistory_ = new List<string>();
        private int submissionHistoryIndex_;
        internal Compiler handle;
        internal ErrorHandle errorHandle;
        internal ReplState state;
        protected List<string> initializerStatements_ = new List<string>();

        public Repl(Compiler handle_, ErrorHandle errorHandle_) {
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
                string text;

                if (initializerStatements_.Any()) {
                    text = initializerStatements_[0];
                    initializerStatements_.RemoveAt(0);
                } else {
                    text = EditSubmission();
                }

                if (string.IsNullOrEmpty(text)) return;

                if (!text.Contains(Environment.NewLine) && text.StartsWith('#'))
                    EvaluateReplCommand(text);
                else
                    EvaluateSubmission(text);

                submissionHistory_.Add(text);
                submissionHistoryIndex_ = 0;
            }
        }

        private sealed class SubmissionView {
            private readonly Action<string> lineRenderer_;
            private readonly ObservableCollection<string> document_;
            private readonly int cursorTop_;
            private int renderedLineCount_;
            private int currentLine_;
            private int currentCharacter_;

            public int currentLine {
                get => currentLine_;
                set {
                    if (currentLine_ != value) {
                        currentLine_ = value;
                        currentCharacter_ = Math.Min(document_[currentLine_].Length, currentCharacter_);
                        UpdateCursorPosition();
                    }
                }
            }
            public int currentCharacter {
                get => currentCharacter_;
                set {
                    if (currentCharacter_ != value) {
                        currentCharacter_ = value;
                        UpdateCursorPosition();
                    }
                }
            }

            public SubmissionView(Action<string> lineRenderer, ObservableCollection<string> document) {
                lineRenderer_ = lineRenderer;
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
                    lineRenderer_(line);
                    Console.Write(new string(' ', Console.WindowWidth - line.Length));
                    lineCount++;
                }

                var blankLineCount = renderedLineCount_ - lineCount;

                if (blankLineCount > 0) {
                    string blankLine = new string(' ', Console.WindowWidth);
                    for (int i = 0; i < blankLineCount; i++) {
                        Console.SetCursorPosition(0, cursorTop_ + lineCount + i);
                        Console.WriteLine(blankLine);
                    }
                }

                renderedLineCount_ = lineCount;
                Console.CursorVisible = true;
                UpdateCursorPosition();
            }

            private void UpdateCursorPosition() {
                Console.CursorTop = cursorTop_ + currentLine_;
                Console.CursorLeft = 2 + currentCharacter_; // accounts for repl indentation
            }
        }

        private string EditSubmission() {
            state.done = false;

            var document = new ObservableCollection<string>() { "" };
            var view = new SubmissionView(RenderLine, document);

            while (!state.done) {
                var key = Console.ReadKey(true);
                HandleKey(key, document, view);
            }

            view.currentLine = document.Count - 1;
            view.currentCharacter = document[view.currentLine].Length;

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
                    case ConsoleKey.Backspace:
                        HandleBackspace(document, view);
                        break;
                    case ConsoleKey.Delete:
                        HandleDelete(document, view);
                        break;
                    case ConsoleKey.Home:
                        HandleHome(document, view);
                        break;
                    case ConsoleKey.End:
                        HandleEnd(document, view);
                        break;
                    case ConsoleKey.Tab:
                        HandleTab(document, view);
                        break;
                    case ConsoleKey.Escape:
                        HandleEscape(document, view);
                        break;
                    case ConsoleKey.PageUp:
                        HandlePageUp(document, view);
                        break;
                    case ConsoleKey.PageDown:
                        HandlePageDown(document, view);
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
            } else if (key.Modifiers == ConsoleModifiers.Shift) {
                switch (key.Key) {
                    case ConsoleKey.Enter:
                        InsertLine(document, view);
                        break;
                    default:
                        break;
                }
            }

            if (key.KeyChar >= ' ')
                HandleTyping(document, view, key.KeyChar.ToString());
        }

        private void HandlePageDown(ObservableCollection<string> document, SubmissionView view) {
            submissionHistoryIndex_++;
            if (submissionHistoryIndex_ > submissionHistory_.Count - 1)
                submissionHistoryIndex_ = 0;
            UpdateDocumentFromHistory(document, view);
        }

        private void HandlePageUp(ObservableCollection<string> document, SubmissionView view) {
            submissionHistoryIndex_--;
            if (submissionHistoryIndex_ < 0)
                submissionHistoryIndex_ = submissionHistory_.Count - 1;
            UpdateDocumentFromHistory(document, view);
        }

        private void UpdateDocumentFromHistory(ObservableCollection<string> document, SubmissionView view) {
            if (submissionHistory_.Count == 0)
                return;

            document.Clear();

            var historyItem = submissionHistory_[submissionHistoryIndex_];
            var lines = historyItem.Split(Environment.NewLine);
            foreach (var line in lines)
                document.Add(line);

            view.currentLine = document.Count - 1;
            view.currentCharacter = document[view.currentLine].Length;
        }

        private void HandleEscape(ObservableCollection<string> document, SubmissionView view) {
            document[view.currentLine] = string.Empty;
            view.currentCharacter = 0;
        }

        private void HandleTab(ObservableCollection<string> document, SubmissionView view) {
            const int tabWidth = 4;
            var start = view.currentCharacter;
            var remainingSpaces = tabWidth - start % tabWidth;
            var line = document[view.currentLine];
            document[view.currentLine] = line.Insert(start, new string(' ', remainingSpaces));
            view.currentCharacter += remainingSpaces;
        }

        private void HandleHome(ObservableCollection<string> document, SubmissionView view) {
            view.currentCharacter = 0;
        }

        private void HandleEnd(ObservableCollection<string> document, SubmissionView view) {
            view.currentCharacter = document[view.currentLine].Length;
        }

        private void HandleDelete(ObservableCollection<string> document, SubmissionView view) {

            var lineIndex = view.currentLine;
            var line = document[lineIndex];
            var start = view.currentCharacter;
            if (start >= line.Length) {
                if (view.currentLine == document.Count - 1)
                    return;

                var nextLine = document[view.currentLine + 1];
                document[view.currentLine] += nextLine;
                document.RemoveAt(view.currentLine + 1);
                return;
            }

            var before = line.Substring(0, start);
            var after = line.Substring(start + 1);
            document[lineIndex] = before + after;
        }

        private void HandleBackspace(ObservableCollection<string> document, SubmissionView view) {
            var start = view.currentCharacter;
            if (start == 0) {
                if (view.currentLine == 0) return;

                var currentLine = document[view.currentLine];
                var previousLine = document[view.currentLine - 1];
                document.RemoveAt(view.currentLine);
                view.currentLine--;
                document[view.currentLine] = previousLine + currentLine;
                view.currentCharacter = previousLine.Length;
            } else {
                var lineIndex = view.currentLine;
                var line = document[lineIndex];
                var before = line.Substring(0, start - 1);
                var after = line.Substring(start);
                document[lineIndex] = before + after;
                view.currentCharacter--;
            }
        }

        private void HandleControlEnter(ObservableCollection<string> document, SubmissionView view) {
            state.done = true;
        }

        private void HandleTyping(ObservableCollection<string> document, SubmissionView view, string text) {
            var lineIndex = view.currentLine;
            var start = view.currentCharacter;
            document[lineIndex] = document[lineIndex].Insert(start, text);
            view.currentCharacter += text.Length;
        }

        private void HandleDownArrow(ObservableCollection<string> document, SubmissionView view) {
            if (view.currentLine < document.Count - 1)
                view.currentLine++;
        }

        private void HandleUpArrow(ObservableCollection<string> document, SubmissionView view) {
            if (view.currentLine > 0)
                view.currentLine--;
        }

        private void HandleRightArrow(ObservableCollection<string> document, SubmissionView view) {
            var line = document[view.currentLine];

            if (view.currentCharacter <= line.Length - 1)
                view.currentCharacter++;
        }

        private void HandleLeftArrow(ObservableCollection<string> document, SubmissionView view) {
            if (view.currentCharacter > 0)
                view.currentCharacter--;
        }

        private void HandleEnter(ObservableCollection<string> document, SubmissionView view) {
            var submissionText = string.Join(Environment.NewLine, document);
            if (submissionText.StartsWith('#') || IsCompleteSubmission(submissionText)) {
                state.done = true;
                return;
            }

            InsertLine(document, view);
        }

        private static void InsertLine(ObservableCollection<string> document, SubmissionView view) {
            var remainder = document[view.currentLine].Substring(view.currentCharacter);
            document[view.currentLine] = document[view.currentLine].Substring(0, view.currentCharacter);

            var lineIndex = view.currentLine + 1;
            document.Insert(lineIndex, remainder);
            view.currentCharacter = 0;
            view.currentLine = lineIndex;
        }

        protected void ClearHistory() {
            submissionHistory_.Clear();
        }

        protected virtual void RenderLine(string line) {
            Console.Write(line);
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
}

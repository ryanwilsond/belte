using System;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Buckle;
using Buckle.Diagnostics;

namespace Belte.Repl;

public abstract class Repl {
    public delegate int DiagnosticHandle(Compiler compiler, string me = null);

    private readonly List<string> submissionHistory_ = new List<string>();
    private readonly List<MetaCommand> metaCommands_ = new List<MetaCommand>();
    private int submissionHistoryIndex_;
    private bool done_;
    internal Compiler handle;
    internal DiagnosticHandle diagnosticHandle;
    internal abstract object state_ { get; set; }

    protected Repl(Compiler handle_, DiagnosticHandle diagnosticHandle_) {
        handle = handle_;
        diagnosticHandle = diagnosticHandle_;
        InitializeMetaCommands();
    }

    private void InitializeMetaCommands() {
        var methods = GetType()
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static |
                BindingFlags.Instance | BindingFlags.FlattenHierarchy);

        foreach (var method in methods) {
            var attribute = method.GetCustomAttribute<MetaCommandAttribute>();
            if (attribute == null)
                continue;

            var metaComand = new MetaCommand(attribute.name, attribute.description, method);
            metaCommands_.Add(metaComand);
        }
    }

    internal virtual void ResetState() {
        done_ = false;
    }

    public void Run() {
        while (true) {
            var text = EditSubmission();

            if (string.IsNullOrEmpty(text))
                return;

            if (!text.Contains(Environment.NewLine) && text.StartsWith('#'))
                EvaluateReplCommand(text);
            else
                EvaluateSubmission(text);

            submissionHistory_.Add(text);
            submissionHistoryIndex_ = 0;
        }
    }

    private delegate object LineRenderHandler(IReadOnlyList<string> lines, int lineIndex, object state);

    private sealed class SubmissionView {
        private readonly LineRenderHandler lineRenderer_;
        private readonly ObservableCollection<string> document_;
        private int cursorTop_;
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

        public SubmissionView(LineRenderHandler lineRenderer, ObservableCollection<string> document) {
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
            var lineCount = 0;

            foreach (var line in document_) {
                if (cursorTop_ + lineCount >= Console.WindowHeight - 1) {
                    Console.SetCursorPosition(0, Console.WindowHeight - 1);
                    Console.WriteLine();

                    if (cursorTop_ > 0)
                        cursorTop_--;
                }

                Console.SetCursorPosition(0, cursorTop_ + lineCount);
                Console.ForegroundColor = ConsoleColor.Green;

                if (lineCount == 0)
                    Console.Write("» ");
                else
                    Console.Write("· ");

                Console.ResetColor();
                lineRenderer_(document_, lineCount, null);
                Console.Write(new string(' ', Console.WindowWidth - line.Length - 2));
                lineCount++;
            }

            var blankLineCount = renderedLineCount_ - lineCount;

            if (blankLineCount > 0) {
                var blankLine = new string(' ', Console.WindowWidth);

                for (int i=0; i<blankLineCount; i++) {
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
            Console.CursorLeft = 2 + currentCharacter_; // +2 comes from repl entry characters
        }
    }

    private string EditSubmission() {
        done_ = false;

        var document = new ObservableCollection<string>() { "" };
        var view = new SubmissionView(RenderLine, document);

        while (!done_) {
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
                    if (key.Modifiers == ConsoleModifiers.Shift)
                        InsertLine(document, view);
                    else
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
        }

        if (key.Key != ConsoleKey.Backspace && key.KeyChar >= ' ')
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
        document.Clear();
        document.Add(string.Empty);
        view.currentLine = 0;
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
            if (view.currentLine == 0)
                return;

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
        done_ = true;
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
            done_ = true;
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

    protected virtual object RenderLine(IReadOnlyList<string> lines, int lineIndex, object state) {
        Console.Write(lines[lineIndex]);
        return state;
    }

    private void EvaluateReplCommand(string line) {
        var position = 1;
        var sb = new StringBuilder();
        var inQuotes = false;
        var args = new List<string>();

        while (position < line.Length) {
            var c = line[position];
            var l = position + 1 >= line.Length ? '\0' : line[position + 1];

            if (char.IsWhiteSpace(c)) {
                if (!inQuotes) {
                    var arg = sb.ToString();
                    if (!string.IsNullOrWhiteSpace(arg))
                        args.Add(arg);

                    sb.Clear();
                } else
                    sb.Append(c);
            } else if (c == '\"') {
                if (!inQuotes)
                    inQuotes = true;
                else if (l == '\"') {
                    sb.Append(c);
                    position++;
                } else if (inQuotes)
                    inQuotes = false;
            } else {
                sb.Append(c);
            }

            position++;
        }

        args.Add(sb.ToString());

        var commandName = args.FirstOrDefault();

        if (args.Count > 0)
            args.RemoveAt(0);

        var command = metaCommands_.SingleOrDefault(mc => mc.name == commandName);

        if (command == null) {
            handle.diagnostics.Push(DiagnosticType.Error, $"unknown repl command '{line}'");

            if (diagnosticHandle != null)
                diagnosticHandle(handle, "repl");
            else
                handle.diagnostics.Clear();

            return;
        }

        var parameters = command.method.GetParameters();

        if (args.Count != parameters.Length) {
            var parameterNames = string.Join(" ", parameters.Select(p => $"<{p.Name}>"));
            handle.diagnostics.Push(
                DiagnosticType.Error, $"invalid number of arguments\nusage: #{command.name} {parameterNames}");

            if (diagnosticHandle != null)
                diagnosticHandle(handle, "repl");
            else
                handle.diagnostics.Clear();

            return;
        }

        var instance = command.method.IsStatic ? null : this;
        command.method.Invoke(instance, args.ToArray());
    }

    protected abstract bool IsCompleteSubmission(string text);
    protected abstract void EvaluateSubmission(string text);

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    protected sealed class MetaCommandAttribute : Attribute {
        public string name { get; }
        public string description { get; }

        public MetaCommandAttribute(string name_, string description_) {
            name = name_;
            description = description_;
        }
    }

    private sealed class MetaCommand {
        public string name { get; }
        public string description { get; set; }
        public MethodInfo method { get; }

        public MetaCommand(string name_, string description_, MethodInfo method_) {
            name = name_;
            method = method_;
            description = description_;
        }
    }

    [MetaCommand("help", "Shows this document")]
    protected void EvaluateHelp() {
        var maxLength = metaCommands_
            .Max(mc => mc.name.Length + string.Join(" ", mc.method.GetParameters()
            .SelectMany(p => p.Name).ToList()).Length);

        foreach (var metaCommand in metaCommands_.OrderBy(mc => mc.name)) {
            var name = metaCommand.name;

            foreach (var parameter in metaCommand.method.GetParameters())
                name += $" <{parameter.Name}>";

            var paddedName = name.PadRight(maxLength);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Out.Write("#");
            Console.ResetColor();
            Console.Out.Write(paddedName);
            Console.Out.Write("  ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Out.Write(metaCommand.description);
            Console.ResetColor();
            Console.Out.WriteLine();
        }
    }
}

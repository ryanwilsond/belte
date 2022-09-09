using System.Text;
using System.Reflection;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Buckle;
using Buckle.Diagnostics;

namespace Repl;

public abstract class ReplBase {
    public delegate int DiagnosticHandle(Compiler compiler, string me = null);

    private readonly List<string> submissionHistory_ = new List<string>();
    private readonly List<MetaCommand> metaCommands_ = new List<MetaCommand>();
    private int submissionHistoryIndex_;
    private bool done_;
    private OutputCapture writer_ = new OutputCapture();

    const int tabWidth = 4;

    internal Compiler handle;
    internal DiagnosticHandle diagnosticHandle;
    internal abstract object state_ { get; set; }

    protected ReplBase(Compiler handle_, DiagnosticHandle diagnosticHandle_) {
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

            var metaCommand = new MetaCommand(attribute.name, attribute.description, method);
            metaCommands_.Add(metaCommand);
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

    private class OutputCapture : TextWriter, IDisposable {
        private TextWriter outWriter_;
        private int offset_;
        internal TextWriter captured { get; private set; }
        public override Encoding Encoding { get { return Encoding.ASCII; } }

        internal OutputCapture() {
            outWriter_ = Console.Out;
            Console.SetOut(this);
            captured = new StringWriter();
        }

        public override void Write(string output) {
            var text = captured.ToString();
            captured.Flush();
            captured.Write(text.Substring(0, offset_));
            captured.Write(output);
            outWriter_.Write(output);
            offset_ += output.Length;
        }

        public override void WriteLine(string output) {
            var text = captured.ToString();
            captured.Flush();
            captured.Write(text.Substring(0, offset_));
            captured.WriteLine(output);
            outWriter_.WriteLine(output);
            offset_ += output.Length + 1;
        }

        public void SetCursorPosition(int left, int top) {
            var text = captured.ToString().Split('\n');

            for (int i=0; i<top; i++)
                offset_ += text[i].Length;

            offset_ += left;
            Console.SetCursorPosition(left, top);
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
        private OutputCapture writer_;

        internal int currentLine {
            get => currentLine_;
            set {
                if (currentLine_ != value) {
                    currentLine_ = value;
                    currentCharacter_ = Math.Min(document_[currentLine_].Length, currentCharacter_);
                    UpdateCursorPosition();
                }
            }
        }

        internal int currentCharacter {
            get => currentCharacter_;
            set {
                if (currentCharacter_ != value) {
                    currentCharacter_ = value;
                    UpdateCursorPosition();
                }
            }
        }

        internal Stack<(char, int)> currentBlockTabbing = new Stack<(char, int)>();
        internal int currentTypingTabbing = 0;

        internal SubmissionView(
            LineRenderHandler lineRenderer, ObservableCollection<string> document, OutputCapture writer) {
            lineRenderer_ = lineRenderer;
            document_ = document;
            document_.CollectionChanged += SubmissionDocumentChanged;
            cursorTop_ = Console.CursorTop;
            writer_ = writer;
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
                    writer_.WriteLine();

                    if (cursorTop_ > 0)
                        cursorTop_--;
                }

                Console.SetCursorPosition(0, cursorTop_ + lineCount);
                Console.ForegroundColor = ConsoleColor.Green;

                if (lineCount == 0)
                    writer_.Write("» ");
                else
                    writer_.Write("· ");

                Console.ResetColor();
                lineRenderer_(document_, lineCount, null);
                writer_.Write(new string(' ', Console.WindowWidth - line.Length - 2));
                lineCount++;
            }

            var blankLineCount = renderedLineCount_ - lineCount;

            if (blankLineCount > 0) {
                var blankLine = new string(' ', Console.WindowWidth);

                for (int i=0; i<blankLineCount; i++) {
                    Console.SetCursorPosition(0, cursorTop_ + lineCount + i);
                    writer_.WriteLine(blankLine);
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
        var view = new SubmissionView(RenderLine, document, writer_);

        while (!done_) {
            var key = Console.ReadKey(true);
            HandleKey(key, document, view);
        }

        view.currentLine = document.Count - 1;
        view.currentCharacter = document[view.currentLine].Length;

        writer_.WriteLine();

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
        } else if (key.Modifiers == (ConsoleModifiers.Control & ConsoleModifiers.Shift)) {
            switch (key.Key) {
                case ConsoleKey.Backspace:
                    HandleControlBackspace(document, view);
                    break;
                case ConsoleKey.Delete:
                    HandleControlDelete(document, view);
                    break;
                case ConsoleKey.Enter:
                    HandleControlShiftEnter(document, view);
                    break;
                case ConsoleKey.LeftArrow:
                    HandleControlLeftArrow(document, view);
                    break;
                case ConsoleKey.RightArrow:
                    HandleControlRightArrow(document, view);
                    break;
                default:
                    break;
            }
        } else if (key.Modifiers == ConsoleModifiers.Control) {
            switch (key.Key) {
                case ConsoleKey.Backspace:
                    HandleControlBackspace(document, view);
                    break;
                case ConsoleKey.Delete:
                    HandleControlDelete(document, view);
                    break;
                case ConsoleKey.Enter:
                    HandleControlEnter(document, view);
                    break;
                case ConsoleKey.LeftArrow:
                    HandleControlLeftArrow(document, view);
                    break;
                case ConsoleKey.RightArrow:
                    HandleControlRightArrow(document, view);
                    break;
                default:
                    break;
            }
        } else if (key.Modifiers == ConsoleModifiers.Shift) {
            switch (key.Key) {
                case ConsoleKey.Enter:
                    InsertLine(document, view);
                    break;
                case ConsoleKey.Backspace:
                    HandleBackspace(document, view);
                    break;
                case ConsoleKey.Delete:
                    HandleDelete(document, view);
                    break;
                case ConsoleKey.Tab:
                    HandleShiftTab(document, view);
                    break;
                case ConsoleKey.LeftArrow:
                    HandleLeftArrow(document, view);
                    break;
                case ConsoleKey.RightArrow:
                    HandleRightArrow(document, view);
                    break;
                default:
                    break;
            }
        }

        if (key.Key != ConsoleKey.Backspace && key.KeyChar >= ' ')
            HandleTyping(document, view, key.KeyChar.ToString());
    }

    internal virtual void SpecialEscapeSequence() {
        done_ = true;
    }

    private void HandleControlShiftEnter(ObservableCollection<string> document, SubmissionView view) {
        SpecialEscapeSequence();
    }

    private void HandleShiftTab(ObservableCollection<string> document, SubmissionView view) {
        var line = document[view.currentLine];
        var whitespace = line.Length - line.TrimStart().Length;
        var remainingSpaces = whitespace % tabWidth;

        if (remainingSpaces == 0 && whitespace > 0)
            remainingSpaces = 4;

        document[view.currentLine] = line.Substring(remainingSpaces);
        view.currentCharacter -= remainingSpaces;

        view.currentTypingTabbing--;
    }

    private void HandleControlRightArrow(ObservableCollection<string> document, SubmissionView view) {
        var line = document[view.currentLine];

        if (view.currentCharacter <= line.Length - 1) {
            var offset = GetWordBoundaryFront(document, view);
            view.currentCharacter += offset;
        }
    }

    private void HandleControlDelete(ObservableCollection<string> document, SubmissionView view) {
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

        var offset = GetWordBoundaryFront(document, view, strict: true);
        var before = line.Substring(0, start);
        var after = line.Substring(start + offset);

        if (ContainsOpening(line.Substring(start, offset)))
            view.currentBlockTabbing.Clear();

        document[lineIndex] = before + after;
    }

    private void HandleControlLeftArrow(ObservableCollection<string> document, SubmissionView view) {
        if (view.currentCharacter > 0) {
            var offset = GetWordBoundaryBack(document, view);
            view.currentCharacter -= offset;
        }
    }

    private void HandleControlBackspace(ObservableCollection<string> document, SubmissionView view) {
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
            var offset = GetWordBoundaryBack(document, view, strict: true);

            var lineIndex = view.currentLine;
            var line = document[lineIndex];
            var before = line.Substring(0, start - offset);
            var after = line.Substring(start);

            if (ContainsOpening(line.Substring(start - offset, offset)))
                view.currentBlockTabbing.Clear();

            document[lineIndex] = before + after;
            view.currentCharacter -= offset;
        }
    }

    private bool ContainsOpening(string line) {
        return line.Contains('{') || line.Contains('(') || line.Contains('[');
    }

    private int GetWordBoundaryFront(ObservableCollection<string> document, SubmissionView view, bool strict = false) {
        var line = document[view.currentLine];
        var maxLength = line.Length - 1;
        var start = view.currentCharacter;
        var offset = 0;

        char GetChar(int extraOffset = 0) {
            return line.Substring(GetPos(extraOffset), 1).Single();
        }

        int GetPos(int extraOffset = 0) {
            return start + offset + extraOffset;
        }

        var current = GetChar();

        bool IsTokenName(char current) {
            if (Char.IsLetterOrDigit(current) || current == '_')
                return true;

            return false;
        }

        if (!strict) {
            while (Char.IsWhiteSpace(current)) {
                if (GetPos() > maxLength)
                    return offset;

                offset++;

                if (GetPos() > maxLength)
                    return offset;

                current = GetChar();
            }

            if (GetPos() < maxLength) {
                if (Char.IsPunctuation(current) && IsTokenName(GetChar(1)))
                    offset++;
            }
        } else {
            if (GetPos() < maxLength) {
                if (Char.IsWhiteSpace(current)) {
                    if (!Char.IsWhiteSpace(GetChar(1))) {
                        offset++;
                    } else {
                        while (Char.IsWhiteSpace(current)) {
                            if (GetPos() > maxLength)
                                return offset;

                            offset++;

                            if (GetPos() > maxLength)
                                return offset;

                            current = GetChar();
                        }

                        return offset;
                    }
                }
            }
        }

        current = GetChar();

        if (Char.IsLetterOrDigit(current)) {
            while (GetPos() <= maxLength) {
                offset++;

                if (GetPos() > maxLength)
                    break;

                current = GetChar();

                if (!IsTokenName(current))
                    break;
            }

            return offset;
        } else if (Char.IsPunctuation(current)) {
            while (GetPos() <= maxLength) {
                offset++;

                if (GetPos() > maxLength)
                    break;

                current = GetChar();

                if (!Char.IsPunctuation(current))
                    break;
            }
        }

        return offset;
    }

    private int GetWordBoundaryBack(ObservableCollection<string> document, SubmissionView view, bool strict = false) {
        var line = document[view.currentLine];
        var start = view.currentCharacter;
        var offset = 1;

        char GetChar(int extraOffset = 0) {
            return line.Substring(GetPos(extraOffset), 1).Single();
        }

        int GetPos(int extraOffset = 0) {
            return start - offset - extraOffset;
        }

        var current = GetChar();

        bool IsTokenName(char current) {
            if (Char.IsLetterOrDigit(current) || current == '_')
                return true;

            return false;
        }

        if (!strict) {
            while (Char.IsWhiteSpace(current)) {
                offset++;

                if (GetPos() == 0)
                    return offset;

                current = GetChar();

                if (GetPos() == 0)
                    return offset;
            }

            if (GetPos() > 1) {
                if (Char.IsPunctuation(current) && IsTokenName(GetChar(1)))
                    offset++;
            }
        } else {
            if (GetPos() > 1) {
                if (Char.IsWhiteSpace(current)) {
                    if (!Char.IsWhiteSpace(GetChar(1))) {
                        offset++;
                    } else {
                        while (GetPos() > 0) {
                            offset++;

                            if (GetPos(1) < 0)
                                break;

                            var previous = GetChar(1);

                            if (!Char.IsWhiteSpace(previous))
                                break;
                        }

                        return offset;
                    }
                }
            }
        }

        current = GetChar();

        if (Char.IsLetterOrDigit(current)) {
            while (GetPos() > 0) {
                offset++;

                if (GetPos(1) < 0)
                    break;

                var previous = GetChar(1);

                if (!IsTokenName(previous))
                    break;
            }

            return offset;
        } else if (Char.IsPunctuation(current)) {
            while (GetPos() > 0) {
                offset++;

                if (GetPos(1) < 0)
                    break;

                var previous = GetChar(1);

                if (!Char.IsPunctuation(previous))
                    break;
            }
        }

        return offset;
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
        var start = view.currentCharacter;
        var remainingSpaces = tabWidth - start % tabWidth;
        var line = document[view.currentLine];
        document[view.currentLine] = line.Insert(start, new string(' ', remainingSpaces));
        view.currentCharacter += remainingSpaces;

        view.currentTypingTabbing++;
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

        if (ContainsOpening(line.Substring(start, 1)))
            view.currentBlockTabbing.Clear();

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

            var offset = GetTabBoundaryBack(document, view);

            var before = line.Substring(0, start - offset);
            var after = line.Substring(start);

            if (ContainsOpening(line.Substring(start - offset, offset)))
                view.currentBlockTabbing.Clear();

            document[lineIndex] = before + after;
            view.currentCharacter -= offset;
        }
    }

    private int GetTabBoundaryBack(ObservableCollection<string> document, SubmissionView view) {
        var line = document[view.currentLine];
        var maxOffset = line.Length % tabWidth;

        if (maxOffset == 0)
            maxOffset = tabWidth;

        var start = view.currentCharacter;
        var offset = 1;

        while (offset < maxOffset) {
            offset++;

            if (start - offset - 1 < 0)
                break;

            var previous = line.Substring(start - offset - 1, 1).Single();

            if (!Char.IsWhiteSpace(previous))
                break;
        }

        if (String.IsNullOrWhiteSpace(line.Substring(start - offset, offset)))
            return offset;

        return 1;
    }

    private void HandleControlEnter(ObservableCollection<string> document, SubmissionView view) {
        done_ = true;
    }

    private void HandleTyping(ObservableCollection<string> document, SubmissionView view, string text) {
        var lineIndex = view.currentLine;

        Dictionary<char, char> pairs = new Dictionary<char, char>(){
            {'{', '}'},
            {'[', ']'},
            {'(', ')'}
        };

        if (text == "{" || text == "(" || text == "[")
            view.currentBlockTabbing.Push((pairs[text.Single()], view.currentTypingTabbing));

        if ((text == "}" || text == ")" || text == "]") && String.IsNullOrWhiteSpace(document[lineIndex])) {
            var foundPair = false;

            if (view.currentBlockTabbing.Count > 0) {
                var targetTabbing = view.currentBlockTabbing.Pop();

                while (targetTabbing.Item1 != text.Single()) {
                    if (view.currentBlockTabbing.Count == 0)
                        break;

                    targetTabbing = view.currentBlockTabbing.Pop();
                }

                if (targetTabbing.Item1 == text.Single()) {
                    foundPair = true;

                    for (int i=view.currentTypingTabbing; i>targetTabbing.Item2; i--)
                        HandleShiftTab(document, view);
                }
            }

            if (!foundPair) {
                document[lineIndex] = "";
                view.currentCharacter = 0;
            }
        }

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

    private void InsertLine(ObservableCollection<string> document, SubmissionView view) {
        var remainder = document[view.currentLine].Substring(view.currentCharacter);
        document[view.currentLine] = document[view.currentLine].Substring(0, view.currentCharacter);

        var lineIndex = view.currentLine + 1;
        document.Insert(lineIndex, remainder);
        view.currentCharacter = 0;
        view.currentLine = lineIndex;

        var previousLine = document[view.currentLine - 1];
        var whitespace = (previousLine.Length - previousLine.TrimStart().Length) / tabWidth;

        if (previousLine.Length > 0 && ContainsOpening(previousLine[^1].ToString())) {
            view.currentTypingTabbing++;
            whitespace++;
        }

        HandleTyping(document, view, new String(' ', whitespace * tabWidth));
    }

    protected void ClearHistory() {
        submissionHistory_.Clear();
    }

    protected virtual object RenderLine(IReadOnlyList<string> lines, int lineIndex, object state) {
        writer_.Write(lines[lineIndex]);
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
                } else {
                    sb.Append(c);
                }
            } else if (c == '\"') {
                if (!inQuotes) {
                    inQuotes = true;
                } else if (l == '\"') {
                    sb.Append(c);
                    position++;
                } else if (inQuotes) {
                    inQuotes = false;
                }
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
            handle.diagnostics.Push(new BelteDiagnostic(Repl.Diagnostics.Error.UnknownReplCommand(line)));

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
                new BelteDiagnostic(Repl.Diagnostics.Error.WrongArgumentCount(command.name, parameterNames)));

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

    internal void ReviveDocument() {
        Console.Clear();
        Console.Write(writer_.captured.ToString());
    }
}

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;
using System.Text;
using Buckle;
using Buckle.Diagnostics;

namespace Repl;

/// <summary>
/// Overrides default console handling and controls all keystrokes. Adds framework for meta commands and submissions.
/// </summary>
public abstract class ReplBase {
    /// <summary>
    /// Handles outputting <see cref="ReplBase" /> text to an out.
    /// </summary>
    internal OutputCapture _writer = new OutputCapture();

    /// <summary>
    /// <see cref="Compiler" /> object representing entirety of compilation.
    /// </summary>
    internal Compiler handle;

    /// <summary>
    /// Particular <see cref="ReplBase.DiagnosticHandle" /> used to handle diagnostics in the <see cref="ReplBase" />.
    /// </summary>
    internal DiagnosticHandle diagnosticHandle;

    private const int TabWidth = 4;

    private readonly List<string> _submissionHistory = new List<string>();
    private readonly List<MetaCommand> _metaCommands = new List<MetaCommand>();

    protected bool _abortEvaluation;
    protected bool _showTime;

    private int _submissionHistoryIndex;
    private bool _done;
    private bool _evaluate;

    protected ReplBase(Compiler handle, DiagnosticHandle diagnosticHandle) {
        this.handle = handle;
        this.diagnosticHandle = diagnosticHandle;
        InitializeMetaCommands();
    }

    /// <summary>
    /// Callback to handle Diagnostics, be it logging or displaying to the console.
    /// </summary>
    /// <param name="compiler"><see cref="Compiler" /> object representing entirety of compilation.</param>
    /// <param name="me">Display name of the program.</param>
    /// <param name="textColor">Color to display Diagnostics (if displaying).</param>
    /// <returns>C-Style error code of most severe <see cref="Diagnostic" />.</returns>
    public delegate int DiagnosticHandle(
        Compiler compiler, string me = null, ConsoleColor textColor = ConsoleColor.White);

    private delegate object LineRenderHandler(IReadOnlyList<string> lines, int lineIndex, object state);

    /// <summary>
    /// <see cref="ReplBase" /> specific state used by child classes.
    /// </summary>
    internal abstract object _state { get; set; }

    /// <summary>
    /// Resets all state.
    /// </summary>
    internal virtual void ResetState() {
        _done = false;
        _evaluate = true;
        _showTime = false;
    }

    /// <summary>
    /// Run loop of the <see cref="ReplBase" /> (exited with ctrl + c or entering blank lines).
    /// Does not initialize <see cref="ReplBase" />, only runs it.
    /// </summary>
    public void Run() {
        string text;

        void EvaluateSubmissionWrapper() {
            EvaluateSubmission(text);
        }

        while (true) {
            text = EditSubmission();

            if (string.IsNullOrEmpty(text))
                return;

            if (_evaluate) {
                if (!text.Contains(Environment.NewLine) && text.StartsWith('#')) {
                    EvaluateReplCommand(text);
                } else {
                    var evaluateSubmissionReference = new ThreadStart(EvaluateSubmissionWrapper);
                    var evaluateSubmissionThread = new Thread(evaluateSubmissionReference);
                    _abortEvaluation = false;
                    var startTime = DateTime.Now;
                    evaluateSubmissionThread.Start();

                    Console.TreatControlCAsInput = true;

                    var broke = false;

                    while (evaluateSubmissionThread.IsAlive) {
                        if (Console.KeyAvailable) {
                            var key = Console.ReadKey(true);

                            if (key.Key == ConsoleKey.C && key.Modifiers == ConsoleModifiers.Control) {
                                broke = true;
                                break;
                            }
                        }
                    }

                    _abortEvaluation = true;
                    Console.TreatControlCAsInput = false;

                    while (evaluateSubmissionThread.IsAlive) { }

                    if (broke || _showTime) {
                        var finishWord = broke ? "Aborted" : "Finished";
                        var seconds = (DateTime.Now - startTime).TotalSeconds;
                        seconds = seconds > 1 ? (int)seconds : Math.Round(seconds, 3);

                        var secondWord = seconds < 1
                            ? (seconds * 1000 == 1 ? "millisecond" : "milliseconds")
                            : (seconds == 1 ? "second" : "seconds");

                        seconds = seconds < 1 ? seconds * 1000 : seconds;

                        var previous = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        _writer.WriteLine($"{finishWord} after {seconds} {secondWord}");
                        Console.ForegroundColor = previous;
                    }
                }
            }

            _evaluate = true;

            _submissionHistory.Add(text);
            _submissionHistoryIndex = 0;
        }
    }

    /// <summary>
    /// Reloads <see cref="ReplBase" /> to start accepting submissions again.
    /// </summary>
    internal void ReviveDocument() {
        // TODO Redisplay previous submissions
        Console.Clear();
    }

    /// <summary>
    /// Safely terminates an in-progress submission without evaluating it for a result.
    /// </summary>
    internal virtual void SpecialEscapeSequence() {
        _done = true;
        _evaluate = false;
    }

    /// <summary>
    /// Gets all previous submissions submitted in current instance (does not access previous instances' submissions).
    /// </summary>
    /// <returns>Internal representation of submissions.</returns>
    internal List<string> GetSubmissionHistory() {
        return _submissionHistory;
    }

    protected virtual object RenderLine(IReadOnlyList<string> lines, int lineIndex, object state) {
        _writer.Write(lines[lineIndex]);
        return state;
    }

    protected void ClearHistory() {
        _submissionHistory.Clear();
    }

    protected abstract bool IsCompleteSubmission(string text);
    protected abstract void EvaluateSubmission(string text);

    [MetaCommand("help", "Shows this document")]
    protected void EvaluateHelp() {
        var maxLength = _metaCommands
            .Max(
                mc => mc.name.Length +
                string.Join(" ", mc.method.GetParameters().SelectMany(p => p.Name).ToList()).Length +
                string.Join(" ", mc.method.GetParameters().SelectMany(p => p.DefaultValue.ToString()).ToList()).Length);

        var previous = Console.ForegroundColor;

        foreach (var metaCommand in _metaCommands.OrderBy(mc => mc.name)) {
            var name = metaCommand.name;

            foreach (var parameter in metaCommand.method.GetParameters()) {
                if (parameter.HasDefaultValue)
                    name += $" <{parameter.Name}={parameter.DefaultValue}>";
                else
                    name += $" <{parameter.Name}>";
            }

            var paddedName = name.PadRight(maxLength);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            _writer.Write("#");
            Console.ForegroundColor = previous;
            _writer.Write(paddedName);
            _writer.Write("  ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            _writer.Write(metaCommand.description);
            Console.ForegroundColor = previous;
            _writer.WriteLine();
        }
    }

    private string EditSubmission() {
        _done = false;

        var document = new ObservableCollection<string>() { "" };
        var view = new SubmissionView(RenderLine, document, _writer);

        while (!_done) {
            // Allow ctrl + c to exit at all times except getting user input
            // This allows custom behavior, but still keeps ctrl + c protection if app freezes
            Console.TreatControlCAsInput = true;
            var key = Console.ReadKey(true);
            Console.TreatControlCAsInput = false;
            HandleKey(key, document, view);
        }

        view.currentLine = document.Count - 1;
        view.currentCharacter = document[view.currentLine].Length;

        _writer.WriteLine();

        return string.Join(Environment.NewLine, document);
    }

    private void InitializeMetaCommands() {
        var methods = GetType()
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static |
                BindingFlags.Instance | BindingFlags.FlattenHierarchy
            );

        foreach (var method in methods) {
            var attribute = method.GetCustomAttribute<MetaCommandAttribute>();

            if (attribute == null)
                continue;

            var metaCommand = new MetaCommand(attribute.name, attribute.description, method);
            _metaCommands.Add(metaCommand);
        }
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
                case ConsoleKey.C:
                    HandleControlC(document, view);
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
        } else if (key.Modifiers == ConsoleModifiers.Alt) {
            switch (key.Key) {
                case ConsoleKey.Enter:
                    HandleAltEnter(document, view);
                    break;
                default:
                    break;
            }
        }

        if (key.Key != ConsoleKey.Backspace && key.KeyChar >= ' ')
            HandleTyping(document, view, key.KeyChar.ToString());
    }

    private void HandleControlShiftEnter(ObservableCollection<string> document, SubmissionView view) {
        SpecialEscapeSequence();
    }

    private void HandleAltEnter(ObservableCollection<string> document, SubmissionView view) {
        SpecialEscapeSequence();
    }

    private void HandleControlC(ObservableCollection<string> document, SubmissionView view) {
        if (!_done)
            SpecialEscapeSequence();
        else
            // Normal ctrl + c behavior
            System.Environment.Exit(1);
    }

    private void HandleShiftTab(ObservableCollection<string> document, SubmissionView view) {
        var line = document[view.currentLine];
        var whitespace = line.Length - line.TrimStart().Length;
        var remainingSpaces = whitespace % TabWidth;

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
            if (char.IsLetterOrDigit(current) || current == '_')
                return true;

            return false;
        }

        if (!strict) {
            while (char.IsWhiteSpace(current)) {
                if (GetPos() > maxLength)
                    return offset;

                offset++;

                if (GetPos() > maxLength)
                    return offset;

                current = GetChar();
            }

            if (GetPos() < maxLength) {
                if (char.IsPunctuation(current) && IsTokenName(GetChar(1)))
                    offset++;
            }
        } else {
            if (GetPos() < maxLength) {
                if (char.IsWhiteSpace(current)) {
                    if (!char.IsWhiteSpace(GetChar(1))) {
                        offset++;
                    } else {
                        while (char.IsWhiteSpace(current)) {
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

        if (char.IsLetterOrDigit(current)) {
            while (GetPos() <= maxLength) {
                offset++;

                if (GetPos() > maxLength)
                    break;

                current = GetChar();

                if (!IsTokenName(current))
                    break;
            }

            return offset;
        } else if (char.IsPunctuation(current)) {
            while (GetPos() <= maxLength) {
                offset++;

                if (GetPos() > maxLength)
                    break;

                current = GetChar();

                if (!char.IsPunctuation(current))
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
            if (char.IsLetterOrDigit(current) || current == '_')
                return true;

            return false;
        }

        if (!strict) {
            while (char.IsWhiteSpace(current)) {
                offset++;

                if (GetPos() == 0)
                    return offset;

                current = GetChar();

                if (GetPos() == 0)
                    return offset;
            }

            if (GetPos() > 1) {
                if (char.IsPunctuation(current) && IsTokenName(GetChar(1)))
                    offset++;
            }
        } else {
            if (GetPos() > 1) {
                if (char.IsWhiteSpace(current)) {
                    if (!char.IsWhiteSpace(GetChar(1))) {
                        offset++;
                    } else {
                        while (GetPos() > 0) {
                            offset++;

                            if (GetPos(1) < 0)
                                break;

                            var previous = GetChar(1);

                            if (!char.IsWhiteSpace(previous))
                                break;
                        }

                        return offset;
                    }
                }
            }
        }

        current = GetChar();

        if (char.IsLetterOrDigit(current)) {
            while (GetPos() > 0) {
                offset++;

                if (GetPos(1) < 0)
                    break;

                var previous = GetChar(1);

                if (!IsTokenName(previous))
                    break;
            }

            return offset;
        } else if (char.IsPunctuation(current)) {
            while (GetPos() > 0) {
                offset++;

                if (GetPos(1) < 0)
                    break;

                var previous = GetChar(1);

                if (!char.IsPunctuation(previous))
                    break;
            }
        }

        return offset;
    }

    private void HandlePageDown(ObservableCollection<string> document, SubmissionView view) {
        _submissionHistoryIndex++;

        if (_submissionHistoryIndex > _submissionHistory.Count - 1)
            _submissionHistoryIndex = 0;

        UpdateDocumentFromHistory(document, view);
    }

    private void HandlePageUp(ObservableCollection<string> document, SubmissionView view) {
        _submissionHistoryIndex--;

        if (_submissionHistoryIndex < 0)
            _submissionHistoryIndex = _submissionHistory.Count - 1;

        UpdateDocumentFromHistory(document, view);
    }

    private void UpdateDocumentFromHistory(ObservableCollection<string> document, SubmissionView view) {
        if (_submissionHistory.Count == 0)
            return;

        document.Clear();

        var historyItem = _submissionHistory[_submissionHistoryIndex];
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
        var remainingSpaces = TabWidth - start % TabWidth;
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
        var maxOffset = line.Length % TabWidth;

        if (maxOffset == 0)
            maxOffset = TabWidth;

        var start = view.currentCharacter;
        var offset = 1;

        while (offset < maxOffset) {
            offset++;

            if (start - offset - 1 < 0)
                break;

            var previous = line.Substring(start - offset - 1, 1).Single();

            if (!char.IsWhiteSpace(previous))
                break;
        }

        if (offset <= start)
            if (string.IsNullOrWhiteSpace(line.Substring(start - offset, offset)))
                return offset;

        return 1;
    }

    private void HandleControlEnter(ObservableCollection<string> document, SubmissionView view) {
        _done = true;
    }

    private void HandleTyping(ObservableCollection<string> document, SubmissionView view, string text) {
        if (document[view.currentLine].Length >= Console.WindowWidth - 3)
            return;

        var lineIndex = view.currentLine;

        Dictionary<char, char> pairs = new Dictionary<char, char>(){
            {'{', '}'},
            {'[', ']'},
            {'(', ')'}
        };

        if (text == "{" || text == "(" || text == "[")
            view.currentBlockTabbing.Push((pairs[text.Single()], view.currentTypingTabbing));

        if ((text == "}" || text == ")" || text == "]")) {
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

                    if (string.IsNullOrWhiteSpace(document[lineIndex])) {
                        for (int i=view.currentTypingTabbing; i>targetTabbing.Item2; i--)
                            HandleShiftTab(document, view);
                    }
                }
            }

            if (!foundPair && string.IsNullOrWhiteSpace(document[lineIndex])) {
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
            _done = true;
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
        var whitespace = (previousLine.Length - previousLine.TrimStart().Length) / TabWidth;

        if (previousLine.Length > 0 && ContainsOpening(previousLine[^1].ToString())) {
            view.currentTypingTabbing++;
            whitespace++;
        }

        HandleTyping(document, view, new String(' ', whitespace * TabWidth));
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

        var command = _metaCommands.SingleOrDefault(mc => mc.name == commandName);

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
            if (args.Count == command.method.GetParameters()
                .Where(t => !t.HasDefaultValue).ToArray().Length) {
                foreach (var parameter in command.method.GetParameters().Where(p => p.HasDefaultValue))
                    args.Add(parameter.DefaultValue.ToString());
            } else {
                var parameterNames = string.Join(" ", parameters.Select(p => $"<{p.Name}>"));
                handle.diagnostics.Push(
                    new BelteDiagnostic(Repl.Diagnostics.Error.WrongArgumentCount(command.name, parameterNames))
                );

                if (diagnosticHandle != null)
                    diagnosticHandle(handle, "repl");
                else
                    handle.diagnostics.Clear();

                return;
            }
        }

        var instance = command.method.IsStatic ? null : this;
        command.method.Invoke(instance, args.ToArray());
    }

    /// <summary>
    /// Wrapper around the System.Console class.
    /// </summary>
    internal sealed class OutputCapture : TextWriter, IDisposable {
        /// <summary>
        /// Creates an out.
        /// </summary>
        internal OutputCapture() {
            // captured = new List<List<string>>();
        }

        // internal List<List<string>> captured { get; private set; }

        /// <summary>
        /// Encoding to use, constant.
        /// </summary>
        /// <value>Ascii.</value>
        public override Encoding Encoding { get { return Encoding.ASCII; } }

        public override void Write(string output) {
            Console.Write(output);
        }

        public override void WriteLine(string output) {
            Console.WriteLine(output);
        }

        public override void WriteLine() {
            Console.WriteLine();
        }

        /// <summary>
        /// Changes Console cursor position.
        /// </summary>
        /// <param name="left">Column position (left (0) -> right).</param>
        /// <param name="top">Row position (top (0) -> down).</param>
        public void SetCursorPosition(int left, int top) {
            Console.SetCursorPosition(left, top);
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    protected sealed class MetaCommandAttribute : Attribute {
        public MetaCommandAttribute(string name, string description) {
            this.name = name;
            this.description = description;
        }

        public string name { get; }
        public string description { get; }
    }

    private sealed class MetaCommand {
        public MetaCommand(string name, string description, MethodInfo method) {
            this.name = name;
            this.method = method;
            this.description = description;
        }

        public string name { get; }
        public string description { get; set; }
        public MethodInfo method { get; }
    }

    private sealed class SubmissionView {
        private readonly LineRenderHandler _lineRenderer;
        private readonly ObservableCollection<string> _document;
        private int _cursorTop;
        private int _renderedLineCount;
        private int _currentLine;
        private int _currentCharacter;
        private OutputCapture _writer;

        internal SubmissionView(
            LineRenderHandler lineRenderer, ObservableCollection<string> document, OutputCapture writer) {
            _lineRenderer = lineRenderer;
            _document = document;
            _document.CollectionChanged += SubmissionDocumentChanged;
            _cursorTop = Console.CursorTop;
            _writer = writer;
            Render();
        }

        internal int currentLine {
            get => _currentLine;
            set {
                if (_currentLine != value) {
                    _currentLine = value;
                    _currentCharacter = Math.Min(_document[_currentLine].Length, _currentCharacter);
                    UpdateCursorPosition();
                }
            }
        }

        internal int currentCharacter {
            get => _currentCharacter;
            set {
                if (_currentCharacter != value) {
                    _currentCharacter = value;
                    UpdateCursorPosition();
                }
            }
        }

        internal Stack<(char, int)> currentBlockTabbing = new Stack<(char, int)>();
        internal int currentTypingTabbing = 0;

        private void SubmissionDocumentChanged(object sender, NotifyCollectionChangedEventArgs e) {
            Render();
        }

        private void Render() {
            Console.CursorVisible = false;
            var lineCount = 0;

            foreach (var line in _document) {
                if (_cursorTop + lineCount >= Console.WindowHeight - 1) {
                    _writer.SetCursorPosition(0, Console.WindowHeight - 1);
                    _writer.WriteLine();

                    if (_cursorTop > 0)
                        _cursorTop--;
                }

                _writer.SetCursorPosition(0, _cursorTop + lineCount);
                var previous = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Green;

                if (lineCount == 0)
                    _writer.Write("» ");
                else
                    _writer.Write("· ");

                Console.ForegroundColor = previous;
                _lineRenderer(_document, lineCount, null);
                _writer.Write(new string(' ', Console.WindowWidth - line.Length - 2));
                lineCount++;
            }

            var blankLineCount = _renderedLineCount - lineCount;

            if (blankLineCount > 0) {
                var blankLine = new string(' ', Console.WindowWidth);

                for (int i=0; i<blankLineCount; i++) {
                    _writer.SetCursorPosition(0, _cursorTop + lineCount + i);
                    _writer.WriteLine(blankLine);
                }
            }

            _renderedLineCount = lineCount;
            Console.CursorVisible = true;
            UpdateCursorPosition();
        }

        private void UpdateCursorPosition() {
            _writer.SetCursorPosition(2 + _currentCharacter, _cursorTop + _currentLine);
        }
    }
}

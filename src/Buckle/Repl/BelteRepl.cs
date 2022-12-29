using Buckle;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Authoring;
using Buckle.CodeAnalysis.Evaluating;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.IO;
using Diagnostics;

namespace Repl;

/// <summary>
/// Uses framework from <see cref="ReplBase" /> and adds syntax highlighting and evaluation.
/// </summary>
public sealed class BelteRepl : ReplBase {
    private static readonly Compilation emptyCompilation = Compilation.CreateScript(null);
    private Dictionary<string, ColorTheme> InUse = new Dictionary<string, ColorTheme>() {
        {"Dark", new DarkTheme()},
        {"Light", new LightTheme()},
        {"Green", new GreenTheme()},
    };

    /// <summary>
    /// Creates a new instance of a <see cref="BelteRepl" />, can run in parallel with other BelteRepls with
    /// unique outs.
    /// Uses System.Console by default, which cannot be used more than once.
    /// </summary>
    /// <param name="handle"><see cref="Compiler" /> object that represents entirety of compilation.</param>
    /// <param name="errorHandle">Callback to handle Diagnostics.</param>
    public BelteRepl(Compiler handle, DiagnosticHandle errorHandle) : base(handle, errorHandle) {
        state = new BelteReplState();
        ResetState();
        Console.BackgroundColor = state.colorTheme.background;
        EvaluateClear();
        LoadSubmissions();
    }

    /// <summary>
    /// Indicated to the state what page is being displayed to the user.
    /// </summary>
    internal enum Page {
        Repl,
        Settings
    }

    internal override object _state { get; set; }

    /// <summary>
    /// Cast of <see cref="ReplBase" /> specific state that has <see cref="BelteRepl" /> related state.
    /// </summary>
    internal BelteReplState state {
        get {
            return (BelteReplState)_state;
        }
        set {
            _state = value;
        }
    }

    internal override void ResetState() {
        state.showTree = false;
        state.showProgram = false;
        state.showWarnings = false;
        state.loadingSubmissions = false;
        state.variables = new Dictionary<VariableSymbol, EvaluatorObject>();
        state.previous = null;
        state.currentPage = Page.Repl;
        base.ResetState();
    }

    protected override object RenderLine(IReadOnlyList<string> lines, int lineIndex, object rState) {
        SyntaxTree syntaxTree;

        if (rState == null) {
            var text = string.Join(Environment.NewLine, lines);
            syntaxTree = SyntaxTree.Parse(text);
        } else {
            syntaxTree = (SyntaxTree)rState;
        }

        var texts = new List<(string text, ConsoleColor color)>();
        var lineSpan = syntaxTree.text.lines[lineIndex].span;
        var fullText = syntaxTree.text.ToString(lineSpan);

        var classifiedSpans = Classifier.Classify(syntaxTree, lineSpan);

        foreach (var classifiedSpan in classifiedSpans) {
            var classifiedText = syntaxTree.text.ToString(classifiedSpan.span);
            var color = state.colorTheme.@default;

            switch (classifiedSpan.classification) {
                case Classification.Identifier:
                    color = state.colorTheme.identifier;
                    break;
                case Classification.Number:
                    color = state.colorTheme.number;
                    break;
                case Classification.String:
                    color = state.colorTheme.@string;
                    break;
                case Classification.Comment:
                    color = state.colorTheme.comment;
                    break;
                case Classification.Keyword:
                    color = state.colorTheme.keyword;
                    break;
                case Classification.TypeName:
                    color = state.colorTheme.typeName;
                    break;
                case Classification.Text:
                    color = state.colorTheme.text;
                    break;
                default:
                    break;
            }

            texts.Add((classifiedText, color));
        }

        var offset = 0;

        for (int i=0; i<texts.Count(); i++) {
            var line = texts[i].text;

            if (fullText.Substring(offset, line.Length) == line) {
                offset += line.Length;
            } else {
                var extra = "";

                while (true) {
                    if (fullText.Substring(offset, texts[i].text.Length) == texts[i].text) {
                        texts.Insert(i, (extra, state.colorTheme.errorText));
                        break;
                    }

                    extra += fullText[offset++];
                }
            }
        }

        if (texts.Count() == 0)
            texts.Add((fullText, state.colorTheme.errorText));

        var pureTexts = texts.Select(t => t.text).ToList();
        var textsLength = string.Join("", pureTexts).Length;

        if (textsLength < fullText.Length)
            texts.Add((fullText.Substring(textsLength), state.colorTheme.errorText));

        foreach (var text in texts) {
            Console.ForegroundColor = text.color;
            _writer.Write(text.text);
        }

        Console.ForegroundColor = state.colorTheme.@default;

        return syntaxTree;
    }

    protected override void EvaluateSubmission(string text) {
        var syntaxTree = SyntaxTree.Parse(text);
        var compilation = Compilation.CreateScript(state.previous, syntaxTree);

        if (state.showTree)
            syntaxTree.root.WriteTo(Console.Out);
        if (state.showProgram)
            compilation.EmitTree(Console.Out);

        if (state.showWarnings)
            handle.diagnostics.Move(compilation.diagnostics);
        else
            handle.diagnostics.Move(compilation.diagnostics.FilterOut(DiagnosticType.Warning));

        EvaluationResult result = null;
        Console.ForegroundColor = state.colorTheme.result;

        if (!handle.diagnostics.FilterOut(DiagnosticType.Warning).Any()) {
            result = compilation.Evaluate(state.variables, ref _abortEvaluation);

            if (_abortEvaluation) {
                Console.ForegroundColor = state.colorTheme.@default;
                return;
            }

            if (state.showWarnings)
                handle.diagnostics.Move(result.diagnostics);
            else
                handle.diagnostics.Move(result.diagnostics.FilterOut(DiagnosticType.Warning));
        }

        var hasErrors = handle.diagnostics.FilterOut(DiagnosticType.Warning).Any();

        if (handle.diagnostics.Any()) {
            if (diagnosticHandle != null) {
                handle.diagnostics = BelteDiagnosticQueue.CleanDiagnostics(handle.diagnostics);
                diagnosticHandle(handle, textColor: state.colorTheme.textDefault);
            } else {
                handle.diagnostics.Clear();
            }
        }

        if (!hasErrors) {
            if (result.hasValue && !state.loadingSubmissions) {
                RenderResult(result.value);
                _writer.WriteLine();
            }

            state.previous = compilation;
            SaveSubmission(text);
        }

        Console.ForegroundColor = state.colorTheme.@default;
    }

    protected override bool IsCompleteSubmission(string text) {
        if (string.IsNullOrEmpty(text))
            return true;

        var twoBlankTines = text.Split(Environment.NewLine).Reverse()
            .TakeWhile(s => (string.IsNullOrEmpty(s) || string.IsNullOrWhiteSpace(s)))
            .Take(2)
            .Count() == 2;

        if (twoBlankTines)
            return true;

        var syntaxTree = SyntaxTree.Parse(text);
        var lastMember = syntaxTree.root.members.LastOrDefault();

        if (lastMember == null || lastMember.GetLastToken().isMissing)
            return false;

        return true;
    }

    private static void ClearSubmissions() {
        var path = GetSubmissionsDirectory();

        if (Directory.Exists(path))
            Directory.Delete(GetSubmissionsDirectory(), true);
    }

    private static string GetSubmissionsDirectory() {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var submissionsFolder = Path.Combine(localAppData, "Buckle", "Submissions");

        if (!Directory.Exists(submissionsFolder))
            Directory.CreateDirectory(submissionsFolder);

        return submissionsFolder;
    }

    private void RenderResult(object value) {
        if (value == null) {
            Console.Out.WritePunctuation("null");
        } else if (value.GetType().IsArray) {
            _writer.Write("{ ");
            var isFirst = true;

            foreach (object item in (Array)value) {
                if (isFirst)
                    isFirst = false;
                else
                    _writer.Write(", ");

                RenderResult(item);
            }

            _writer.Write(" }");
        } else if (value is Dictionary<object, object>) {
            _writer.Write("{ ");
            var isFirst = true;

            foreach (var pair in (Dictionary<object, object>)value) {
                if (isFirst)
                    isFirst = false;
                else
                    _writer.Write(", ");

                RenderResult(pair.Key);
                _writer.Write(": ");
                RenderResult(pair.Value);
            }

            _writer.Write(" }");
        } else {
            _writer.Write(value);
        }
    }

    private void SaveSubmission(string text) {
        if (state.loadingSubmissions)
            return;

        var submissionsFolder = GetSubmissionsDirectory();
        var count = Directory.GetFiles(submissionsFolder).Length;
        var name = $"submission{count:0000}";
        var fileName = Path.Combine(submissionsFolder, name);
        File.WriteAllText(fileName, text);
    }

    private void LoadSubmissions() {
        var files = Directory.GetFiles(GetSubmissionsDirectory()).OrderBy(f => f).ToArray();
        var keyword = files.Length == 1 ? "submission" : "submissions";
        Console.Out.WritePunctuation($"loaded {files.Length} {keyword}");
        _writer.WriteLine();

        var @out = Console.Out;
        Console.SetOut(new StreamWriter(Stream.Null));
        state.loadingSubmissions = true;

        foreach (var file in files) {
            var text = File.ReadAllText(file);
            EvaluateSubmission(text);
        }

        state.loadingSubmissions = false;
        Console.SetOut(@out);
    }

    [MetaCommand("showTree", "Toggle to display parse tree of each input")]
    private void EvaluateShowTree() {
        state.showTree = !state.showTree;
        _writer.WriteLine(state.showTree ? "Parse-trees visible" : "Parse-trees hidden");
    }

    [MetaCommand("showProgram", "Toggle to display intermediate representation of each input")]
    private void EvaluateShowProgram() {
        state.showProgram = !state.showProgram;
        _writer.WriteLine(state.showProgram ? "Bound-trees visible" : "Bound-trees hidden");
    }

    [MetaCommand("clear", "Clear the screen")]
    private void EvaluateClear() {
        Console.Clear();
    }

    [MetaCommand("cls", "Clear the screen")]
    private void EvaluateCls() {
        Console.Clear();
    }

    [MetaCommand("reset", "Clear previous submissions")]
    private void EvaluateReset() {
        ResetState();
        ClearSubmissions();
    }

    [MetaCommand("load", "Load in text from <path>")]
    private void EvaluateLoad(string path) {
        if (!File.Exists(path)) {
            handle.diagnostics.Push(new BelteDiagnostic(Repl.Diagnostics.Error.NoSuchFile(path)));

            if (diagnosticHandle != null)
                diagnosticHandle(handle, "repl", state.colorTheme.textDefault);
            else
                handle.diagnostics.Clear();

            return;
        }

        var text = File.ReadAllText(path);
        EvaluateSubmission(text);
    }

    [MetaCommand("ls", "List all defined symbols")]
    private void EvaluateLs() {
        var compilation = state.previous ?? emptyCompilation;
        var symbols = compilation.GetSymbols().OrderBy(s => s.kind).ThenBy(s => s.name);

        foreach (var symbol in symbols) {
            symbol.WriteTo(Console.Out);
            _writer.WriteLine();
        }
    }

    [MetaCommand("dump", "Show contents of symbol <name>")]
    private void EvaluateDump(string signature) {
        var compilation = state.previous ?? emptyCompilation;
        var name = signature.Contains('(') ? signature.Split('(')[0] : signature;
        var symbols = (signature == name
            ? compilation.GetSymbols().Where(f => f.name == name)
            : compilation.GetSymbols<FunctionSymbol>().Where(f => f.SignatureNoReturnNoParameterNames() == signature))
                .ToArray();

        Symbol symbol = null;

        if (symbols.ToArray().Length == 0 && signature.StartsWith('<')) {
            // This will find hidden function symbols not normally exposed to the user
            // Generated functions should never have overloads, so only the name is checked
            // (as apposed to the entire signature)
            try {
                compilation.EmitTree(name, Console.Out);
                return;
            } catch (BelteInternalException) { }
        }

        if (symbols.Length == 0) {
            if (signature == name)
                handle.diagnostics.Push(new BelteDiagnostic(Repl.Diagnostics.Error.UndefinedSymbol(name)));
            else
                handle.diagnostics.Push(new BelteDiagnostic(Repl.Diagnostics.Error.NoSuchFunction(signature)));
        } else if (symbols.Length == 1) {
            symbol = symbols.Single();
        } else if (signature == name) {
            var temp = symbols.Where(s => s is not FunctionSymbol);

            if (temp.Any()) {
                symbol = temp.First();
            } else {
                handle.diagnostics.Push(
                    new BelteDiagnostic(Repl.Diagnostics.Error.AmbiguousSignature(signature, symbols))
                );
            }
        } else {
            symbol = symbols.First();
        }

        if (symbol != null) {
            compilation.EmitTree(symbol, Console.Out);
            return;
        }

        if (diagnosticHandle != null)
            diagnosticHandle(handle, "repl", state.colorTheme.textDefault);
        else
            handle.diagnostics.Clear();

        return;
    }

    [MetaCommand("exit", "Exit the repl")]
    private void EvaluateExit() {
        Environment.Exit(0);
    }

    [MetaCommand("saveToFile", "Save previous <count> submissions to <path>")]
    private void EvaluateSaveToFile(string path, string count = "1") {
        if (!Int32.TryParse(count, out var countInt)) {
            handle.diagnostics.Push(
                new BelteDiagnostic(Repl.Diagnostics.Error.InvalidArgument(count, typeof(Int32))));

            if (diagnosticHandle != null)
                diagnosticHandle(handle, "repl", state.colorTheme.textDefault);
            else
                handle.diagnostics.Clear();

            return;
        }

        if (File.Exists(path)) {
            Console.ForegroundColor = state.colorTheme.textDefault;
            _writer.Write("File already exists, continue? [y/n] ");
            var response = Console.ReadKey().KeyChar;
            _writer.WriteLine();

            if (response != 'y') {
                _writer.WriteLine("Aborting");
                return;
            }
        }

        var submissions = GetSubmissionHistory();

        if (countInt > submissions.Count)
            countInt = submissions.Count;

        var subset = submissions.GetRange(submissions.Count - countInt, countInt);

        var joined = string.Join(Environment.NewLine, subset);
        var split = joined.Split(Environment.NewLine);

        var wrote = false;

        for (int i=0; i<3; i++) {
            try {
                File.WriteAllLines(path, subset);
                wrote = true;
                break;
            } catch (Exception) {
                // In case file is being used by another process, retry
                Thread.Sleep(100);

                // If there is an exception unrelated to the file being used, it should be shown
                if (i == 2)
                    throw;
            }
        }

        Console.ForegroundColor = state.colorTheme.textDefault;

        if (wrote)
            _writer.WriteLine($"Wrote {split.Length} lines");
        else
            _writer.WriteLine($"Failed to write to file");
    }

    [MetaCommand("settings", "Open settings page")]
    private void EvaluateSettings() {
        state.currentPage = Page.Settings;

        void UpdatePage(int targetIndex) {
            targetIndex -= 2;
            state.colorTheme = InUse[InUse.Keys.ToArray()[targetIndex]];

            Console.BackgroundColor = state.colorTheme.background;
            Console.ForegroundColor = state.colorTheme.textDefault;
            Console.Clear();
            _writer.WriteLine("Settings");
            _writer.WriteLine();
            _writer.Write("Theme: ");

            var index = 2;

            foreach (var (Key, Value) in InUse) {
                _writer.SetCursorPosition(7, index++);

                if (state.colorTheme.GetType() == Value.GetType()) {
                    Console.BackgroundColor = state.colorTheme.selection;
                } else {
                    Console.BackgroundColor = state.colorTheme.background;
                }

                _writer.Write(Key.PadRight(8));
            }

            _writer.SetCursorPosition(7, targetIndex + 2);
        }

        var targetIndex = 2;
        var index = 2;

        foreach (var (Key, Value) in InUse) {
            if (state.colorTheme.GetType() == Value.GetType())
                targetIndex = index;
            else
                index++;
        }

        while (true) {
            UpdatePage(targetIndex);

            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Enter) {
                Console.BackgroundColor = state.colorTheme.background;
                break;
            } else if (key.Key == ConsoleKey.UpArrow) {
                if (targetIndex - 2 > 0)
                    targetIndex--;
            } else if (key.Key == ConsoleKey.DownArrow) {
                if (targetIndex - 2 < InUse.Count - 1)
                    targetIndex++;
            }
        }

        Console.BackgroundColor = state.colorTheme.background;
        ReviveDocument();
        state.currentPage = Page.Repl;
    }

    [MetaCommand("showWarnings", "Toggle to display compiler produced warnings")]
    private void EvaluateShowWarnings() {
        state.showWarnings = !state.showWarnings;
        _writer.WriteLine(state.showWarnings ? "Warnings shown" : "Warnings ignored");
    }

    [MetaCommand("showTime", "Toggle to display execution time of entries")]
    private void EvaluateShowTime() {
        _showTime = !_showTime;
        _writer.WriteLine(_showTime ? "Execution time visible" : "Execution time hidden");
    }

    /// <summary>
    /// All required fields to implement for a REPL color theme (only supported if using System.Console as out).
    /// </summary>
    internal abstract class ColorTheme {
        /// <summary>
        /// Default color to result to for unformatted text.
        /// </summary>
        internal abstract ConsoleColor @default { get; }

        /// <summary>
        /// Background color to indicate selected text.
        /// </summary>
        internal abstract ConsoleColor selection { get; }

        /// <summary>
        /// Default color for text with no special color.
        /// </summary>
        internal abstract ConsoleColor textDefault { get; }

        /// <summary>
        /// Color of all results.
        /// </summary>
        internal abstract ConsoleColor result { get; }

        /// <summary>
        /// Background color of terminal.
        /// </summary>
        internal abstract ConsoleColor background { get; }

        /// <summary>
        /// Color of identifer tokens.
        /// </summary>
        internal abstract ConsoleColor identifier { get; }

        /// <summary>
        /// Color of number literals.
        /// </summary>
        internal abstract ConsoleColor number { get; }

        /// <summary>
        /// Color of string literals.
        /// </summary>
        internal abstract ConsoleColor @string { get; }

        /// <summary>
        /// Color of comments (all types).
        /// </summary>
        internal abstract ConsoleColor comment { get; }

        /// <summary>
        /// Color of keywords.
        /// </summary>
        internal abstract ConsoleColor keyword { get; }

        /// <summary>
        /// Color of type names (not full type clauses).
        /// </summary>
        internal abstract ConsoleColor typeName { get; }

        /// <summary>
        /// Color any other code text.
        /// </summary>
        internal abstract ConsoleColor text { get; }

        /// <summary>
        /// Color of code text that could not parse.
        /// </summary>
        internal abstract ConsoleColor errorText { get; }
    }

    /// <summary>
    /// Dark theme (default). Mostly dark colors and pairs well with dark themed terminals.
    /// </summary>
    internal sealed class DarkTheme : ColorTheme {
        internal override ConsoleColor @default => ConsoleColor.DarkGray;
        internal override ConsoleColor selection => ConsoleColor.DarkGray;
        internal override ConsoleColor textDefault => ConsoleColor.White;
        internal override ConsoleColor result => ConsoleColor.White;
        internal override ConsoleColor background => ConsoleColor.Black;
        internal override ConsoleColor identifier => ConsoleColor.White;
        internal override ConsoleColor number => ConsoleColor.Cyan;
        internal override ConsoleColor @string => ConsoleColor.Yellow;
        internal override ConsoleColor comment => ConsoleColor.DarkGray;
        internal override ConsoleColor keyword => ConsoleColor.Blue;
        internal override ConsoleColor typeName => ConsoleColor.Blue;
        internal override ConsoleColor text => ConsoleColor.DarkGray;
        internal override ConsoleColor errorText => ConsoleColor.White;
    }

    /// <summary>
    /// Light theme. Mostly bright colors and pairs well with light themed terminals.
    /// </summary>
    internal sealed class LightTheme : ColorTheme {
        internal override ConsoleColor @default => ConsoleColor.DarkGray;
        internal override ConsoleColor selection => ConsoleColor.DarkGray;
        internal override ConsoleColor textDefault => ConsoleColor.Black;
        internal override ConsoleColor result => ConsoleColor.Black;
        internal override ConsoleColor background => ConsoleColor.White;
        internal override ConsoleColor identifier => ConsoleColor.Black;
        internal override ConsoleColor number => ConsoleColor.DarkCyan;
        internal override ConsoleColor @string => ConsoleColor.DarkYellow;
        internal override ConsoleColor comment => ConsoleColor.DarkGray;
        internal override ConsoleColor keyword => ConsoleColor.DarkBlue;
        internal override ConsoleColor typeName => ConsoleColor.DarkBlue;
        internal override ConsoleColor text => ConsoleColor.DarkGray;
        internal override ConsoleColor errorText => ConsoleColor.Black;
    }

    /// <summary>
    /// Green theme. Mostly dark colors with green background.
    /// </summary>
    internal sealed class GreenTheme : ColorTheme {
        internal override ConsoleColor @default => ConsoleColor.DarkGray;
        internal override ConsoleColor selection => ConsoleColor.DarkGray;
        internal override ConsoleColor textDefault => ConsoleColor.Black;
        internal override ConsoleColor result => ConsoleColor.DarkGreen;
        internal override ConsoleColor background => ConsoleColor.Green;
        internal override ConsoleColor identifier => ConsoleColor.White;
        internal override ConsoleColor number => ConsoleColor.DarkCyan;
        internal override ConsoleColor @string => ConsoleColor.DarkMagenta;
        internal override ConsoleColor comment => ConsoleColor.DarkGray;
        internal override ConsoleColor keyword => ConsoleColor.DarkBlue;
        internal override ConsoleColor typeName => ConsoleColor.Red;
        internal override ConsoleColor text => ConsoleColor.DarkGray;
        internal override ConsoleColor errorText => ConsoleColor.Gray;
    }

    /// <summary>
    /// REPL specific state, maintained throughout instance, recreated every instance.
    /// </summary>
    internal sealed class BelteReplState {
        /// <summary>
        /// Show the parse tree after a submission.
        /// </summary>
        public bool showTree = false;

        /// <summary>
        /// Show the lowered code after a submission.
        /// </summary>
        public bool showProgram = false;

        /// <summary>
        /// Show compiler produced warnings.
        /// </summary>
        public bool showWarnings = false;

        /// <summary>
        /// If to ignore statements with side effects (Print, PrintLine, etc.).
        /// </summary>
        public bool loadingSubmissions = false;

        /// <summary>
        /// What color theme to use (can change).
        /// </summary>
        public ColorTheme colorTheme = new DarkTheme();

        /// <summary>
        /// Current <see cref="Page" /> the user is viewing.
        /// </summary>
        public Page currentPage = Page.Repl;

        /// <summary>
        /// Previous <see cref="Compilation" /> (used to build of previous).
        /// </summary>
        public Compilation previous;

        /// <summary>
        /// Current defined variables.
        /// Not tracked after REPL instance is over, instead previous submissions are reevaluated.
        /// </summary>
        public Dictionary<VariableSymbol, EvaluatorObject> variables;
    }
}

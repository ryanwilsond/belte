using Buckle;
using Buckle.IO;
using Buckle.Diagnostics;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Authoring;
using Diagnostics;

namespace Repl;

public sealed class BelteRepl : ReplBase {
    private static readonly Compilation emptyCompilation = Compilation.CreateScript(null);
    internal override object state_ { get; set; }
    internal BelteReplState state { get { return (BelteReplState)state_; } set { state_=value; } }

    internal abstract class ColorTheme {
        internal abstract ConsoleColor @default { get; }
        internal abstract ConsoleColor selection { get; }
        internal abstract ConsoleColor textDefault { get; }
        internal abstract ConsoleColor result { get; }
        internal abstract ConsoleColor background { get; }
        internal abstract ConsoleColor identifier { get; }
        internal abstract ConsoleColor number { get; }
        internal abstract ConsoleColor @string { get; }
        internal abstract ConsoleColor comment { get; }
        internal abstract ConsoleColor keyword { get; }
        internal abstract ConsoleColor typeName { get; }
        internal abstract ConsoleColor text { get; }
        internal abstract ConsoleColor errorText { get; }
    }

    internal class DarkTheme : ColorTheme {
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

    internal class LightTheme : ColorTheme {
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

    internal enum Page {
        Repl,
        Settings
    }

    internal sealed class BelteReplState {
        public bool showTree = false;
        public bool showProgram = false;
        public bool loadingSubmissions = false;
        public ColorTheme colorTheme = new DarkTheme();
        public Page currentPage = Page.Repl;
        public Compilation previous;
        public Dictionary<VariableSymbol, object> variables;
    }

    public BelteRepl(Compiler handle, DiagnosticHandle errorHandle) : base(handle, errorHandle) {
        state = new BelteReplState();
        ResetState();
        Console.BackgroundColor = state.colorTheme.background;
        EvaluateClear();
        LoadSubmissions();
    }

    internal override void ResetState() {
        state.showTree = false;
        state.showProgram = false;
        state.loadingSubmissions = false;
        state.variables = new Dictionary<VariableSymbol, object>();
        state.previous = null;
        state.currentPage = Page.Repl;
        base.ResetState();
    }

    protected override void EvaluateSubmission(string text) {
        var syntaxTree = SyntaxTree.Parse(text);
        var compilation = Compilation.CreateScript(state.previous, syntaxTree);

        if (state.showTree)
            syntaxTree.root.WriteTo(Console.Out);
        if (state.showProgram)
            compilation.EmitTree(Console.Out);

        handle.diagnostics.Move(compilation.diagnostics.FilterOut(DiagnosticType.Warning));
        EvaluationResult result = null;

        if (!handle.diagnostics.Any()) {
            result = compilation.Evaluate(state.variables);
            handle.diagnostics.Move(result.diagnostics.FilterOut(DiagnosticType.Warning));
        }

        if (handle.diagnostics.Any()) {
            if (diagnosticHandle != null) {
                handle.diagnostics = BelteDiagnosticQueue.CleanDiagnostics(handle.diagnostics);
                diagnosticHandle(handle, textColor: state.colorTheme.textDefault);
            } else {
                handle.diagnostics.Clear();
            }
        } else {
            if (result.value != null && !state.loadingSubmissions) {
                Console.ForegroundColor = state.colorTheme.result;
                RenderResult(result.value);
                writer_.WriteLine();
                Console.ForegroundColor = state.colorTheme.@default;
            }

            state.previous = compilation;
            SaveSubmission(text);
        }
    }

    private void RenderResult(object value) {
        if (value.GetType().IsArray) {
            writer_.Write("{ ");
            var isFirst = true;

            foreach (object item in (Array)value) {
                if (isFirst)
                    isFirst = false;
                else
                    writer_.Write(", ");

                RenderResult(item);
            }

            writer_.Write(" }");
        } else {
            writer_.Write(value);
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

    private static void ClearSubmissions() {
        var path = GetSubmissionsDirectory();

        if (Directory.Exists(path))
            Directory.Delete(GetSubmissionsDirectory(), true);
    }

    private void LoadSubmissions() {
        var files = Directory.GetFiles(GetSubmissionsDirectory()).OrderBy(f => f).ToArray();
        var keyword = files.Length == 1 ? "submission" : "submissions";
        Console.Out.WritePunctuation($"loaded {files.Length} {keyword}");
        writer_.WriteLine();

        state.loadingSubmissions = true;

        foreach (var file in files) {
            var text = File.ReadAllText(file);
            EvaluateSubmission(text);
        }

        state.loadingSubmissions = false;
    }

    private static string GetSubmissionsDirectory() {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var submissionsFolder = Path.Combine(localAppData, "Buckle", "Submissions");

        if (!Directory.Exists(submissionsFolder))
            Directory.CreateDirectory(submissionsFolder);

        return submissionsFolder;
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
        var textsLength = String.Join("", pureTexts).Length;

        if (textsLength < fullText.Length)
            texts.Add((fullText.Substring(textsLength), state.colorTheme.errorText));

        foreach (var text in texts) {
            Console.ForegroundColor = text.color;
            writer_.Write(text.text);
        }

        Console.ForegroundColor = state.colorTheme.@default;

        return syntaxTree;
    }

    [MetaCommand("showTree", "Toggle to display parse tree of each input")]
    private void EvaluateShowTree() {
        state.showTree = !state.showTree;
        writer_.WriteLine(state.showTree ? "Parse-trees visible" : "Parse-trees hidden");
    }

    [MetaCommand("showProgram", "Toggle to display intermediate representation of each input")]
    private void EvaluateShowProgram() {
        state.showProgram = !state.showProgram;
        writer_.WriteLine(state.showProgram ? "Bound-trees visible" : "Bound-trees hidden");
    }

    [MetaCommand("clear", "Clears the screen")]
    private void EvaluateClear() {
        Console.Clear();
    }

    // TODO @Logan add a meta command called 'cls' that clears the terminal

    [MetaCommand("reset", "Clears previous submissions")]
    private void EvaluateReset() {
        ResetState();
        ClearSubmissions();
    }

    [MetaCommand("load", "Loads in text from file")]
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

    [MetaCommand("ls", "Lists all defined symbols")]
    private void EvaluateLs() {
        var compilation = state.previous ?? emptyCompilation;
        var symbols = compilation.GetSymbols().OrderBy(s => s.type).ThenBy(s => s.name);

        foreach (var symbol in symbols) {
            symbol.WriteTo(Console.Out);
            writer_.WriteLine();
        }
    }

    [MetaCommand("dump", "Shows contents of a symbol")]
    private void EvaluateDump(string name) {
        var compilation = state.previous ?? emptyCompilation;
        var symbol = compilation.GetSymbols().SingleOrDefault(f => f.name == name);

        if (symbol == null) {
            handle.diagnostics.Push(new BelteDiagnostic(Repl.Diagnostics.Error.UndefinedSymbol(name)));

            if (diagnosticHandle != null)
                diagnosticHandle(handle, "repl", state.colorTheme.textDefault);
            else
                handle.diagnostics.Clear();

            return;
        }

        compilation.EmitTree(symbol, Console.Out);
    }

    [MetaCommand("exit", "Exits the repl")]
    private void EvaluateExit() {
        Environment.Exit(0);
    }

    [MetaCommand("settings", "Opens settings page")]
    private void EvaluateSettings() {
        state.currentPage = Page.Settings;

        void UpdatePage() {
            Console.BackgroundColor = state.colorTheme.background;
            Console.ForegroundColor = state.colorTheme.textDefault;
            Console.Clear();
            writer_.WriteLine("Settings\n");
            writer_.Write("Theme: ");

            if (state.colorTheme is DarkTheme) {
                writer_.SetCursorPosition(7, 2);
                Console.BackgroundColor = state.colorTheme.background;
                writer_.Write("Light   ");
                writer_.SetCursorPosition(7, 3);
                Console.BackgroundColor = state.colorTheme.selection;
                writer_.Write("Dark    ");
            } else if (state.colorTheme is LightTheme) {
                writer_.SetCursorPosition(7, 2);
                Console.BackgroundColor = state.colorTheme.selection;
                writer_.Write("Light   ");
                writer_.SetCursorPosition(7, 3);
                Console.BackgroundColor = state.colorTheme.background;
                writer_.Write("Dark    ");
            }
        }

        while (true) {
            UpdatePage();

            if (state.colorTheme is LightTheme)
                writer_.SetCursorPosition(7, 2);
            else if (state.colorTheme is DarkTheme)
                writer_.SetCursorPosition(7, 3);

            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Enter) {
                Console.BackgroundColor = state.colorTheme.background;
                break;
            } else if (key.Key == ConsoleKey.UpArrow) {
                state.colorTheme = new LightTheme();
                writer_.SetCursorPosition(7, 2);
            } else if (key.Key == ConsoleKey.DownArrow) {
                state.colorTheme = new DarkTheme();
                writer_.SetCursorPosition(7, 3);
            }
        }

        Console.BackgroundColor = state.colorTheme.background;
        ReviveDocument();
        state.currentPage = Page.Repl;
    }

    protected override bool IsCompleteSubmission(string text) {
        if (string.IsNullOrEmpty(text))
            return true;

        var twoBlankTines = text.Split(Environment.NewLine).Reverse()
            .TakeWhile(s => string.IsNullOrEmpty(s))
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
}

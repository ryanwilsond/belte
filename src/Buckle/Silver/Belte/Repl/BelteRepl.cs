using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Buckle;
using Buckle.IO;
using Buckle.Diagnostics;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Authoring;

namespace Belte.Repl;

public sealed class BelteRepl : Repl {
    private static readonly Compilation emptyCompilation = Compilation.CreateScript(null);
    internal override object state_ { get; set; }
    internal BelteReplState state { get { return (BelteReplState)state_; } set { state_=value; } }

    internal sealed class BelteReplState {
        public bool showTree = false;
        public bool showProgram = false;
        public bool loadingSubmissions = false;
        public Compilation previous;
        public Dictionary<VariableSymbol, object> variables;
    }

    public BelteRepl(Compiler handle, DiagnosticHandle errorHandle) : base(handle, errorHandle) {
        state = new BelteReplState();
        ResetState();
        EvaluateClear();
        LoadSubmissions();
    }

    internal override void ResetState() {
        state.showTree = false;
        state.showProgram = false;
        state.loadingSubmissions = false;
        state.variables = new Dictionary<VariableSymbol, object>();
        state.previous = null;
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
                handle.diagnostics = DiagnosticQueue.CleanDiagnostics(handle.diagnostics);
                diagnosticHandle(handle);
            } else {
                handle.diagnostics.Clear();
            }
        } else {
            if (result.value != null && !state.loadingSubmissions) {
                Console.ForegroundColor = ConsoleColor.White;
                RenderResult(result.value);
                Console.WriteLine();
                Console.ResetColor();
            }

            state.previous = compilation;
            SaveSubmission(text);
        }
    }

    private void RenderResult(object value) {
        if (value.GetType().IsArray) {
            Console.Write("{ ");
            var isFirst = true;

            foreach (object item in (Array)value) {
                if (isFirst)
                    isFirst = false;
                else
                    Console.Write(", ");

                RenderResult(item);
            }

            Console.Write(" }");
        } else {
            Console.Write(value);
        }
    }

    private void SaveSubmission(string text) {
        if (state.loadingSubmissions)
            return;

        var submissionsFolder = GetSumbissionsDirectory();
        var count = Directory.GetFiles(submissionsFolder).Length;
        var name = $"submission{count:0000}";
        var fileName = Path.Combine(submissionsFolder, name);
        File.WriteAllText(fileName, text);
    }

    private static void ClearSubmissions() {
        var path = GetSumbissionsDirectory();
        if (Directory.Exists(path))
            Directory.Delete(GetSumbissionsDirectory(), true);
    }

    private void LoadSubmissions() {
        var files = Directory.GetFiles(GetSumbissionsDirectory()).OrderBy(f => f).ToArray();
        var keyword = files.Length == 1 ? "submission" : "submissions";
        Console.Out.WritePunctuation($"loaded {files.Length} {keyword}");
        Console.WriteLine();

        state.loadingSubmissions = true;

        foreach (var file in files) {
            var text = File.ReadAllText(file);
            EvaluateSubmission(text);
        }

        state.loadingSubmissions = false;
    }

    private static string GetSumbissionsDirectory() {
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
            var color = ConsoleColor.DarkGray;

            switch (classifiedSpan.classification) {
                case Classification.Identifier:
                    color = ConsoleColor.White;
                    break;
                case Classification.Number:
                    color = ConsoleColor.Cyan;
                    break;
                case Classification.String:
                    color = ConsoleColor.Yellow;
                    break;
                case Classification.Comment:
                    color = ConsoleColor.DarkGray;
                    break;
                case Classification.Keyword:
                    color = ConsoleColor.Blue;
                    break;
                case Classification.TypeName:
                    color = ConsoleColor.Blue;
                    break;
                case Classification.Text:
                    color = ConsoleColor.DarkGray;
                    break;
                default:
                    break;
            }

            texts.Add((classifiedText, color));
        }

        int offset = 0;

        for (int i=0; i<texts.Count(); i++) {
            var line = texts[i].text;

            if (fullText.Substring(offset, line.Length) == line) {
                offset += line.Length;
            } else {
                string extra = "";

                while (true) {
                    if (fullText.Substring(offset, texts[i].text.Length) == texts[i].text) {
                        texts.Insert(i, (extra, ConsoleColor.White));
                        break;
                    }

                    extra += fullText[offset++];
                }
            }
        }

        if (texts.Count() == 0)
            texts.Add((fullText, ConsoleColor.White));

        foreach (var text in texts) {
            Console.ForegroundColor = text.color;
            Console.Write(text.text);
        }

        Console.ResetColor();

        return syntaxTree;
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
        ClearSubmissions();
    }

    [MetaCommand("load", "Loads in text from file")]
    private void EvaluateLoad(string path) {
        if (!File.Exists(path)) {
            handle.diagnostics.Push(DiagnosticType.Error, $"{path}: no such file");

            if (diagnosticHandle != null)
                diagnosticHandle(handle, "repl");
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
            Console.WriteLine();
        }
    }

    [MetaCommand("dump", "Shows contents of a symbol")]
    private void EvaluateDump(string name) {
        var compilation = state.previous ?? emptyCompilation;
        var symbol = compilation.GetSymbols().SingleOrDefault(f => f.name == name);

        if (symbol == null) {
            handle.diagnostics.Push(DiagnosticType.Error, $"undefined symbol '{name}'");

            if (diagnosticHandle != null)
                diagnosticHandle(handle, "repl");
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

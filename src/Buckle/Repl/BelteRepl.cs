using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using Buckle;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Authoring;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Evaluating;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Diagnostics;
using Repl.Themes;
using static Buckle.CodeAnalysis.Display.DisplayTextSegment;

namespace Repl;

/// <summary>
/// Uses framework from <see cref="Repl" /> and adds syntax highlighting and evaluation.
/// </summary>
public sealed partial class BelteRepl : Repl {
    private static readonly CompilationOptions DefaultOptions =
        new CompilationOptions(BuildMode.Repl, OutputKind.Console, [], true, false);
    // TODO Any benefit to generating numbered assembly names so they are unique?
    private static readonly Compilation EmptyCompilation = Compilation.CreateScript("ReplSubmission", DefaultOptions);
    private static readonly ImmutableArray<(string name, string contributor, ColorTheme theme)> InUse =
        [
            ("Dark", "", new DarkTheme()),
            ("Light", "", new LightTheme()),
            ("Green", "Abiral Shakya", new GreenTheme()),
            ("Purpura", "Logan Kuz", new PurpuraTheme()),
            ("Traffic Stop", "Jason Pelkey", new TrafficStopTheme()),
        ];

    private List<TextChange> _changes = new List<TextChange>();

    private DiagnosticHandle _diagnosticHandle;

    /// <summary>
    /// Creates a new instance of a <see cref="BelteRepl" />, can run in parallel with other BelteRepls with
    /// unique outs.
    /// Uses System.Console by default, which cannot be used more than once.
    /// </summary>
    /// <param name="handle"><see cref="Compiler" /> object that represents entirety of compilation.</param>
    /// <param name="errorHandle">Callback to handle Diagnostics.</param>
    public BelteRepl(Compiler handle, DiagnosticHandle errorHandle) : base(handle) {
        state = new BelteReplState();
        _diagnosticHandle = errorHandle;
        _hasDiagnosticHandle = true;
        var diagnostics = LoadLibraries();
        ResetState();
        Console.BackgroundColor = state.colorTheme.background;
        EvaluateClear();

        if (diagnostics.AnyErrors()) {
            handle.AddLibraryErrors(diagnostics);
            errorHandle(handle, "repl");
        } else {
            LoadSubmissions();
        }
    }

    /// <summary>
    /// Callback to handle Diagnostics, be it logging or displaying to the console.
    /// </summary>
    /// <param name="handle">Handle object representing entirety of compilation.</param>
    /// <param name="me">Display name of the program.</param>
    /// <param name="textColor">Color to display Diagnostics (if displaying).</param>
    /// <returns>C-Style error code of most severe Diagnostic.</returns>
    public delegate int DiagnosticHandle(
        Compiler handle, string me = null, ConsoleColor textColor = ConsoleColor.White);

    /// <summary>
    /// Cast of <see cref="Repl" /> specific state that has <see cref="BelteRepl" /> related state.
    /// </summary>
    internal BelteReplState state {
        get {
            return _state as BelteReplState;
        }
        set {
            _state = value;
        }
    }

    /// <summary>
    /// Cast of <see cref="Repl._handle" /> that is a <see cref="Compiler" /> object.
    /// </summary>
    /// <value></value>
    internal Compiler handle {
        get {
            return _handle as Compiler;
        }
        set {
            _handle = value;
        }
    }

    internal override void ResetState() {
        state.showTokens = false;
        state.showTree = false;
        state.showProgram = false;
        state.showWarnings = false;
        state.showIL = false;
        state.showCS = false;
        state.loadingSubmissions = false;
        state.context = new EvaluatorContext();
        state.previous = state.baseCompilation;
        state.currentPage = Page.Repl;
        _changes.Clear();
        ClearTree();
        base.ResetState();
    }

    private protected override void RenderLine(IReadOnlyList<string> lines, int lineIndex) {
        UpdateTree();

        var texts = new List<(string text, ConsoleColor color)>();
        var lineSpan = state.tree.text.GetLine(lineIndex).span;
        var fullText = state.tree.text.ToString(lineSpan);

        var classifiedSpans = Classifier.Classify(state.tree, lineSpan);

        foreach (var classifiedSpan in classifiedSpans) {
            var classifiedText = state.tree.text.ToString(classifiedSpan.span);
            var color = GetColorFromClassification(classifiedSpan.classification);
            texts.Add((classifiedText, color));
        }

        var offset = 0;

        for (var i = 0; i < texts.Count; i++) {
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

        if (texts.Count == 0)
            texts.Add((fullText, state.colorTheme.errorText));

        var pureTexts = texts.Select(t => t.text).ToList();
        var textsLength = string.Join("", pureTexts).Length;

        if (textsLength < fullText.Length)
            texts.Add((fullText.Substring(textsLength), state.colorTheme.errorText));

        foreach (var text in texts) {
            Console.ForegroundColor = text.color;
            writer.Write(text.text);
        }

        Console.ForegroundColor = state.colorTheme.@default;
    }
    private protected override void EvaluateSubmission(string text) {
        // ONLY use this when evaluating previous submissions, where incremental compilation would do nothing
        // Otherwise, this is much slower than the 0 arity overload
        var syntaxTree = SyntaxTree.Parse(text, SourceCodeKind.Script);
        EvaluateSubmissionInternal(syntaxTree);
    }

    private protected override void EvaluateSubmission() {
        UpdateTree();
        EvaluateSubmissionInternal(state.tree);
        ClearTree();
    }

    private protected override string EditSubmission() {
        ClearTree();
        return base.EditSubmission();
    }

    private protected override void AddChange(
        ObservableCollection<string> document, int lineIndex, int startIndex, int oldLength, string newText) {
        var position = startIndex;

        for (var i = 0; i < lineIndex; i++)
            position += document[i].Length + Environment.NewLine.Length;

        _changes.Add(new TextChange(new TextSpan(position, oldLength), newText));
    }

    private protected override void AddClearChange(ObservableCollection<string> document) {
        ClearTree();
    }

    private protected override void AddRemoveLineChange(ObservableCollection<string> document, int lineIndex) {
        var position = 0;

        for (var i = 0; i < lineIndex; i++) {
            position += document[i].Length;

            if (i > 0)
                position += Environment.NewLine.Length;
        }

        _changes.Add(
            new TextChange(new TextSpan(position, document[lineIndex].Length + Environment.NewLine.Length), "")
        );
    }

    private protected override bool IsCompleteSubmission(string text) {
        if (string.IsNullOrEmpty(text))
            return true;

        var lines = text.Split(Environment.NewLine);
        lines.Reverse();

        var twoBlankLines = lines
            .TakeWhile(s => string.IsNullOrEmpty(s) || string.IsNullOrWhiteSpace(s))
            .Take(2)
            .Count() == 2;

        if (twoBlankLines)
            return true;

        UpdateTree();
        var lastMember = state.tree.GetCompilationUnitRoot().members.LastOrDefault();

        if (lastMember is null || lastMember.GetLastToken(includeZeroWidth: true).isFabricated)
            return false;

        return true;
    }

    private protected override void AddDiagnostic(Diagnostic diagnostic) {
        handle.diagnostics.Push(diagnostic);
    }

    private protected override void ClearDiagnostics() {
        handle.diagnostics.Clear();
    }

    private protected override void CallDiagnosticHandle(object handle, object arg1 = null, object arg2 = null) {
        if (arg2 is null)
            _diagnosticHandle(handle as Compiler, arg1 is null ? null : arg1 as string);
        else
            _diagnosticHandle(handle as Compiler, arg1 is null ? null : arg1 as string, (ConsoleColor)arg2);
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

    private void ClearTree() {
        state.tree = SyntaxTree.Parse("", SourceCodeKind.Script);
        // This should always be empty by now, but just in case there was a race condition
        _changes.Clear();
    }

    private void IterateTokens(SyntaxNodeOrToken node, DisplayText text) {
        if (node.isToken) {
            SyntaxTokenExtensions.PrettyPrint(text, node.AsToken());
            text.Write(CreateSpace());
        }

        if (node.isNode) {
            foreach (var child in node.ChildNodesAndTokens())
                IterateTokens(child, text);
        }
    }

    private BelteDiagnosticQueue LoadLibraries() {
        var compilation = CompilerHelpers.LoadLibraries();
        state.baseCompilation = compilation;
        // compilation.Evaluate(_abortEvaluation);
        return compilation.GetDiagnostics();
    }

    private void EvaluateSubmissionInternal(SyntaxTree syntaxTree) {
        var compilation = Compilation.CreateScript("ReplSubmission", DefaultOptions, syntaxTree, state.previous);
        var displayText = new DisplayText();

        if (state.showTokens) {
            IterateTokens(syntaxTree.GetRoot(), displayText);
            displayText.WriteLine();
            WriteDisplayText(displayText);
        }

        if (state.showTree) {
            SyntaxNodeExtensions.PrettyPrint(displayText, syntaxTree.GetRoot());
            WriteDisplayText(displayText);
        }

        if (state.showProgram) {
            compilation.EmitTree(displayText);
            WriteDisplayText(displayText);
        }

        if (state.showCS) {
            var code = compilation.EmitToString(out _, BuildMode.CSharpTranspile);
            writer.Write(code);
        }

        if (state.showIL) {
            try {
                var code = compilation.EmitToString(out _, BuildMode.Dotnet);
                writer.Write(code);
            } catch (KeyNotFoundException) {
                handle.diagnostics.Push(new BelteDiagnostic(Diagnostics.Error.FailedILGeneration()));
            }
        }

        var diagnostics = compilation.GetDiagnostics();

        if (state.showWarnings)
            handle.diagnostics.Move(diagnostics);
        else
            handle.diagnostics.Move(diagnostics.Errors());

        EvaluationResult result = null;
        Console.ForegroundColor = state.colorTheme.result;

        if (!handle.diagnostics.AnyErrors()) {
            result = compilation.Evaluate(state.context, _abortEvaluation);

            if (result.lastOutputWasPrint)
                writer.WriteLine();

            if (_abortEvaluation) {
                Console.ForegroundColor = state.colorTheme.@default;
                return;
            }

            if (state.showWarnings)
                handle.diagnostics.Move(result.diagnostics);
            else
                handle.diagnostics.Move(result.diagnostics.Errors());
        }

        var hasErrors = handle.diagnostics.AnyErrors();

        if (handle.diagnostics.Any()) {
            if (_hasDiagnosticHandle) {
                // ? View the todo marker in BelteDiagnosticQueue.CleanDiagnostics
                // handle.diagnostics = BelteDiagnosticQueue.CleanDiagnostics(handle.diagnostics);
                _diagnosticHandle(handle, textColor: state.colorTheme.textDefault);
            } else {
                handle.diagnostics.Clear();
            }
        }

        if (!hasErrors) {
            if (result.hasValue && !state.loadingSubmissions) {
                RenderResult(result.value);
                writer.WriteLine();
            }

            state.previous = compilation;

            if (!result.containsIO)
                SaveSubmission(syntaxTree.text.ToString());
        }

        Console.ForegroundColor = state.colorTheme.@default;
        Console.BackgroundColor = state.colorTheme.background;
    }

    private ConsoleColor GetColorFromClassification(Classification classification) {
        return classification switch {
            Classification.Identifier => state.colorTheme.identifier,
            Classification.Keyword => state.colorTheme.keyword,
            Classification.Type => state.colorTheme.typeName,
            Classification.Literal => state.colorTheme.literal,
            Classification.String => state.colorTheme.@string,
            Classification.Comment => state.colorTheme.comment,
            Classification.Text => state.colorTheme.text,
            Classification.Escape => state.colorTheme.escape,
            Classification.RedNode => state.colorTheme.redNode,
            Classification.GreenNode => state.colorTheme.greenNode,
            Classification.BlueNode => state.colorTheme.blueNode,
            _ => state.colorTheme.@default,
        };
    }

    private void UpdateTree() {
        var changes = _changes.ToArray();
        _changes.Clear();
        state.tree = state.tree.WithChanges(changes);
    }

    private void RenderResult(object value) {
        Console.ForegroundColor = state.colorTheme.result;
        var displayText = new DisplayText();

        if (value is null) {
            displayText.Write(CreatePunctuation("null"));
        } else if (value.GetType().IsArray) {
            writer.Write("{ ");
            var isFirst = true;

            foreach (var item in (Array)value) {
                if (isFirst)
                    isFirst = false;
                else
                    writer.Write(", ");

                RenderResult(item);
            }

            writer.Write(" }");
        } else if (value is Dictionary<object, object> dictionary) {
            writer.Write("{ ");
            var isFirst = true;

            foreach (var pair in dictionary) {
                if (isFirst)
                    isFirst = false;
                else
                    writer.Write(", ");

                RenderResult(pair.Key);
                writer.Write(": ");
                RenderResult(pair.Value);
            }

            writer.Write(" }");
        } else if (value is ISymbol symbol) {
            displayText.Write(CreateKeyword(SyntaxKind.RefKeyword));
            displayText.Write(CreateSpace());
            SymbolDisplay.AppendToDisplayText(displayText, symbol, SymbolDisplayFormat.QualifiedNameFormat);
        } else {
            writer.Write(value);
        }

        WriteDisplayText(displayText);
        Console.ForegroundColor = state.colorTheme.result;
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

        var displayText = new DisplayText();
        displayText.Write(CreatePunctuation($"loaded {files.Length} {keyword}"));
        displayText.WriteLine();
        WriteDisplayText(displayText);

        var @out = Console.Out;
        var @in = Console.In;
        Console.SetOut(new StreamWriter(Stream.Null));
        Console.SetIn(TextReader.Null);
        state.loadingSubmissions = true;

        foreach (var file in files) {
            var text = File.ReadAllText(file);
            EvaluateSubmission(text);
            _submissionHistory.Add(text);
        }

        state.loadingSubmissions = false;
        Console.SetOut(@out);
        Console.SetIn(@in);
    }

    private void WriteDisplayText(DisplayText text) {
        var segments = text.Flush();

        foreach (var segment in segments) {
            Console.ForegroundColor = GetColorFromClassification(segment.classification);

            if (segment.classification == Classification.Line)
                writer.WriteLine();
            else if (segment.classification == Classification.Indent)
                writer.Write(new string(' ', TabWidth));
            else
                writer.Write(segment.text);
        }
    }

    private object EvaluatorObjectToNativeObject(EvaluatorObject evaluatorObject) {
        if (evaluatorObject.isReference)
            return evaluatorObject.publicReference;

        var value = evaluatorObject.value;

        if (value is EvaluatorObject e)
            return EvaluatorObjectToNativeObject(e);
        else if (value is EvaluatorObject[])
            return CollectionValue(value as EvaluatorObject[]);

        var members = evaluatorObject.publicMembers;

        if (value is null && members is not null)
            return DictionaryValue(members, evaluatorObject.publicType);

        return value;
    }

    private Dictionary<object, object> DictionaryValue(Dictionary<ISymbol, EvaluatorObject> value, ITypeSymbol type) {
        var dictionary = new Dictionary<object, object>();

        foreach (var pair in value) {
            if (pair.Key is IFieldSymbol) {
                var name = pair.Key.containingSymbol.Equals(type)
                    ? pair.Key.name
                    : $"{pair.Key.containingSymbol.name}.{pair.Key.name}";

                dictionary.Add(name, EvaluatorObjectToNativeObject(pair.Value));
            }
        }

        return dictionary;
    }

    private object[] CollectionValue(EvaluatorObject[] value) {
        var builder = new object[value.Length];

        for (var i = 0; i < value.Length; i++)
            builder[i] = EvaluatorObjectToNativeObject(value[i]);

        return builder;
    }

    [MetaCommand("showTree", "Toggle display of the parse tree")]
    private void EvaluateShowTree() {
        state.showTree = !state.showTree;
        writer.WriteLine(state.showTree ? "Parse trees visible" : "Parse trees hidden");
    }

    [MetaCommand("showTokens", "Toggle display of syntax tokens")]
    private void EvaluateShowTokens() {
        state.showTokens = !state.showTokens;
        writer.WriteLine(state.showTokens ? "Syntax tokens visible" : "Syntax tokens hidden");
    }

    [MetaCommand("showProgram", "Toggle display of the intermediate representation")]
    private void EvaluateShowProgram() {
        state.showProgram = !state.showProgram;
        writer.WriteLine(state.showProgram ? "Bound trees visible" : "Bound trees hidden");
    }

    [MetaCommand("clear", "Clear the screen")]
    private void EvaluateClear() {
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
            handle.diagnostics.Push(new BelteDiagnostic(Diagnostics.Error.NoSuchFile(path)));

            if (_hasDiagnosticHandle)
                _diagnosticHandle(handle, "repl", state.colorTheme.textDefault);
            else
                handle.diagnostics.Clear();

            return;
        }

        var text = File.ReadAllText(path);
        EvaluateSubmission(text);
    }

    [MetaCommand("list", "List [all|global|type] symbols")]
    private void EvaluateList(string mode = "global") {
        if (mode != "all" && mode != "global" && mode != "type") {
            handle.diagnostics.Push(
                new BelteDiagnostic(Diagnostics.Error.InvalidOption(mode, ["all", "global", "type"]))
            );

            if (_hasDiagnosticHandle)
                _diagnosticHandle(handle, "repl", state.colorTheme.textDefault);
            else
                handle.diagnostics.Clear();

            return;
        }

        var displayText = new DisplayText();

        if (mode == "type" || mode == "all") {
            writer.WriteLine("Type Symbols:");

            var compilation = state.previous ?? EmptyCompilation;
            var symbols = compilation.GetSymbols(true);

            foreach (var symbol in symbols) {
                displayText.Write(CreateIndent());
                SymbolDisplay.AppendToDisplayText(displayText, symbol, SymbolDisplayFormat.Everything);
                displayText.WriteLine();
            }

            WriteDisplayText(displayText);
        }

        if (mode == "all")
            writer.WriteLine();

        if (mode == "global" || mode == "all") {
            writer.WriteLine("Global Symbols:");

            foreach (var symbol in state.context.GetTrackedSymbols()) {
                displayText.Write(CreateIndent());
                SymbolDisplay.AppendToDisplayText(displayText, symbol, SymbolDisplayFormat.Everything);
                displayText.WriteLine();
            }

            WriteDisplayText(displayText);
        }
    }

    [MetaCommand("dump", "Locate a symbol to show the contents of")]
    private void EvaluateDump() {
        state.currentPage = Page.DumpLocator;
        Console.CursorVisible = false;

        var targetIndex = 0;
        var select = false;
        ISymbol currentSymbol = null;
        var currentIsType = false;
        var isAtTop = true;

        var compilation = state.previous ?? EmptyCompilation;
        var topLevelSymbols = compilation.GetSymbols(true);
        var toplevelGlobals = state.context.GetTrackedSymbolsAndObjects();
        var currentSymbols = topLevelSymbols;
        var currentGlobals = toplevelGlobals;
        EvaluatorObject currentGlobal = null;
        Stack<(ISymbol, EvaluatorObject)> globalChain = [];

        while (true) {
            if (UpdatePage(select))
                break;

            var key = Console.ReadKey(true);
            select = false;

            if (key.Key == ConsoleKey.Enter) {
                select = true;
            } else if (key.Key == ConsoleKey.UpArrow) {
                if (targetIndex > 0)
                    targetIndex--;
            } else if (key.Key == ConsoleKey.DownArrow) {
                var pageLength = (currentSymbol is null ? 0 : 2) + currentSymbols.Length + currentGlobals.Count;

                if (targetIndex < pageLength)
                    targetIndex++;
            }
        }

        Console.BackgroundColor = state.colorTheme.background;
        Console.CursorVisible = true;
        ReviveDocument();
        state.currentPage = Page.Repl;

        if (currentSymbol is not null) {
            var displayText = new DisplayText();

            displayText.Write(CreatePunctuation("#"));
            displayText.Write(CreateKeyword("dump "));
            DisplaySymbolName(displayText, currentSymbol);
            displayText.WriteLine();

            if (currentIsType) {
                compilation.EmitTree(currentSymbol, displayText);
                WriteDisplayText(displayText);
            } else {
                SymbolDisplay.AppendToDisplayText(displayText, currentSymbol, SymbolDisplayFormat.Everything);
                displayText.Write(CreatePunctuation(" = "));
                WriteDisplayText(displayText);
                var localValue = EvaluatorObjectToNativeObject(currentGlobal);
                RenderResult(localValue);
                writer.WriteLine();
            }
        }

        bool UpdatePage(bool select) {
            var includeUp = currentSymbol is not null;

            if (select) {
                if (targetIndex == 0) {
                    currentSymbol = null;
                    return true;
                } else if (targetIndex == 1 && includeUp) {
                    if (currentIsType) {
                        currentSymbol = currentSymbol.containingSymbol;

                        if (currentSymbol.kind == SymbolKind.Namespace) {
                            currentSymbol = null;
                            currentSymbols = topLevelSymbols;
                            currentGlobals = toplevelGlobals;
                            isAtTop = true;
                        }
                    } else {
                        if (globalChain.TryPop(out var both)) {
                            currentSymbol = both.Item1;
                            currentGlobal = both.Item2;
                        } else {
                            currentSymbol = null;
                            currentGlobal = null;
                            currentSymbols = topLevelSymbols;
                            currentGlobals = toplevelGlobals;
                            isAtTop = true;
                        }
                    }
                } else if (targetIndex == 2 && includeUp) {
                    return true;
                } else {
                    var trueIndex = targetIndex - (includeUp ? 3 : 1);

                    if (isAtTop) {
                        isAtTop = false;

                        if (trueIndex >= currentSymbols.Length) {
                            currentIsType = false;
                            trueIndex -= currentSymbols.Length;
                            currentSymbols = [];
                        } else {
                            currentGlobals = [];
                            currentIsType = true;
                        }
                    }

                    if (currentIsType) {
                        currentSymbol = currentSymbols[trueIndex];
                    } else {
                        (currentSymbol, currentGlobal) = currentGlobals.ElementAt(trueIndex);
                        globalChain.Push((currentSymbol, currentGlobal));
                    }
                }

                if (currentSymbol is not null) {
                    if (currentIsType) {
                        currentSymbols = currentSymbol switch {
                            INamedTypeSymbol namedType => namedType.GetMembers(),
                            IMethodSymbol method => CompilationExtensions.GetMethodLocals(method)
                                                        .Cast<ISymbol>().ToImmutableArray(),
                            _ => [],
                        };
                    } else {
                        currentGlobals = currentGlobal.publicMembers ?? [];
                    }
                }

                includeUp = currentSymbol is not null;
                targetIndex = includeUp ? 2 : 1;

                if (!includeUp &&
                    ((currentIsType && currentSymbols.Length == 0) || (!currentIsType && currentGlobals.Count == 0))) {
                    targetIndex--;
                }
            }

            Console.BackgroundColor = state.colorTheme.background;
            Console.Clear();
            var pageText = new DisplayText();
            pageText.Write(CreatePunctuation("#"));
            pageText.Write(CreateKeyword("dump "));

            if (currentSymbol is null)
                pageText.Write(CreatePunctuation("[none]"));
            else
                DisplaySymbolName(pageText, currentSymbol);

            WriteDisplayText(pageText);

            writer.WriteLine();

            var index = 2;
            Console.ForegroundColor = state.colorTheme.textDefault;

            if (targetIndex == 0)
                Console.BackgroundColor = state.colorTheme.selection;

            writer.SetCursorPosition(9, index++);
            writer.Write("Exit");
            Console.BackgroundColor = state.colorTheme.background;

            if (includeUp) {
                if (targetIndex == 1)
                    Console.BackgroundColor = state.colorTheme.selection;

                writer.SetCursorPosition(9, index++);
                writer.Write("..");
                Console.BackgroundColor = state.colorTheme.background;

                if (targetIndex == 2)
                    Console.BackgroundColor = state.colorTheme.selection;

                writer.SetCursorPosition(9, index++);
                writer.Write("Select Current Symbol");
                Console.BackgroundColor = state.colorTheme.background;
            }

            if (currentSymbols.Length > 0 && currentGlobals.Count > 0) {
                writer.SetCursorPosition(0, index);
                writer.WriteLine("Types:");
            }

            foreach (var symbol in currentSymbols) {
                writer.SetCursorPosition(9, index++);

                if (targetIndex == index - 3)
                    Console.BackgroundColor = state.colorTheme.selection;
                else
                    Console.BackgroundColor = state.colorTheme.background;

                SymbolDisplay.AppendToDisplayText(pageText, symbol, SymbolDisplayFormat.Everything);
                WriteDisplayText(pageText);
            }

            if (currentSymbols.Length > 0 && currentGlobals.Count > 0) {
                Console.BackgroundColor = state.colorTheme.background;
                writer.SetCursorPosition(0, index);
                writer.WriteLine("Globals:");
            }

            foreach (var global in currentGlobals) {
                writer.SetCursorPosition(9, index++);

                if (targetIndex == index - 3)
                    Console.BackgroundColor = state.colorTheme.selection;
                else
                    Console.BackgroundColor = state.colorTheme.background;

                SymbolDisplay.AppendToDisplayText(pageText, global.Key, SymbolDisplayFormat.Everything);
                WriteDisplayText(pageText);
            }

            return false;
        }

        static void DisplaySymbolName(DisplayText text, ISymbol symbol) {
            if (symbol.kind == SymbolKind.Local) {
                SymbolDisplay.AppendToDisplayText(
                    text,
                    symbol.containingSymbol,
                    SymbolDisplayFormat.QualifiedNameFormat
                );

                text.Write(CreatePunctuation(SyntaxKind.PeriodToken));
            }

            SymbolDisplay.AppendToDisplayText(text, symbol, SymbolDisplayFormat.QualifiedNameFormat);
        }
    }

    [MetaCommand("dump", "Show contents of symbol <signature>")]
    private void EvaluateDump(string signature) {
        // TODO Let this work with template overloads

        // Prefer tracked symbols first
        foreach (var symbolAndObject in state.context.GetTrackedSymbolsAndObjects()) {
            var local = symbolAndObject.Key;

            if (local.name == signature) {
                var localDisplayText = new DisplayText();
                SymbolDisplay.AppendToDisplayText(localDisplayText, local, SymbolDisplayFormat.Everything);
                localDisplayText.Write(CreatePunctuation(" = "));
                WriteDisplayText(localDisplayText);

                var localValue = EvaluatorObjectToNativeObject(symbolAndObject.Value);
                RenderResult(localValue);
                writer.WriteLine();

                return;
            }
        }

        // Then do a deeper search for non-global symbols
        var compilation = state.previous ?? EmptyCompilation;
        var allSymbols = compilation.GetSymbols(true);
        var name = signature.Contains('(') ? signature.Split('(')[0] : signature;
        ISymbol[] symbols;

        if (name.Contains('.')) {
            var failed = false;
            var parts = name.Split('.');

            for (var i = 0; i < parts.Length - 1; i++) {
                var namedTypes = allSymbols
                    .Where(s => s.name == parts[i] && s is INamedTypeSymbol)
                    .Select(s => s as INamedTypeSymbol);

                if (!namedTypes.Any()) {
                    failed = true;
                    break;
                }

                var first = namedTypes.First();
                allSymbols = first.GetMembers();
            }

            if (failed) {
                symbols = [];
            } else {
                symbols = (signature == name
                    ? allSymbols.Where(s => s.name == parts[^1])
                    : allSymbols.Where(s => s is IMethodSymbol i &&
                        i.ToString() == (parts[^1] + string.Join('(', signature.Split('(')[1..]))))
                    .ToArray();
            }
        } else {
            symbols = (signature == name
                ? allSymbols.Where(s => s.name == name)
                : allSymbols.Where(s => s.name == name).Where(f => f.ToString() == signature))
                    .ToArray();
        }

        ISymbol symbol = null;
        var displayText = new DisplayText();

        if (symbols.Length == 0) {
            if (signature == name)
                handle.diagnostics.Push(new BelteDiagnostic(Diagnostics.Error.UndefinedSymbol(name)));
            else
                handle.diagnostics.Push(new BelteDiagnostic(Diagnostics.Error.NoSuchMethod(signature)));
        } else if (symbols.Length == 1) {
            symbol = symbols.Single();
        } else if (signature == name) {
            var temp = symbols.Where(s => s is not IMethodSymbol);

            if (temp.Any()) {
                symbol = temp.First();
            } else {
                handle.diagnostics.Push(
                    new BelteDiagnostic(Diagnostics.Error.AmbiguousSignature(signature, symbols))
                );
            }
        } else {
            symbol = symbols[0];
        }

        if (symbol is not null) {
            compilation.EmitTree(symbol, displayText);
            WriteDisplayText(displayText);
            return;
        }

        if (_hasDiagnosticHandle)
            _diagnosticHandle(handle, "repl", state.colorTheme.textDefault);
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
        if (!int.TryParse(count, out var countInt)) {
            handle.diagnostics.Push(new BelteDiagnostic(Diagnostics.Error.InvalidArgument(count, typeof(int))));

            if (_hasDiagnosticHandle)
                CallDiagnosticHandle(handle, "repl", state.colorTheme.textDefault);
            else
                handle.diagnostics.Clear();

            return;
        }

        if (File.Exists(path)) {
            Console.ForegroundColor = state.colorTheme.textDefault;
            writer.Write("File already exists, continue? [y/n] ");
            var response = Console.ReadKey().KeyChar;
            writer.WriteLine();

            if (response != 'y') {
                writer.WriteLine("Aborting");
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

        for (var i = 0; i < 3; i++) {
            try {
                File.WriteAllLines(path, subset);
                wrote = true;
                break;
            } catch (IOException) {
                // In case file is being used by another process, retry
                Thread.Sleep(100);
            }
        }

        Console.ForegroundColor = state.colorTheme.textDefault;
        var linesWord = split.Length == 1 ? "line" : "lines";

        if (wrote)
            writer.WriteLine($"Wrote {split.Length} {linesWord}");
        else
            writer.WriteLine($"Failed to write to file");
    }

    [MetaCommand("settings", "Open settings page")]
    private void EvaluateSettings() {
        state.currentPage = Page.Settings;

        var maxNameLength = 0;

        foreach (var (name, _, _) in InUse)
            maxNameLength = name.Length > maxNameLength ? name.Length : maxNameLength;

        var targetIndex = 2;
        var index = 2;

        foreach (var (_, _, theme) in InUse) {
            if (state.colorTheme.GetType() == theme.GetType())
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
                if (targetIndex - 2 < InUse.Length - 1)
                    targetIndex++;
            }
        }

        Console.BackgroundColor = state.colorTheme.background;
        ReviveDocument();
        state.currentPage = Page.Repl;

        void UpdatePage(int targetIndex) {
            targetIndex -= 2;
            state.colorTheme = InUse[targetIndex].theme;

            Console.BackgroundColor = state.colorTheme.background;
            Console.ForegroundColor = state.colorTheme.textDefault;
            Console.Clear();
            writer.WriteLine("Settings");
            writer.WriteLine();
            writer.Write("Theme: ");

            var index = 2;

            foreach (var (name, contributor, theme) in InUse) {
                writer.SetCursorPosition(7, index++);

                if (state.colorTheme.GetType() == theme.GetType())
                    Console.BackgroundColor = state.colorTheme.selection;
                else
                    Console.BackgroundColor = state.colorTheme.background;

                writer.Write(name.PadRight(maxNameLength + 3)); // Arbitrary padding

                if (contributor.Length > 0) {
                    Console.BackgroundColor = state.colorTheme.background;
                    writer.Write($"  Created by contributor {contributor}");
                }
            }

            writer.SetCursorPosition(7, targetIndex + 2);
        }
    }

    [MetaCommand("showWarnings", "Toggle display of warnings")]
    private void EvaluateShowWarnings() {
        state.showWarnings = !state.showWarnings;
        writer.WriteLine(state.showWarnings ? "Warnings shown" : "Warnings ignored");
    }

    [MetaCommand("showTime", "Toggle display of submission execution time")]
    private void EvaluateShowTime() {
        _showTime = !_showTime;
        writer.WriteLine(_showTime ? "Execution time visible" : "Execution time hidden");
    }

    [MetaCommand("showIL", "Toggle display of IL code")]
    private void EvaluateShowIL() {
        state.showIL = !state.showIL;
        writer.WriteLine(state.showIL ? "IL visible" : "IL hidden");
    }

    [MetaCommand("showCS", "Toggle display of C# code")]
    private void EvaluateShowCS() {
        state.showCS = !state.showCS;
        writer.WriteLine(state.showCS ? "C# visible" : "C# hidden");
    }
}

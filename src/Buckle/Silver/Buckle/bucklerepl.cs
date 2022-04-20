using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.CodeAnalysis.Symbols;
using Buckle.IO;
using CommandLine;

namespace Buckle {
    public sealed class BuckleRepl : Repl {
        private static readonly Compilation emptyCompilation = Compilation.CreateScript(null);
        internal override object state_ { get; set; }
        internal BuckleReplState state { get { return (BuckleReplState)state_; } set { state_=value; } }

        internal sealed class BuckleReplState {
            public bool showTree = false;
            public bool showProgram = false;
            public bool loadingSubmissions = false;
            public Compilation previous;
            public Dictionary<VariableSymbol, object> variables;
        }

        private sealed class RenderState {
            public SourceText text { get; }
            public ImmutableArray<Token> tokens { get; }

            public RenderState(SourceText text_, ImmutableArray<Token> tokens_) {
                text = text_;
                tokens = tokens_;
            }
        }

        public BuckleRepl(Compiler handle, ErrorHandle errorHandle) : base(handle, errorHandle) {
            state = new BuckleReplState();
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

            if (state.showTree) syntaxTree.root.WriteTo(Console.Out);
            if (state.showProgram) compilation.EmitTree(Console.Out);

            var result = compilation.Evaluate(state.variables);

            handle.diagnostics.Move(result.diagnostics);
            if (handle.diagnostics.Any()) {
                if (errorHandle != null) {
                    handle.diagnostics = DiagnosticQueue.CleanDiagnostics(handle.diagnostics);
                    errorHandle(handle);
                } else {
                    handle.diagnostics.Clear();
                }
            } else {
                if (result.value != null && !state.loadingSubmissions) {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(result.value);
                    Console.ResetColor();
                }

                state.previous = compilation;
                SaveSubmission(text);
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

        protected override object RenderLine(IReadOnlyList<string> lines, int lineIndex, object state) {
            RenderState renderState;

            if (state == null) {
                var text = string.Join(Environment.NewLine, lines);
                var sourceText = SourceText.From(text);
                var tokens = SyntaxTree.ParseTokens(sourceText);
                renderState = new RenderState(sourceText, tokens);
            } else {
                renderState = (RenderState)state;
            }

            var lineSpan = renderState.text.lines[lineIndex].span;

            for (int i=0; i<renderState.tokens.Length; i++) {
                var tokens = renderState.tokens;
                var token = tokens[i];

                if (!lineSpan.OverlapsWith(token.span))
                    continue;

                var tokenStart = Math.Max(token.span.start, lineSpan.start);
                var tokenEnd = Math.Min(token.span.end, lineSpan.end);
                var tokenSpan = TextSpan.FromBounds(tokenStart, tokenEnd);
                var tokenText = renderState.text.ToString(tokenSpan);

                var isKeyword = token.type.IsKeyword();
                var isNumber = token.type == SyntaxType.NUMBERIC_LITERAL_TOKEN;
                var isIdentifier = token.type == SyntaxType.IDENTIFIER_TOKEN;
                var isString = token.type == SyntaxType.STRING_LITERAL_TOKEN;
                var isType = (i < tokens.Length-2) && (tokens[i+1].type == SyntaxType.WHITESPACE_TRIVIA) &&
                    (tokens[i+2].type == SyntaxType.IDENTIFIER_TOKEN) && isIdentifier;
                isType |= i == 1 && tokens[0].text == "#";
                isType &= !(i > 1 && tokens[0].text == "#");
                var isComment = token.type.IsComment();

                if (isComment)
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                else if (isKeyword)
                    Console.ForegroundColor = ConsoleColor.Blue;
                else if (isNumber)
                    Console.ForegroundColor = ConsoleColor.Cyan;
                else if (isString)
                    Console.ForegroundColor = ConsoleColor.Yellow;
                else if (isType)
                    Console.ForegroundColor = ConsoleColor.Blue;
                else if (!isIdentifier)
                    Console.ForegroundColor = ConsoleColor.DarkGray;

                Console.Write(tokenText);
                Console.ResetColor();
            }

            return state;
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

                if (errorHandle != null)
                    errorHandle(handle, "repl");
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

                if (errorHandle != null)
                    errorHandle(handle, "repl");
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
            if (string.IsNullOrEmpty(text)) return true;

            var twoBlankTines = text.Split(Environment.NewLine).Reverse()
                .TakeWhile(s => string.IsNullOrEmpty(s))
                .Take(2)
                .Count() == 2;

            if (twoBlankTines) return true;

            var syntaxTree = SyntaxTree.Parse(text);
            var lastMember = syntaxTree.root.members.LastOrDefault();
            if (lastMember == null || lastMember.GetLastToken().isMissing) return false;

            return true;
        }
    }
}

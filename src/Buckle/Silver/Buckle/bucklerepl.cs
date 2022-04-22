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

        protected override object RenderLine(IReadOnlyList<string> lines, int lineIndex, object rState) {
            SyntaxTree syntaxTree;

            if (rState == null) {
                var text = string.Join(Environment.NewLine, lines);
                syntaxTree = SyntaxTree.Parse(text);
            } else {
                syntaxTree = (SyntaxTree)rState;
            }

            var lineSpan = syntaxTree.text.lines[lineIndex].span;
            var classifiedSpans = Classifier.Classify(syntaxTree, lineSpan);

            foreach (var classifiedSpan in classifiedSpans) {
                var classifiedText = syntaxTree.text.ToString(classifiedSpan.span);

                // TODO
                // var isType = (i < tokens.Length-2) && (tokens[i+1].type == SyntaxType.WHITESPACE_TRIVIA) &&
                //     (tokens[i+2].type == SyntaxType.IDENTIFIER_TOKEN) && isIdentifier;
                // isType |= i == 1 && tokens[0].text == "#";
                // isType &= !(i > 1 && tokens[0].text == "#");

                switch (classifiedSpan.classification) {
                    case Classification.Identifier:
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                    case Classification.Number:
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        break;
                    case Classification.String:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    case Classification.Comment:
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        break;
                    case Classification.Keyword:
                        Console.ForegroundColor = ConsoleColor.Blue;
                        break;
                    case Classification.Type:
                        Console.ForegroundColor = ConsoleColor.Blue;
                        break;
                    case Classification.Text:
                    default:
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        break;
                }

                Console.Write(classifiedText);
                Console.ResetColor();
            }

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

    enum Classification {
        Identifier,
        Keyword,
        Type,
        Number,
        String,
        Comment,
        Text,
    }

    class ClassifiedSpan {
        public TextSpan span { get; }
        public Classification classification { get; }

        public ClassifiedSpan(TextSpan span_, Classification classification_) {
            span = span_;
            classification = classification_;
        }
    }

    class Classifier {
        public static ImmutableArray<ClassifiedSpan> Classify(SyntaxTree syntaxTree, TextSpan span) {
            var result = ImmutableArray.CreateBuilder<ClassifiedSpan>();
            ClassifyNode(syntaxTree.root, span, result);
            return result.ToImmutable();
        }

        private static void ClassifyNode(Node node, TextSpan span, ImmutableArray<ClassifiedSpan>.Builder result) {
            if (!node.fullSpan.OverlapsWith(span))
                return;

            if (node is Token token) {
                ClassifyToken(token, span, result);
            }

            foreach (var child in node.GetChildren())
                ClassifyNode(child, span, result);
        }

        private static void ClassifyToken(Token token, TextSpan span, ImmutableArray<ClassifiedSpan>.Builder result) {
            foreach (var trivia in token.leadingTrivia)
                ClassifyTrivia(trivia, span, result);

            AddClassification(token.type, token.span, span, result);

            foreach (var trivia in token.trailingTrivia)
                ClassifyTrivia(trivia, span, result);
        }

        private static void ClassifyTrivia(
            SyntaxTrivia trivia, TextSpan span, ImmutableArray<ClassifiedSpan>.Builder result) {
            AddClassification(trivia.type, trivia.span, span, result);
        }

        private static void AddClassification(SyntaxType elementType, TextSpan elementSpan,
            TextSpan span, ImmutableArray<ClassifiedSpan>.Builder result) {
            if (!elementSpan.OverlapsWith(span))
                return;

            var classification = GetClassification(elementType);
            var adjustedStart = Math.Max(elementSpan.start, span.start);
            var adjustedEnd = Math.Min(elementSpan.end, span.end);
            var adjustedSpan = TextSpan.FromBounds(adjustedStart, adjustedEnd);

            var classifiedSpan = new ClassifiedSpan(adjustedSpan, classification);
            result.Add(classifiedSpan);
        }

        private static Classification GetClassification(SyntaxType type) {
            var isKeyword = type.IsKeyword();
            var isNumber = type == SyntaxType.NUMBERIC_LITERAL_TOKEN;
            var isIdentifier = type == SyntaxType.IDENTIFIER_TOKEN;
            var isString = type == SyntaxType.STRING_LITERAL_TOKEN;
            var isComment = type.IsComment();

            if (isKeyword)
                return Classification.Keyword;
            else if (isIdentifier)
                return Classification.Identifier;
            else if (isNumber)
                return Classification.Number;
            else if (isString)
                return Classification.String;
            else if (isComment)
                return Classification.Comment;
            else
                return Classification.Text;
        }
    }
}

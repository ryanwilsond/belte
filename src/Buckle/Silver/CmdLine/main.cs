using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Buckle;
using Buckle.CodeAnalysis.Text;
using System.Linq;

namespace CommandLine {

    public static class CmdLine {

        const int SUCCESS_EXIT_CODE = 0;
        const int ERROR_EXIT_CODE = 1;
        const int FATAL_EXIT_CODE = 2;

        private static CompilerState DecodeOptions(string[] args, out DiagnosticQueue diagnostics) {
            diagnostics = new DiagnosticQueue();
            CompilerState state = new CompilerState();
            List<FileState> tasks = new List<FileState>();

            bool specifyStage = false;
            bool specifyOut = false;
            state.buildMode = BuildMode.Independent;
            state.finishStage = CompilerStage.Linked;
            state.linkOutputFilename = "a.exe";

            for (int i = 0; i < args.Length; i++) {
                string arg = args[i];

                if (arg.StartsWith('-')) {
                    switch (arg) {
                        case "-E":
                            specifyStage = true;
                            state.finishStage = CompilerStage.Preprocessed;
                            break;
                        case "-S":
                            specifyStage = true;
                            state.finishStage = CompilerStage.Compiled;
                            break;
                        case "-c":
                            specifyStage = true;
                            state.finishStage = CompilerStage.Assembled;
                            break;
                        case "-r":
                            state.buildMode = BuildMode.Repl;
                            if (args.Length != 1)
                                diagnostics.Push(DiagnosticType.Fatal, "cannot use any other arguments with '-r'");

                            break;
                        case "-i":
                            state.buildMode = BuildMode.Interpreter;
                            break;
                        case "-o":
                            specifyOut = true;
                            if (i >= args.Length - 1)
                                diagnostics.Push(DiagnosticType.Fatal, "missing filename after '-o'");
                            state.linkOutputFilename = args[i++];
                            break;
                        default:
                            diagnostics.Push(DiagnosticType.Fatal, $"unknown argument '{arg}'");
                            break;
                    }
                } else {
                    string filename = arg;
                    string[] parts = filename.Split('.');
                    string type = parts[parts.Length - 1];
                    FileState task = new FileState();
                    task.inputFilename = filename;

                    if (!File.Exists(task.inputFilename)) {
                        diagnostics.Push(DiagnosticType.Error, $"{filename}: no such file or directory");
                        continue;
                    }

                    switch (type) {
                        case "ble":
                            task.stage = CompilerStage.Raw;
                            break;
                        case "pble":
                            task.stage = CompilerStage.Preprocessed;
                            break;
                        case "s":
                        case "asm":
                            task.stage = CompilerStage.Compiled;
                            break;
                        case "o":
                        case "obj":
                            task.stage = CompilerStage.Assembled;
                            break;
                        default:
                            diagnostics.Push(DiagnosticType.Warning,
                                $"unknown file type of input file '{filename}'; ignoring");
                            break;
                    }

                    tasks.Add(task);
                }
            }

            state.tasks = tasks.ToArray();

            if (specifyOut && specifyStage && state.tasks.Length > 1)
                diagnostics.Push(
                    DiagnosticType.Fatal, "cannot specify output file with '-E', '-S', or '-c' with multiple files");

            if (state.tasks.Length == 0 && !(state.buildMode == BuildMode.Repl))
                diagnostics.Push(DiagnosticType.Fatal, "no input files");

            if (specifyStage && state.buildMode == BuildMode.Interpreter)
                diagnostics.Push(
                    DiagnosticType.Fatal, "cannot specify output file with '-E', '-S', or '-c' with interpreter");

            return state;
        }

        private static void PrettyPrintDiagnostic(SourceText text, Diagnostic diagnostic) {
            var span = diagnostic.location.span;
            int lineNumber = text.GetLineIndex(span.start);
            TextLine line = text.lines[lineNumber];
            int column = span.start - line.start + 1;
            string lineText = line.ToString();
            string filename = diagnostic.location.fileName;
            if (!string.IsNullOrEmpty(filename))
                Console.Write($"{filename}:");

            Console.Write($"{lineNumber + 1}:{column}:");

            if (diagnostic.type == DiagnosticType.Error) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(" error: ");
            } else if (diagnostic.type == DiagnosticType.Fatal) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(" fatal error: ");
            } else if (diagnostic.type == DiagnosticType.Warning) {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write(" warning: ");
            }

            Console.ResetColor();
            Console.WriteLine(diagnostic.msg);

            if (text.IsAtEndOfInput(span)) return;

            TextSpan prefixSpan = TextSpan.FromBounds(line.start, span.start);
            TextSpan suffixSpan = TextSpan.FromBounds(span.end, line.end);

            string prefix = text.ToString(prefixSpan);
            string focus = text.ToString(span);
            string suffix = text.ToString(suffixSpan);

            Console.Write($" {prefix}");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write(focus);
            Console.ResetColor();
            Console.WriteLine(suffix);

            Console.ForegroundColor = ConsoleColor.Red;
            string markerPrefix = " " + Regex.Replace(prefix, @"\S", " ");
            string marker = "^";
            if (span.length > 0 && span.start != lineText.Length)
                marker += new string('~', span.length - 1);

            Console.WriteLine(markerPrefix + marker);

            if (diagnostic.suggestion != null) {
                Console.ForegroundColor = ConsoleColor.Green;
                string suggestion = diagnostic.suggestion.Replace("%", focus);
                Console.WriteLine(markerPrefix + suggestion);
            }

            Console.ResetColor();
        }

        private static int ResolveDiagnostics(Compiler compiler, string me = null) {
            if (compiler.diagnostics.count == 0) return SUCCESS_EXIT_CODE;
            DiagnosticType worst = DiagnosticType.Unknown;
            me = me ?? compiler.me;

            Diagnostic diagnostic = compiler.diagnostics.Pop();
            while (diagnostic != null) {
                if (diagnostic.type == DiagnosticType.Unknown) {
                } else if (diagnostic.location == null) {
                    Console.Write($"{me}: ");

                    if (diagnostic.type == DiagnosticType.Warning) {
                        if (worst == DiagnosticType.Unknown)
                            worst = DiagnosticType.Warning;

                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.Write("warning: ");
                    } else if (diagnostic.type == DiagnosticType.Error) {
                        if (worst != DiagnosticType.Fatal)
                            worst = DiagnosticType.Error;

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("error: ");
                    } else if (diagnostic.type == DiagnosticType.Fatal) {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("fatal error: ");
                        worst = DiagnosticType.Fatal;
                    }

                    Console.ResetColor();
                    Console.WriteLine(diagnostic.msg);

                } else {
                    PrettyPrintDiagnostic(compiler.state.sourceText, diagnostic);
                }

                diagnostic = compiler.diagnostics.Pop();
            }

            switch (worst) {
                case DiagnosticType.Error: return ERROR_EXIT_CODE;
                case DiagnosticType.Fatal: return FATAL_EXIT_CODE;
                case DiagnosticType.Unknown:
                case DiagnosticType.Warning:
                default: return SUCCESS_EXIT_CODE;
            }
        }

        private static void ProduceOutputFiles(Compiler compiler) {
            if (compiler.state.finishStage == CompilerStage.Linked) return;

            foreach (FileState file in compiler.state.tasks) {
                string inter = file.inputFilename.Split('.')[0];

                switch (compiler.state.finishStage) {
                    case CompilerStage.Preprocessed:
                        inter += ".pble";
                        break;
                    case CompilerStage.Compiled:
                        inter += ".s";
                        break;
                    case CompilerStage.Assembled:
                        inter += ".o";
                        break;
                    default: break;
                }
            }
        }

        private static void CleanOutputFiles(Compiler compiler) {
            if (compiler.state.finishStage == CompilerStage.Linked) {
                File.Delete(compiler.state.linkOutputFilename);
                return;
            }

            foreach (FileState file in compiler.state.tasks) {
                File.Delete(file.outputFilename);
            }
        }

        private static void ResolveOutputFiles(Compiler compiler) {
            ProduceOutputFiles(compiler);
            CleanOutputFiles(compiler);
        }

        private static void ResolveCompilerOutput(Compiler compiler) {
            if (compiler.state.buildMode == BuildMode.Interpreter)
                return;

            if (compiler.state.finishStage == CompilerStage.Linked) {
                if (compiler.state.linkOutputContent != null)
                    File.WriteAllBytes(compiler.state.linkOutputFilename,
                        compiler.state.linkOutputContent.ToArray());
                return;
            }

            foreach (FileState file in compiler.state.tasks) {
                if (file.stage == compiler.state.finishStage) {
                    if (file.stage == CompilerStage.Assembled)
                        File.WriteAllBytes(file.outputFilename, file.fileContent.bytes.ToArray());
                    else
                        File.WriteAllLines(file.outputFilename, file.fileContent.lines.ToArray());
                }
            }
        }

        private static void ReadInputFiles(Compiler compiler) {
            for (int i=0; i<compiler.state.tasks.Length; i++) {
                ref FileState task = ref compiler.state.tasks[i];

                switch (task.stage) {
                    case CompilerStage.Raw:
                    case CompilerStage.Preprocessed:
                    case CompilerStage.Compiled:
                        task.fileContent.lines = File.ReadAllLines(task.inputFilename).ToList();
                        break;
                    case CompilerStage.Assembled:
                        task.fileContent.bytes = File.ReadAllBytes(task.inputFilename).ToList();
                        break;
                    case CompilerStage.Linked:
                        compiler.diagnostics.Push(
                            DiagnosticType.Warning, $"{task.inputFilename}: file already compiled; ignoring");
                        break;
                }
            }
        }

        public static int Main(string[] args) {
            int err = SUCCESS_EXIT_CODE;
            Compiler compiler = new Compiler();
            compiler.me = Process.GetCurrentProcess().ProcessName;

            compiler.state = DecodeOptions(args, out DiagnosticQueue diagnostics);
            ResolveOutputFiles(compiler);
            ReadInputFiles(compiler);
            compiler.diagnostics.Move(diagnostics);
            err = ResolveDiagnostics(compiler);
            if (err > 0) return err;

            // only mode that doesn't go through one-time compilation
            if (compiler.state.buildMode == BuildMode.Repl) {
                var repl = new BuckleRepl(compiler, ResolveDiagnostics);
                repl.Run();

                return SUCCESS_EXIT_CODE;
            }

            compiler.Compile();
            err = ResolveDiagnostics(compiler);
            if (err > 0) return err;

            ResolveCompilerOutput(compiler);
            return SUCCESS_EXIT_CODE;
        }
    }
}

using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using Buckle;

namespace CommandLine {

    public static class CmdLine {

        const int SUCCESS_EXIT_CODE = 0;
        const int ERROR_EXIT_CODE = 1;
        const int FATAL_EXIT_CODE = 2;

        private static CompilerState DecodeOptions(string[] args, out DiagnosticQueue diagnostics) {
            diagnostics = new DiagnosticQueue();
            CompilerState state = new CompilerState();
            List<FileState> tasks = new List<FileState>();

            bool specify_stage = false;
            bool specify_out = false;
            state.finish_stage = CompilerStage.linked;
            state.link_output_filename = "a.exe";

            for (int i=0; i<args.Length; i++) {
                string arg = args[i];

                if (arg.StartsWith('-')) {
                    switch (arg) {
                        case "-E":
                            specify_stage = true;
                            state.finish_stage = CompilerStage.preprocessed;
                            break;
                        case "-S":
                            specify_stage = true;
                            state.finish_stage = CompilerStage.compiled;
                            break;
                        case "-c":
                            specify_stage = true;
                            state.finish_stage = CompilerStage.assembled;
                            break;
                        case "-r":
                            return state;
                        case "-o":
                            specify_out = true;
                            if (i >= args.Length-1)
                                diagnostics.Push(DiagnosticType.fatal, "missing filename after '-o'");
                            state.link_output_filename = args[i++];
                            break;
                        default:
                            diagnostics.Push(DiagnosticType.fatal, $"unknown argument '{arg}'");
                            break;
                    }
                } else {
                    string filename = arg;
                    string[] parts = filename.Split('.');
                    string type = parts[parts.Length-1];
                    FileState task = new FileState();
                    // check if exists
                    task.in_filename = filename;

                    switch (type) {
                        case "ble":
                            task.stage = CompilerStage.raw;
                            break;
                        case "pble":
                            task.stage = CompilerStage.preprocessed;
                            break;
                        case "s":
                        case "asm":
                            task.stage = CompilerStage.compiled;
                            break;
                        case "o":
                        case "obj":
                            task.stage = CompilerStage.assembled;
                            break;
                        default:
                            diagnostics.Push(DiagnosticType.warning,
                                $"unknown file type of input file '{filename}'; ignoring");
                            break;
                    }

                    tasks.Add(task);
                }
            }

            state.tasks = tasks.ToArray();

            if (specify_out && specify_stage && state.tasks.Length > 1)
                diagnostics.Push(DiagnosticType.fatal,
                    "cannot specify output file with '-E', '-S', or '-c' with multiple files");
            if (state.tasks.Length == 0)
                diagnostics.Push(DiagnosticType.fatal, "no input files");

            return state;
        }

        private static void PrettyPrintDiagnostic(string line, Diagnostic error) {
            if (error.span.file != null) Console.Write($"{error.span.file}:");
            if (error.span.line != null) Console.Write($"{error.span.line.Value}:");
            if (error.span.start != null) Console.Write($"{error.span.start.Value}:");

            if (error.type == DiagnosticType.error) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(" error: ");
            } else if (error.type == DiagnosticType.fatal) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(" fatal error: ");
            } else if (error.type == DiagnosticType.warning) {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write(" warning: ");
            }

            Console.ResetColor();
            Console.WriteLine(error.msg);

            string prefix, focus, suffix;

            if (error.span.start.Value == line.Length) {
                prefix = line;
                focus = " ";
                suffix = "";
            } else {
                prefix = line.Substring(0, error.span.start.Value);
                focus = line.Substring(error.span.start.Value, error.span.length.Value);
                suffix = line.Substring(error.span.end.Value);
            }

            Console.Write($" {prefix}");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write(focus);
            Console.ResetColor();
            Console.WriteLine(suffix);

            Console.ForegroundColor = ConsoleColor.Red;
            int tabcount = prefix.Length - prefix.Replace("\t", "").Length;
            string marker = new string(' ', error.span.start.Value + 1);
            marker += new string('\t', tabcount);
            marker += "^";
            marker += new string('~', error.span.length.Value - 1);
            Console.WriteLine(marker);

            Console.ResetColor();
        }

        private static int ResolveDiagnostics(Compiler compiler, string line = null) {
            if (compiler.diagnostics.count == 0) return SUCCESS_EXIT_CODE;
            DiagnosticType worst = DiagnosticType.unknown;

            Diagnostic diagnostic = compiler.diagnostics.Pop();
            while (diagnostic != null) {
                if (diagnostic.type == DiagnosticType.unknown) {
                } else if (diagnostic.span == null) {
                    Console.Write($"{compiler.me}: ");

                    if (diagnostic.type == DiagnosticType.warning) {
                        if (worst == DiagnosticType.unknown)
                            worst = DiagnosticType.warning;

                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.Write("warning: ");
                    } else if (diagnostic.type == DiagnosticType.error) {
                        if (worst != DiagnosticType.fatal)
                            worst = DiagnosticType.error;

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("error: ");
                    } else if (diagnostic.type == DiagnosticType.fatal) {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("fatal error: ");
                        worst = DiagnosticType.fatal;
                    }

                    Console.ResetColor();
                    if (diagnostic.type != DiagnosticType.error)
                        Console.WriteLine($"{diagnostic.msg}");

                } else {
                    PrettyPrintDiagnostic(line, diagnostic);
                }

                diagnostic = compiler.diagnostics.Pop();
            }

            switch (worst) {
                case DiagnosticType.error: return ERROR_EXIT_CODE;
                case DiagnosticType.fatal: return FATAL_EXIT_CODE;
                case DiagnosticType.unknown:
                case DiagnosticType.warning:
                default: return SUCCESS_EXIT_CODE;
            }
        }

        private static void ProduceOutputFiles(Compiler compiler) {
            if (compiler.state.finish_stage == CompilerStage.linked) return;

            foreach (FileState file in compiler.state.tasks) {
                string inter = file.in_filename.Split('.')[0];

                switch (compiler.state.finish_stage) {
                    case CompilerStage.preprocessed:
                        inter += ".pble";
                        break;
                    case CompilerStage.compiled:
                        inter += ".s";
                        break;
                    case CompilerStage.assembled:
                        inter += ".o";
                        break;
                    default: break;
                }
            }
        }

        private static void CleanOutputFiles(Compiler compiler) {
            if (compiler.state.finish_stage == CompilerStage.linked) {
                File.Delete(compiler.state.link_output_filename);
                return;
            }

            foreach (FileState file in compiler.state.tasks) {
                File.Delete(file.out_filename);
            }
        }

        private static void ResolveOutputFiles(Compiler compiler) {
            ProduceOutputFiles(compiler);
            CleanOutputFiles(compiler);
        }

        private static void ResolveCompilerOutput(Compiler compiler) {
            if (compiler.state.finish_stage == CompilerStage.linked) {
                if (compiler.state.link_output_content != null)
                    File.WriteAllBytes(compiler.state.link_output_filename,
                        compiler.state.link_output_content.ToArray());
                return;
            }

            foreach (FileState file in compiler.state.tasks) {
                if (file.stage == compiler.state.finish_stage) {
                    if (file.stage == CompilerStage.assembled)
                        File.WriteAllBytes(file.out_filename, file.file_content.bytes.ToArray());
                    else
                        File.WriteAllLines(file.out_filename, file.file_content.lines.ToArray());
                }
            }
        }

        public static int Main(string[] args) {
            int err = SUCCESS_EXIT_CODE;
            Compiler compiler = new Compiler();
            compiler.me = Process.GetCurrentProcess().ProcessName;

            compiler.state = DecodeOptions(args, out DiagnosticQueue diagnostics);
            ResolveOutputFiles(compiler);
            compiler.diagnostics.Move(diagnostics);
            err = ResolveDiagnostics(compiler);
            if (err > 0) return err;

            compiler.Compile(ResolveDiagnostics); // temp callback
            err = ResolveDiagnostics(compiler);
            if (err > 0) return err;

            ResolveCompilerOutput(compiler);
            return SUCCESS_EXIT_CODE;
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Belte.Repl;
using Buckle;
using Buckle.Diagnostics;
using Buckle.CodeAnalysis.Text;

namespace Belte.CommandLine;

public static partial class BuckleCommandLine {
    const int SuccessExitCode = 0;
    const int ErrorExitCode = 1;
    const int FatalExitCode = 2;

    static readonly string[] AllowedOptions = {
        // "error", "ignore", "all"
    };

    private static void ShowErrorHelp(int error) {
        // TODO
    }

    private static void ShowHelpDialog() {
        string helpMessage = @"Usage: buckle.exe [options] file...
Options:
-h|--help             Display this information.
-p                    Preprocess only, otherwise compiler preprocesses.
-s                    Compile only; do not assemble or link.
-c                    Compile and assemble; do not link.
-r                    Invoke the Repl.
-i                    Interpret only.
-d                    Compile with .NET integration (cannot stop at assembly or linking).
-o <file>             Specify output file.
-W<options>           Forward options to various sub-processes.
--entry=<symbol>      Specify the entry point of the program.
--modulename=<name>   Specify the module name (used with .NET integration only).
--ref=<file>          Specify a reference (used with .NET integration only).
--dumpmachine         Display the compiler's target system.
--version             Dispaly compiler version information.";

        Console.WriteLine(helpMessage);
    }

    private static void ShowMachineDialog() {
        string machineMessage = "Machine: x86_64-w64";
        Console.WriteLine(machineMessage);
    }

    private static void ShowVersionDialog() {
        string versionMessage = "Version: Buckle 0.1";
        Console.WriteLine(versionMessage);
    }

    private static void PrettyPrintDiagnostic(Diagnostic diagnostic) {
        TextSpan span = diagnostic.location.span;
        SourceText text = diagnostic.location.text;

        int lineNumber = text.GetLineIndex(span.start);
        TextLine line = text.lines[lineNumber];
        int column = span.start - line.start + 1;
        string lineText = line.ToString();

        string filename = diagnostic.location.fileName;
        if (!string.IsNullOrEmpty(filename))
            Console.Write($"{filename}:");

        Console.Write($"{lineNumber + 1}:{column}:");

        ConsoleColor highlightColor = ConsoleColor.White;

        if (diagnostic.info.severity == DiagnosticType.Error) {
            highlightColor = ConsoleColor.Red;
            Console.ForegroundColor = highlightColor;
            Console.Write(" error");
        } else if (diagnostic.info.severity == DiagnosticType.Fatal) {
            highlightColor = ConsoleColor.Red;
            Console.ForegroundColor = highlightColor;
            Console.Write(" fatal error");
        } else if (diagnostic.info.severity == DiagnosticType.Warning) {
            highlightColor = ConsoleColor.Magenta;
            Console.ForegroundColor = highlightColor;
            Console.Write(" warning");
        }

        if (diagnostic.info.code != null && diagnostic.info.code > 0) {
            var number = diagnostic.info.code.ToString();
            Console.Write($" BU{number.PadLeft(4, '0')}: ");
        } else {
            Console.Write(": ");
        }

        Console.ResetColor();
        Console.WriteLine(diagnostic.message);

        if (text.IsAtEndOfInput(span))
            return;

        TextSpan prefixSpan = TextSpan.FromBounds(line.start, span.start);
        TextSpan suffixSpan = TextSpan.FromBounds(span.end, line.end);

        string prefix = text.ToString(prefixSpan);
        string focus = text.ToString(span);
        string suffix = text.ToString(suffixSpan);

        Console.Write($" {prefix}");
        Console.ForegroundColor = highlightColor;
        Console.Write(focus);
        Console.ResetColor();
        Console.WriteLine(suffix);

        Console.ForegroundColor = highlightColor;
        string markerPrefix = " " + Regex.Replace(prefix, @"\S", " ");
        string marker = "^";
        if (span.length > 0 && column != lineText.Length)
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
        if (compiler.diagnostics.count == 0)
            return SuccessExitCode;

        DiagnosticType worst = DiagnosticType.Unknown;
        me = me ?? compiler.me;

        Diagnostic diagnostic = compiler.diagnostics.Pop();
        while (diagnostic != null) {
            if (diagnostic.info.severity == DiagnosticType.Unknown) {
            } else if (diagnostic.location == null) {
                Console.Write($"{me}: ");

                if (diagnostic.info.severity == DiagnosticType.Warning) {
                    if (worst == DiagnosticType.Unknown)
                        worst = DiagnosticType.Warning;

                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write("warning: ");
                } else if (diagnostic.info.severity == DiagnosticType.Error) {
                    if (worst != DiagnosticType.Fatal)
                        worst = DiagnosticType.Error;

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("error: ");
                } else if (diagnostic.info.severity == DiagnosticType.Fatal) {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("fatal error: ");
                    worst = DiagnosticType.Fatal;
                }

                Console.ResetColor();
                Console.WriteLine(diagnostic.message);

            } else {
                PrettyPrintDiagnostic(diagnostic);
            }

            diagnostic = compiler.diagnostics.Pop();
        }

        switch (worst) {
            case DiagnosticType.Error:
                return ErrorExitCode;
            case DiagnosticType.Fatal:
                return FatalExitCode;
            case DiagnosticType.Unknown:
            case DiagnosticType.Warning:
            default:
                return SuccessExitCode;
        }
    }

    private static void ProduceOutputFiles(Compiler compiler) {
        if (compiler.state.finishStage == CompilerStage.Linked)
            return;

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
                default:
                    break;
            }
        }
    }

    private static void CleanOutputFiles(Compiler compiler) {
        if (compiler.state.finishStage == CompilerStage.Linked) {
            if (File.Exists(compiler.state.outputFilename))
                File.Delete(compiler.state.outputFilename);
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
        if (compiler.state.buildMode == BuildMode.Interpreter ||
            compiler.state.buildMode == BuildMode.Dotnet)
            return;

        if (compiler.state.finishStage == CompilerStage.Linked) {
            if (compiler.state.linkOutputContent != null)
                File.WriteAllBytes(compiler.state.outputFilename, compiler.state.linkOutputContent.ToArray());

            return;
        }

        foreach (FileState file in compiler.state.tasks) {
            if (file.stage == compiler.state.finishStage) {
                if (file.stage == CompilerStage.Assembled)
                    File.WriteAllBytes(file.outputFilename, file.fileContent.bytes.ToArray());
                else
                    File.WriteAllText(file.outputFilename, file.fileContent.text);
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
                    task.fileContent.text = File.ReadAllText(task.inputFilename);
                    break;
                case CompilerStage.Assembled:
                    task.fileContent.bytes = File.ReadAllBytes(task.inputFilename).ToList();
                    break;
                case CompilerStage.Linked:
                    compiler.diagnostics.Push(
                        DiagnosticType.Warning, $"{task.inputFilename}: file already compiled; ignoring");
                    break;
                default:
                    break;
            }
        }
    }

    public static int ProcessArgs(string[] args) {
        int err;
        Compiler compiler = new Compiler();
        compiler.me = Process.GetCurrentProcess().ProcessName;

        compiler.state = DecodeOptions(
            args, out DiagnosticQueue diagnostics, out ShowDialogs dialogs);

        ResolveOutputFiles(compiler);
        ReadInputFiles(compiler);
        compiler.diagnostics.Move(diagnostics);

        if (dialogs.help || dialogs.version || dialogs.machine || dialogs.error.HasValue)
            compiler.diagnostics.Clear(DiagnosticType.Fatal);

        err = ResolveDiagnostics(compiler);

        if (dialogs.machine)
            ShowMachineDialog();
        if (dialogs.version)
            ShowVersionDialog();
        if (dialogs.help)
            ShowHelpDialog();
        if (dialogs.error.HasValue)
            ShowErrorHelp(dialogs.error.Value);

        if (dialogs.machine || dialogs.version || dialogs.help || dialogs.error.HasValue)
            return SuccessExitCode;
        if (err > 0)
            return err;

        // only mode that doesn't go through one-time compilation
        if (compiler.state.buildMode == BuildMode.Repl) {
            BelteRepl repl = new BelteRepl(compiler, ResolveDiagnostics);
            repl.Run();

            return SuccessExitCode;
        }

        compiler.Compile();

        err = ResolveDiagnostics(compiler);
        if (err > 0)
            return err;

        ResolveCompilerOutput(compiler);

        return SuccessExitCode;
    }
}

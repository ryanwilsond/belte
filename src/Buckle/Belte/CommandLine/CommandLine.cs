using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Buckle;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Diagnostics;
using Repl;

namespace Belte.CommandLine;

/// <summary>
/// Handles all command-line interaction, argument parsing, and <see cref="Compiler" /> invocation.
/// </summary>
public static partial class BuckleCommandLine {
    private const int SuccessExitCode = 0;
    private const int ErrorExitCode = 1;
    private const int FatalExitCode = 2;

    private static readonly string[] AllowedOptions = {
        "error",    // Treats all warnings as errors
        "ignore",   // Ignores all warnings
        "all"       // Shows additional warnings, and shows warnings while interpreting
    };

    /// <summary>
    /// Processes/decodes command-line arguments, and invokes <see cref="Compiler" />.
    /// </summary>
    /// <param name="args">Command-line arguments from Main.</param>
    /// <returns>Error code, 0 = success.</returns>
    public static int ProcessArgs(string[] args) {
        int err;
        Compiler compiler = new Compiler(ResolveDiagnostics);
        compiler.me = Process.GetCurrentProcess().ProcessName;

        compiler.state = DecodeOptions(
            args, out DiagnosticQueue<Diagnostic> diagnostics, out ShowDialogs dialogs
        );

        bool hasDialog = dialogs.machine || dialogs.version || dialogs.help || dialogs.error != null;

        string resources = Path.Combine(GetExecutingPath(), "Resources");
        bool corrupt = false;

        if (!Directory.Exists(resources)) {
            corrupt = true;
            ResolveDiagnostic(Belte.Diagnostics.Warning.CorruptInstallation(), compiler.me, compiler.state.options);
        }

        if (hasDialog)
            diagnostics.Clear();

        if (dialogs.machine)
            ShowMachineDialog();

        if (dialogs.version)
            ShowVersionDialog();

        if (dialogs.help && !corrupt)
            ShowHelpDialog();

        if (dialogs.error != null && !corrupt) {
            ShowErrorHelp(dialogs.error, out DiagnosticQueue<Diagnostic> dialogDiagnostics);
            diagnostics.Move(dialogDiagnostics);
        }

        if (hasDialog) {
            ResolveDiagnostics(diagnostics, compiler.me, compiler.state.options);
            return SuccessExitCode;
        }

        err = ResolveDiagnostics(diagnostics, compiler.me, compiler.state.options);

        if (err > 0)
            return err;

        ResolveOutputFiles(compiler);
        ReadInputFiles(compiler, out diagnostics);

        err = ResolveDiagnostics(diagnostics, compiler.me, compiler.state.options);

        if (err > 0)
            return err;

        // Only mode that does not go through one-time compilation
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

    private static void ShowErrorHelp(string error, out DiagnosticQueue<Diagnostic> diagnostics) {
        // TODO This only works for debug builds currently, not release
        string prefix = error.Substring(0, 2);
        diagnostics = new DiagnosticQueue<Diagnostic>();

        int errorCode = 0;

        try {
            errorCode = Convert.ToInt32(error.Substring(2));
        } catch (Exception e) when (e is FormatException || e is OverflowException) {
            diagnostics.Push(Belte.Diagnostics.Error.InvalidErrorCode(error));
            return;
        }

        string path = Path.Combine(GetExecutingPath(), $"Resources/ErrorDescriptions{prefix}.txt");

        if (!File.Exists(path)) {
            diagnostics.Push(Belte.Diagnostics.Error.InvalidErrorCode(error));
            return;
        }

        string allMessages = File.ReadAllText(path);

        Dictionary<int, string> messages = new Dictionary<int, string>();

        foreach (string message in allMessages.Split($"${prefix}")) {
            try {
                string code = message.Substring(0, 4);
                messages[Convert.ToInt32(code)] = message.Substring(4);
            } catch (ArgumentOutOfRangeException) {
                // ! This is bad practice
            }
        }

        if (messages.ContainsKey(errorCode)) {
            string message = messages[errorCode].Substring(2);

            if (message.EndsWith('\n'))
                message = message.Substring(0, message.Length-1);

            string[] lines = message.Split('\n');
            int count = 0;

            while (count < lines.Length) {
                // First -1 is required, second -1 is because we are printing -- More --
                // -2 is to account for the next terminal input line
                if (count > Console.WindowHeight - 1 - 1 - 2) {
                    char key = ' ';

                    do {
                        Console.Write("-- More --");
                        key = Console.ReadKey().KeyChar;
                        int currentLineCursor = Console.CursorTop;
                        Console.SetCursorPosition(0, Console.CursorTop);
                        // * Does not need -1 in some terminals
                        // Unfortunately the program cant tell what terminal is being used
                        Console.Write(new string(' ', Console.WindowWidth - 1));
                        Console.SetCursorPosition(0, currentLineCursor);
                    } while (key != '\n' && key != '\r');
                }

                string line = lines[count++];
                Console.WriteLine(line);
            }
        } else {
            diagnostics.Push(Belte.Diagnostics.Error.UnusedErrorCode(error));
        }
    }

    private static void ShowHelpDialog() {
        string path = Path.Combine(GetExecutingPath(), "Resources/HelpPrompt.txt");
        string helpMessage = File.ReadAllText(path);
        Console.WriteLine(helpMessage);
    }

    private static string GetExecutingPath() {
        string executingLocation = Assembly.GetExecutingAssembly().Location;
        string executingPath = System.IO.Path.GetDirectoryName(executingLocation);
        return executingPath;
    }

    private static void ShowMachineDialog() {
        string machineMessage = "Machine: x86_64-w64";
        Console.WriteLine(machineMessage);
    }

    private static void ShowVersionDialog() {
        string versionMessage = "Version: Buckle 0.1";
        Console.WriteLine(versionMessage);
    }

    private static void PrettyPrintDiagnostic(BelteDiagnostic diagnostic, ConsoleColor? textColor, string[] options) {
        void ResetColor() {
            if (textColor != null)
                Console.ForegroundColor = textColor.Value;
            else
                Console.ResetColor();
        }

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

        var severity = diagnostic.info.severity;

        if (severity == DiagnosticType.Warning && options.Contains("error"))
            severity = DiagnosticType.Error;

        if (severity == DiagnosticType.Error) {
            highlightColor = ConsoleColor.Red;
            Console.ForegroundColor = highlightColor;
            Console.Write(" error");
        } else if (severity == DiagnosticType.Fatal) {
            highlightColor = ConsoleColor.Red;
            Console.ForegroundColor = highlightColor;
            Console.Write(" fatal error");
        } else if (severity == DiagnosticType.Warning) {
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

        ResetColor();
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
        ResetColor();
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

        ResetColor();
    }

    private static DiagnosticType ResolveDiagnostic<Type>(
        Type diagnostic, string me, string[] options, ConsoleColor? textColor = null)
        where Type : Diagnostic {
        ConsoleColor previous = Console.ForegroundColor;

        void ResetColor() {
            if (textColor != null)
                Console.ForegroundColor = textColor.Value;
            else
                Console.ResetColor();
        }

        var severity = diagnostic.info.severity;

        if (severity == DiagnosticType.Warning && options.Contains("error"))
            severity = DiagnosticType.Error;
        if (severity == DiagnosticType.Warning && options.Contains("ignore"))
            severity = DiagnosticType.Unknown;

        ResetColor();

        if (severity == DiagnosticType.Unknown) {
        } else if (diagnostic.info.module != "BU" || (diagnostic is BelteDiagnostic bd && bd.location == null)) {
            Console.Write($"{me}: ");

            if (severity == DiagnosticType.Warning) {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write("warning ");
            } else if (severity == DiagnosticType.Error) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("error ");
            } else if (severity == DiagnosticType.Fatal) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("fatal error ");
            }

            string errorCode = diagnostic.info.code.Value.ToString();
            errorCode = errorCode.PadLeft(4, '0');
            Console.Write($"{diagnostic.info.module}{errorCode}: ");

            ResetColor();
            Console.WriteLine(diagnostic.message);
        } else {
            PrettyPrintDiagnostic(diagnostic as BelteDiagnostic, textColor, options);
        }

        Console.ForegroundColor = previous;
        return severity;
    }

    private static int ResolveDiagnostics<Type>(
        DiagnosticQueue<Type> diagnostics, string me, string[] options, ConsoleColor? textColor = null)
        where Type : Diagnostic {
        if (diagnostics.count == 0)
            return SuccessExitCode;

        DiagnosticType worst = DiagnosticType.Unknown;
        Diagnostic diagnostic = diagnostics.Pop();

        while (diagnostic != null) {
            DiagnosticType temp = ResolveDiagnostic(diagnostic, me, options, textColor);

            switch (temp) {
                case DiagnosticType.Warning:
                    if (worst == DiagnosticType.Unknown)
                        worst = temp;

                    break;
                case DiagnosticType.Error:
                    if (worst != DiagnosticType.Fatal)
                        worst = temp;

                    break;
                case DiagnosticType.Fatal:
                    worst = temp;
                    break;
            }

            diagnostic = diagnostics.Pop();
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

    private static int ResolveDiagnostics(Compiler compiler) {
        return ResolveDiagnostics(compiler, null);
    }

    private static int ResolveDiagnostics(
        Compiler compiler, string me = null, ConsoleColor textColor = ConsoleColor.White) {
        return ResolveDiagnostics(compiler.diagnostics, me ?? compiler.me, compiler.state.options, textColor);
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

    private static void ReadInputFiles(Compiler compiler, out DiagnosticQueue<Diagnostic> diagnostics) {
        diagnostics = new DiagnosticQueue<Diagnostic>();

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
                    diagnostics.Push(Belte.Diagnostics.Warning.IgnoringCompiledFile(task.inputFilename));
                    break;
                default:
                    break;
            }
        }
    }
}

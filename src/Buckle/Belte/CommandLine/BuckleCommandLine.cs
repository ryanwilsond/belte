using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    public static int ProcessArgs(string[] args, AppSettings appSettings) {
        int err;
        var compiler = new Compiler();
        compiler.me = Process.GetCurrentProcess().ProcessName;

        compiler.state = DecodeOptions(
            args, out DiagnosticQueue<Diagnostic> diagnostics, out ShowDialogs dialogs
        );

        var hasDialog = dialogs.machine || dialogs.version || dialogs.help || dialogs.error != null;
        var corrupt = false;

        if (!Directory.Exists(appSettings.resourcesPath)) {
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
            ShowHelpDialog(appSettings);

        if (dialogs.error != null && !corrupt) {
            ShowErrorHelp(dialogs.error, appSettings, out DiagnosticQueue<Diagnostic> dialogDiagnostics);
            diagnostics.Move(dialogDiagnostics);
        }

        if (hasDialog) {
            ResolveDiagnostics(diagnostics, compiler.me, compiler.state.options);
            return SuccessExitCode;
        }

        err = ResolveDiagnostics(diagnostics, compiler.me, compiler.state.options);

        // Only mode that does not go through one-time compilation
        if (compiler.state.buildMode == BuildMode.Repl) {
            BelteRepl repl = new BelteRepl(compiler, ResolveDiagnostics);
            repl.Run();

            return SuccessExitCode;
        }

        if (err > 0)
            return err;

        ResolveOutputFiles(compiler);
        ReadInputFiles(compiler, out diagnostics);

        err = ResolveDiagnostics(diagnostics, compiler.me, compiler.state.options);

        if (err > 0)
            return err;

        compiler.Compile();

        err = ResolveDiagnostics(compiler);

        if (err > 0)
            return err;

        return SuccessExitCode;
    }

    private static void ShowErrorHelp(
        string error, AppSettings appSettings, out DiagnosticQueue<Diagnostic> diagnostics) {
        string prefix;

        if (error.Length < 3 || (Char.IsDigit(error[0]) && Char.IsDigit(error[1]))) {
            prefix = "BU";
            error = "BU" + error;
        } else {
            prefix = error.Substring(0, 2);
        }

        diagnostics = new DiagnosticQueue<Diagnostic>();
        var errorCode = 0;

        try {
            errorCode = Convert.ToInt32(error.Substring(2));
        } catch (Exception e) when (e is FormatException || e is OverflowException) {
            diagnostics.Push(Belte.Diagnostics.Error.InvalidErrorCode(error));
            return;
        }

        var path = Path.Combine(appSettings.resourcesPath, $"ErrorDescriptions{prefix}.txt");

        if (!File.Exists(path)) {
            diagnostics.Push(Belte.Diagnostics.Error.InvalidErrorCode(error));
            return;
        }

        var allMessages = File.ReadAllText(path);
        var messages = new Dictionary<int, string>();

        foreach (string message in allMessages.Split($"${prefix}")) {
            try {
                string code = message.Substring(0, 4);
                messages[Convert.ToInt32(code)] = message.Substring(4);
            } catch (ArgumentOutOfRangeException) { }
        }

        if (!messages.ContainsKey(errorCode)) {
            diagnostics.Push(Belte.Diagnostics.Error.UnusedErrorCode(error));
            return;
        }

        var foundMessage = messages[errorCode].Substring(2);

        if (foundMessage.EndsWith(Environment.NewLine))
            foundMessage = foundMessage.Substring(0, foundMessage.Length - 1);

        var lines = foundMessage.Split(Environment.NewLine);
        var count = 0;

        while (count < lines.Length) {
            // First -1 is required, second -1 is because we are printing -- More --
            // -2 is to account for the next terminal input line
            if (count > Console.WindowHeight - 1 - 1 - 2) {
                var key = ' ';

                do {
                    Console.Write("-- More --");
                    key = Console.ReadKey().KeyChar;
                    var currentLineCursor = Console.CursorTop;
                    Console.SetCursorPosition(0, Console.CursorTop);
                    // * Does not need -1 in some terminals
                    // Unfortunately the program cant tell what terminal is being used
                    Console.Write(new string(' ', Console.WindowWidth - 1));
                    Console.SetCursorPosition(0, currentLineCursor);
                } while (key != '\n' && key != '\r');
            }

            var line = lines[count++];
            Console.WriteLine(line);
        }
    }

    private static void ShowHelpDialog(AppSettings appSettings) {
        var path = Path.Combine(appSettings.resourcesPath, "HelpPrompt.txt");
        var helpMessage = File.ReadAllText(path);
        Console.WriteLine(helpMessage);
    }

    private static void ShowMachineDialog() {
        var machineMessage = "Machine: x86_64-w64";
        Console.WriteLine(machineMessage);
    }

    private static void ShowVersionDialog() {
        var versionMessage = "Version: Buckle 0.1";
        Console.WriteLine(versionMessage);
    }

    private static void PrettyPrintDiagnostic(BelteDiagnostic diagnostic, ConsoleColor? textColor, string[] options) {
        void ResetColor() {
            if (textColor != null)
                Console.ForegroundColor = textColor.Value;
            else
                Console.ResetColor();
        }

        var span = diagnostic.location.span;
        var text = diagnostic.location.text;

        var lineNumber = text.GetLineIndex(span.start);
        var line = text.lines[lineNumber];
        var column = span.start - line.start + 1;
        var lineText = line.ToString();

        var filename = diagnostic.location.fileName;

        if (!string.IsNullOrEmpty(filename))
            Console.Write($"{filename}:");

        Console.Write($"{lineNumber + 1}:{column}:");

        var highlightColor = ConsoleColor.White;

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

        var prefixSpan = TextSpan.FromBounds(line.start, span.start);
        var suffixSpan = TextSpan.FromBounds(span.end, line.end);

        var prefix = text.ToString(prefixSpan);
        var focus = text.ToString(span);
        var suffix = text.ToString(suffixSpan);

        Console.Write($" {prefix}");
        Console.ForegroundColor = highlightColor;
        Console.Write(focus);
        ResetColor();
        Console.WriteLine(suffix);

        Console.ForegroundColor = highlightColor;
        var markerPrefix = " " + Regex.Replace(prefix, @"\S", " ");
        var marker = "^";

        if (span.length > 0 && column != lineText.Length)
            marker += new string('~', span.length - 1);

        Console.WriteLine(markerPrefix + marker);

        if (diagnostic.suggestion != null) {
            Console.ForegroundColor = ConsoleColor.Green;
            var suggestion = diagnostic.suggestion.Replace("%", focus);
            Console.WriteLine(markerPrefix + suggestion);
        }

        ResetColor();
    }

    private static DiagnosticType ResolveDiagnostic<Type>(
        Type diagnostic, string me, string[] options, ConsoleColor? textColor = null)
        where Type : Diagnostic {
        var previous = Console.ForegroundColor;

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
            // Ignore
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

            var errorCode = diagnostic.info.code.Value.ToString();
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

        var worst = DiagnosticType.Unknown;
        var diagnostic = diagnostics.Pop();

        while (diagnostic != null) {
            var temp = ResolveDiagnostic(diagnostic, me, options, textColor);

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
            var inter = file.inputFilename.Split('.')[0];

            switch (compiler.state.finishStage) {
                case CompilerStage.Preprocessed:
                    inter += ".pblt";
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

    private static void ReadInputFiles(Compiler compiler, out DiagnosticQueue<Diagnostic> diagnostics) {
        diagnostics = new DiagnosticQueue<Diagnostic>();

        for (int i=0; i<compiler.state.tasks.Length; i++) {
            ref var task = ref compiler.state.tasks[i];

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

    private static CompilerState DecodeOptions(
        string[] args, out DiagnosticQueue<Diagnostic> diagnostics, out ShowDialogs dialogs) {
        var state = new CompilerState();
        var tasks = new List<FileState>();
        var references = new List<string>();
        var options = new List<string>();
        var diagnosticsCL = new DiagnosticQueue<Diagnostic>();
        diagnostics = new DiagnosticQueue<Diagnostic>();

        var specifyStage = false;
        var specifyOut = false;
        var specifyModule = false;

        var tempDialogs = new ShowDialogs();

        tempDialogs.help = false;
        tempDialogs.machine = false;
        tempDialogs.version = false;
        tempDialogs.error = null;

        state.buildMode = BuildMode.Independent;
        state.finishStage = CompilerStage.Linked;
        state.outputFilename = "a.exe";
        state.moduleName = "defaultModuleName";

        void DecodeSimpleOption(string arg) {
            switch (arg) {
                case "-p":
                    specifyStage = true;
                    state.finishStage = CompilerStage.Preprocessed;
                    break;
                case "-s":
                    specifyStage = true;
                    state.finishStage = CompilerStage.Compiled;
                    break;
                case "-c":
                    specifyStage = true;
                    state.finishStage = CompilerStage.Assembled;
                    break;
                case "-r":
                    state.buildMode = BuildMode.Repl;
                    break;
                case "-i":
                    state.buildMode = BuildMode.Interpret;
                    break;
                case "-t":
                    state.buildMode = BuildMode.CSharpTranspile;
                    break;
                case "-d":
                    state.buildMode = BuildMode.Dotnet;
                    break;
                case "-h":
                case "--help":
                    tempDialogs.help = true;
                    break;
                case "--dumpmachine":
                    tempDialogs.machine = true;
                    break;
                case "--version":
                    tempDialogs.version = true;
                    break;
                default:
                    diagnosticsCL.Push(Belte.Diagnostics.Error.UnrecognizedOption(arg));
                    break;
            }
        }

        for (int i=0; i<args.Length; i++) {
            var arg = args[i];

            if (!arg.StartsWith('-')) {
                diagnostics.Move(ResolveInputFileOrDir(arg, ref tasks));
                continue;
            }

            if (arg.StartsWith("-o")) {
                specifyOut = true;

                if (arg != "-o") {
                    state.outputFilename = arg.Substring(2);
                    continue;
                }

                if (i < args.Length - 1)
                    state.outputFilename = args[++i];
                else
                    diagnostics.Push(Belte.Diagnostics.Error.MissingFilenameO());
            } else if (arg.StartsWith("--explain")) {
                if (tempDialogs.error != null) {
                    diagnostics.Push(Belte.Diagnostics.Error.MultipleExplains());
                    continue;
                }

                if (arg != "--explain") {
                    var errorCode = args[i].Substring(9);
                    tempDialogs.error = errorCode;
                    continue;
                }

                if (i < args.Length - 1) {
                    i++;
                    tempDialogs.error = args[i];
                } else {
                    diagnostics.Push(Belte.Diagnostics.Error.MissingCodeExplain());
                }
            } else if (arg.StartsWith("--modulename")) {
                if (arg != "--modulename" && arg != "--modulename=") {
                    specifyModule = true;
                    state.moduleName = arg.Substring(13);
                } else {
                    diagnostics.Push(Belte.Diagnostics.Error.MissingModuleName(arg));
                }
            } else if (arg.StartsWith("--ref")) {
                if (arg != "--ref" && arg != "--ref=")
                    references.Add(arg.Substring(6));
                else
                    diagnostics.Push(Belte.Diagnostics.Error.MissingReference(arg));
            } else if (arg.StartsWith("-W")) {
                if (arg.Length == 2) {
                    diagnostics.Push(Belte.Diagnostics.Error.NoOptionAfterW());
                    continue;
                }

                string[] wArgs = arg.Substring(2).Split(',');

                foreach (string wArg in wArgs) {
                    if (AllowedOptions.Contains(wArg))
                        options.Add(wArg);
                    else
                        diagnostics.Push(Belte.Diagnostics.Error.UnrecognizedWOption(wArg));
                }
            } else {
                DecodeSimpleOption(arg);
            }
        }

        dialogs = tempDialogs;
        diagnostics.Move(diagnosticsCL);

        if (dialogs.machine || dialogs.help || dialogs.version || dialogs.error != null)
            return state;

        state.tasks = tasks.ToArray();
        state.references = references.ToArray();
        state.options = options.ToArray();

        if (!specifyOut && state.buildMode == BuildMode.CSharpTranspile)
            state.outputFilename = "a.cs";

        if (args.Length > 1 && state.buildMode == BuildMode.Repl)
            diagnostics.Push(Belte.Diagnostics.Warning.ReplInvokeIgnore());

        if (specifyStage && state.buildMode == BuildMode.Dotnet)
            diagnostics.Push(Belte.Diagnostics.Error.CannotSpecifyWithDotnet());

        if (specifyOut && specifyStage && state.tasks.Length > 1 && !(state.buildMode == BuildMode.Dotnet))
            diagnostics.Push(Belte.Diagnostics.Error.CannotSpecifyWithMultipleFiles());

        if ((specifyStage || specifyOut) && state.buildMode == BuildMode.Interpret)
            diagnostics.Push(Belte.Diagnostics.Error.CannotSpecifyWithInterpreter());

        if (specifyModule && state.buildMode != BuildMode.Dotnet)
            diagnostics.Push(Belte.Diagnostics.Error.CannotSpecifyModuleNameWithoutDotnet());

        if (references.Count != 0 && state.buildMode != BuildMode.Dotnet)
            diagnostics.Push(Belte.Diagnostics.Error.CannotSpecifyReferencesWithoutDotnet());

        if (state.tasks.Length == 0 && !(state.buildMode == BuildMode.Repl))
            diagnostics.Push(Belte.Diagnostics.Error.NoInputFiles());

        state.outputFilename = state.outputFilename.Trim();

        return state;
    }

    private static DiagnosticQueue<Diagnostic> ResolveInputFileOrDir(string name, ref List<FileState> tasks) {
        var filenames = new List<string>();
        var diagnostics = new DiagnosticQueue<Diagnostic>();

        if (Directory.Exists(name)) {
            filenames.AddRange(Directory.GetFiles(name));
        } else if (File.Exists(name)) {
            filenames.Add(name);
        } else {
            diagnostics.Push(Belte.Diagnostics.Error.NoSuchFileOrDirectory(name));
            return diagnostics;
        }

        foreach (string filename in filenames) {
            var task = new FileState();
            task.inputFilename = filename;

            var parts = task.inputFilename.Split('.');
            var type = parts[parts.Length - 1];

            switch (type) {
                case "belte":
                case "blt":
                    task.stage = CompilerStage.Raw;
                    break;
                case "pblt":
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
                    diagnostics.Push(Belte.Diagnostics.Warning.IgnoringUnknownFileType(task.inputFilename));
                    continue;
            }

            tasks.Add(task);
        }

        return diagnostics;
    }
}

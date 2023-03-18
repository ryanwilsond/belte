using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
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

    /// <summary>
    /// Processes/decodes command-line arguments, and invokes <see cref="Compiler" />.
    /// </summary>
    /// <param name="args">Command-line arguments from Main.</param>
    /// <param name="appSettings">Settings from the App.config.</param>
    /// <returns>Error code, 0 = success.</returns>
    public static int ProcessArgs(string[] args, AppSettings appSettings) {
        int err;
        var compiler = new Compiler();
        compiler.me = Process.GetCurrentProcess().ProcessName;
        compiler.state = DecodeOptions(args, out var diagnostics, out var dialogs, out var multipleExplains);

        var hasDialog = dialogs.machine || dialogs.version || dialogs.help || dialogs.error != null;
        var corrupt = false;

        if (!Directory.Exists(appSettings.resourcesPath)) {
            corrupt = true;
            ResolveDiagnostic(Belte.Diagnostics.Warning.CorruptInstallation(), compiler.me, compiler.state);
        }

        if (multipleExplains)
            ResolveDiagnostic(Belte.Diagnostics.Error.MultipleExplains(), compiler.me, compiler.state);

        if (hasDialog) {
            diagnostics.Clear();
            diagnostics.Move(ShowDialogs(dialogs, corrupt, appSettings, multipleExplains));
            ResolveDiagnostics(diagnostics, compiler.me, compiler.state);

            return SuccessExitCode;
        }

        // Only mode that does not go through one-time compilation
        if (compiler.state.buildMode == BuildMode.Repl) {
            ResolveDiagnostics(diagnostics, compiler.me, compiler.state);

            if (!compiler.state.noOut) {
                BelteRepl repl = new BelteRepl(compiler, ResolveDiagnostics);
                repl.Run();
            }

            return SuccessExitCode;
        }

        err = ResolveDiagnostics(diagnostics, compiler.me, compiler.state);

        if (err > 0)
            return err;

        if (!compiler.state.noOut)
            ResolveOutputFiles(compiler);

        ReadInputFiles(compiler, out diagnostics);

        err = ResolveDiagnostics(diagnostics, compiler.me, compiler.state);

        if (err > 0)
            return err;

        compiler.Compile();

        err = ResolveDiagnostics(compiler);

        if (err > 0)
            return err;

        return SuccessExitCode;
    }

    private static DiagnosticQueue<Diagnostic> ShowDialogs(
        ShowDialogs dialogs, bool corrupt, AppSettings appSettings, bool multipleExplains) {
        DiagnosticQueue<Diagnostic> diagnostics = new DiagnosticQueue<Diagnostic>();

        if (dialogs.machine)
            ShowMachineDialog();

        if (dialogs.version)
            ShowVersionDialog();

        if (dialogs.help && !corrupt)
            ShowHelpDialog(appSettings);

        if (dialogs.error != null && !corrupt && !multipleExplains)
            ShowErrorHelp(dialogs.error, appSettings, out diagnostics);

        return diagnostics;
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

    private static void PrettyPrintDiagnostic(BelteDiagnostic diagnostic, ConsoleColor? textColor) {
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

        switch (severity) {
            case DiagnosticSeverity.Debug:
                highlightColor = ConsoleColor.Gray;
                Console.ForegroundColor = highlightColor;
                Console.Write("debug ");
                break;
            case DiagnosticSeverity.Info:
                highlightColor = ConsoleColor.Yellow;
                Console.ForegroundColor = highlightColor;
                Console.Write(" info");
                break;
            case DiagnosticSeverity.Warning:
                highlightColor = ConsoleColor.Magenta;
                Console.ForegroundColor = highlightColor;
                Console.Write(" warning");
                break;
            case DiagnosticSeverity.Error:
                highlightColor = ConsoleColor.Red;
                Console.ForegroundColor = highlightColor;
                Console.Write(" error");
                break;
            case DiagnosticSeverity.Fatal:
                highlightColor = ConsoleColor.Red;
                Console.ForegroundColor = highlightColor;
                Console.Write(" fatal");
                break;
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

    private static DiagnosticSeverity ResolveDiagnostic<Type>(
        Type diagnostic, string me, CompilerState state, ConsoleColor? textColor = null)
        where Type : Diagnostic {
        var previous = Console.ForegroundColor;

        void ResetColor() {
            if (textColor != null)
                Console.ForegroundColor = textColor.Value;
            else
                Console.ResetColor();
        }

        var severity = diagnostic.info.severity;
        ResetColor();

        if ((int)state.severity > (int)severity) {
            // Ignore
        } else if (diagnostic.info.module != "BU" || (diagnostic is BelteDiagnostic bd && bd.location == null)) {
            Console.Write($"{me}: ");

            switch (severity) {
                case DiagnosticSeverity.Debug:
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.Write("debug ");
                    break;
                case DiagnosticSeverity.Info:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("info ");
                    break;
                case DiagnosticSeverity.Warning:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write("warning ");
                    break;
                case DiagnosticSeverity.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("error ");
                    break;
                case DiagnosticSeverity.Fatal:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("fatal ");
                    break;
            }

            var errorCode = diagnostic.info.code.Value.ToString();
            errorCode = errorCode.PadLeft(4, '0');
            Console.Write($"{diagnostic.info.module}{errorCode}: ");

            ResetColor();
            Console.WriteLine(diagnostic.message);
        } else {
            PrettyPrintDiagnostic(diagnostic as BelteDiagnostic, textColor);
        }

        Console.ForegroundColor = previous;

        return severity;
    }

    private static int ResolveDiagnostics<Type>(
        DiagnosticQueue<Type> diagnostics, string me, CompilerState state, ConsoleColor? textColor = null)
        where Type : Diagnostic {
        if (diagnostics.count == 0)
            return SuccessExitCode;

        var worst = diagnostics.ToList().Select(d => (int)d.info.severity).Max();
        var diagnostic = diagnostics.Pop();

        while (diagnostic != null) {
            ResolveDiagnostic(diagnostic, me, state, textColor);
            diagnostic = diagnostics.Pop();
        }

        switch ((DiagnosticSeverity)worst) {
            case DiagnosticSeverity.Fatal:
                return FatalExitCode;
            case DiagnosticSeverity.Error:
                return ErrorExitCode;
            case DiagnosticSeverity.Warning:
            case DiagnosticSeverity.Info:
            case DiagnosticSeverity.Debug:
            default:
                return SuccessExitCode;
        }
    }

    private static int ResolveDiagnostics(Compiler compiler) {
        return ResolveDiagnostics(compiler, null);
    }

    private static int ResolveDiagnostics(
        Compiler compiler, string me = null, ConsoleColor textColor = ConsoleColor.White) {
        return ResolveDiagnostics(compiler.diagnostics, me ?? compiler.me, compiler.state, textColor);
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
            var opened = false;

            switch (task.stage) {
                case CompilerStage.Raw:
                case CompilerStage.Preprocessed:
                case CompilerStage.Compiled:
                    for (int j=0; j<3; j++) {
                        try {
                            task.fileContent.text = File.ReadAllText(task.inputFilename);
                            opened = true;
                        } catch (IOException) {
                            Thread.Sleep(100);

                            if (j == 2)
                                break;
                        }
                    }

                    if (!opened)
                        diagnostics.Push(Belte.Diagnostics.Error.UnableToOpenFile(task.inputFilename));

                    break;
                case CompilerStage.Assembled:
                    for (int j=0; j<3; j++) {
                        try {
                            task.fileContent.bytes = File.ReadAllBytes(task.inputFilename).ToList();
                            opened = true;
                        } catch (IOException) {
                            Thread.Sleep(100);

                            if (j == 2)
                                break;
                        }
                    }

                    if (!opened)
                        diagnostics.Push(Belte.Diagnostics.Error.UnableToOpenFile(task.inputFilename));

                    break;
                case CompilerStage.Linked:
                    diagnostics.Push(Belte.Diagnostics.Info.IgnoringCompiledFile(task.inputFilename));
                    break;
                default:
                    break;
            }
        }
    }

    private static CompilerState DecodeOptions(
        string[] args, out DiagnosticQueue<Diagnostic> diagnostics,
        out ShowDialogs dialogs, out bool multipleExplains) {
        var state = new CompilerState();
        var tasks = new List<FileState>();
        var references = new List<string>();
        var diagnosticsCL = new DiagnosticQueue<Diagnostic>();
        diagnostics = new DiagnosticQueue<Diagnostic>();

        var specifyStage = false;
        var specifyOut = false;
        var specifyModule = false;
        DiagnosticSeverity? severity = null;

        var tempDialogs = new ShowDialogs();

        tempDialogs.help = false;
        tempDialogs.machine = false;
        tempDialogs.version = false;
        tempDialogs.error = null;
        multipleExplains = false;

        state.buildMode = BuildMode.Independent;
        state.finishStage = CompilerStage.Linked;
        state.outputFilename = "a.exe";
        state.moduleName = "defaultModuleName";
        state.noOut = false;

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
                case "--no-out":
                    state.noOut = true;
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
                    multipleExplains = true;
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
            } else if (arg.StartsWith("--severity")) {
                if (arg == "--severity" || arg == "--severity=") {
                    diagnostics.Push(Belte.Diagnostics.Error.MissingSeverity(arg));
                    continue;
                }

                var severityString = arg.Substring(11);

                if (Enum.TryParse<DiagnosticSeverity>(severityString, true, out var severityLevel))
                    severity = severityLevel;
                else
                    diagnostics.Push(Belte.Diagnostics.Error.UnrecognizedSeverity(severityString));
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
        state.severity = severity ??
            (state.buildMode == BuildMode.Independent ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning);

        if (!specifyOut && state.buildMode == BuildMode.CSharpTranspile)
            state.outputFilename = "a.cs";

        if (args.Length > 1 && state.buildMode == BuildMode.Repl)
            diagnostics.Push(Belte.Diagnostics.Info.ReplInvokeIgnore());

        if (specifyStage && state.buildMode == BuildMode.Dotnet)
            diagnostics.Push(Belte.Diagnostics.Fatal.CannotSpecifyWithDotnet());

        if (specifyOut && specifyStage && state.tasks.Length > 1 && !(state.buildMode == BuildMode.Dotnet))
            diagnostics.Push(Belte.Diagnostics.Fatal.CannotSpecifyWithMultipleFiles());

        if ((specifyStage || specifyOut) && state.buildMode == BuildMode.Interpret)
            diagnostics.Push(Belte.Diagnostics.Fatal.CannotSpecifyWithInterpreter());

        if (specifyModule && state.buildMode != BuildMode.Dotnet)
            diagnostics.Push(Belte.Diagnostics.Fatal.CannotSpecifyModuleNameWithoutDotnet());

        if (references.Count != 0 && state.buildMode != BuildMode.Dotnet)
            diagnostics.Push(Belte.Diagnostics.Fatal.CannotSpecifyReferencesWithoutDotnet());

        if (state.tasks.Length == 0 && !(state.buildMode == BuildMode.Repl))
            diagnostics.Push(Belte.Diagnostics.Fatal.NoInputFiles());

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
                case "exe":
                    task.stage = CompilerStage.Linked;
                    break;
                default:
                    diagnostics.Push(Belte.Diagnostics.Info.IgnoringUnknownFileType(task.inputFilename));
                    continue;
            }

            tasks.Add(task);
        }

        return diagnostics;
    }
}

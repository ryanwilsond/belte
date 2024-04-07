using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Buckle;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Diagnostics;
using Repl;

namespace CommandLine;

/// <summary>
/// Handles all command-line interaction, argument parsing, and <see cref="Compiler" /> invocation.
/// </summary>
public static class BuckleCommandLine {
    private const int SuccessExitCode = 0;
    private const int ErrorExitCode = 1;
    private const int FatalExitCode = 2;

    /// <summary>
    /// Processes/decodes command-line arguments, and invokes <see cref="Compiler" />.
    /// </summary>
    /// <param name="args">Command-line arguments from Main.</param>
    /// <returns>Error code, 0 = success.</returns>
    public static int ProcessArgs(string[] args) {
        int err;
        var compiler = new Compiler {
            me = Process.GetCurrentProcess().ProcessName,
            state = DecodeOptions(args, out var diagnostics, out var dialogs, out var multipleExplains)
        };

        var hasDialog = dialogs.machine || dialogs.version || dialogs.help || dialogs.error != null;

        if (multipleExplains)
            ResolveDiagnostic(Belte.Diagnostics.Error.MultipleExplains(), compiler.me, compiler.state);

        if (hasDialog) {
            diagnostics.Clear();
            diagnostics.Move(ShowDialogs(dialogs, multipleExplains));
            ResolveDiagnostics(diagnostics, compiler.me, compiler.state);

            return SuccessExitCode;
        }

        // Only mode that does not go through one-time compilation
        if (compiler.state.buildMode == BuildMode.Repl) {
            ResolveDiagnostics(diagnostics, compiler.me, compiler.state);

            if (!compiler.state.noOut) {
                var repl = new BelteRepl(compiler, ResolveDiagnostics);
                repl.Run();
            }

            return SuccessExitCode;
        }

        err = ResolveDiagnostics(diagnostics, compiler.me, compiler.state);

        if (err > 0)
            return err;

        if (!compiler.state.noOut)
            CleanOutputFiles(compiler);

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

    private static DiagnosticQueue<Diagnostic> ShowDialogs(ShowDialogs dialogs, bool multipleExplains) {
        var diagnostics = new DiagnosticQueue<Diagnostic>();

        if (dialogs.machine)
            ShowMachineDialog();

        if (dialogs.version)
            ShowVersionDialog();

        if (dialogs.help)
            ShowHelpDialog();

        if (dialogs.error != null && !multipleExplains)
            ShowErrorHelp(dialogs.error, out diagnostics);

        return diagnostics;
    }

    private static void ShowErrorHelp(string error, out DiagnosticQueue<Diagnostic> diagnostics) {
        string prefix;

        if (error.Length < 3 || (char.IsDigit(error[0]) && char.IsDigit(error[1]))) {
            prefix = "BU";
            error = prefix + error;
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

        string allDescriptions = null;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
            var foundDescriptions = assembly.GetManifestResourceNames()
                .Where(r => r.EndsWith($"Resources.ErrorDescriptions{prefix}.txt"));

            if (foundDescriptions.Any()) {
                using var stream = assembly.GetManifestResourceStream(foundDescriptions.First());
                using var reader = new StreamReader(stream);
                allDescriptions = reader.ReadToEnd();
                break;
            }
        }

        if (allDescriptions is null) {
            diagnostics.Push(Belte.Diagnostics.Error.InvalidErrorCode(error));
            return;
        }

        var messages = new Dictionary<int, string>();

        foreach (var message in allDescriptions.Split($"${prefix}")) {
            try {
                var code = message.Substring(0, 4);
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

    private static void ShowHelpDialog() {
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream("CommandLine.Resources.HelpPrompt.txt");
        using var reader = new StreamReader(stream);
        Console.WriteLine(reader.ReadToEnd().TrimEnd());
    }

    private static void ShowMachineDialog() {
        var machineMessage = "Machine: x86_64-w64";
        Console.WriteLine(machineMessage);
    }

    private static void ShowVersionDialog() {
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream("CommandLine.Resources.Version.txt");
        using var reader = new StreamReader(stream);
        Console.WriteLine($"Version: Buckle {reader.ReadLine()}");
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
        var line = text.GetLine(lineNumber);
        var column = span.start - line.start + 1;
        var lineText = line.ToString();

        var fileName = diagnostic.location.fileName;

        if (!string.IsNullOrEmpty(fileName))
            Console.Write($"{fileName}:");

        Console.Write($"{lineNumber + 1}:{column}:");

        var highlightColor = ConsoleColor.White;

        var severity = diagnostic.info.severity;

        switch (severity) {
            case DiagnosticSeverity.Debug:
                highlightColor = ConsoleColor.Gray;
                Console.ForegroundColor = highlightColor;
                Console.Write(" debug");
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

        if (diagnostic.suggestions.Length > 0) {
            Console.ForegroundColor = ConsoleColor.Green;
            var firstSuggestion = diagnostic.suggestions[0].Replace("%", focus);
            Console.WriteLine(markerPrefix + firstSuggestion);

            for (var i = 1; i < diagnostic.suggestions.Length; i++) {
                var suggestion = diagnostic.suggestions[i].Replace("%", focus);
                ResetColor();
                Console.Write(markerPrefix.Substring(0, markerPrefix.Length - 3) + "or ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(suggestion);
            }
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
            // Ignore the diagnostic
        } else if (diagnostic.info.module != "BU" || (diagnostic is BelteDiagnostic bd && bd.location is null)) {
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
        if (diagnostics.Count == 0)
            return SuccessExitCode;

        var worst = diagnostics.ToList().Select(d => (int)d.info.severity).Max();
        var diagnostic = diagnostics.Pop();

        while (diagnostic != null) {
            ResolveDiagnostic(diagnostic, me, state, textColor);
            diagnostic = diagnostics.Pop();
        }

        return (DiagnosticSeverity)worst switch {
            DiagnosticSeverity.Fatal => FatalExitCode,
            DiagnosticSeverity.Error => ErrorExitCode,
            _ => SuccessExitCode,
        };
    }

    private static int ResolveDiagnostics(Compiler compiler) {
        return ResolveDiagnostics(compiler, null);
    }

    private static int ResolveDiagnostics(
        Compiler compiler, string me = null, ConsoleColor textColor = ConsoleColor.White) {
        return ResolveDiagnostics(compiler.diagnostics, me ?? compiler.me, compiler.state, textColor);
    }

    private static void CleanOutputFiles(Compiler compiler) {
        if (compiler.state.finishStage == CompilerStage.Finished) {
            if (File.Exists(compiler.state.outputFilename))
                File.Delete(compiler.state.outputFilename);

            return;
        }

        foreach (var file in compiler.state.tasks) {
            File.Delete(file.outputFilename);
        }
    }

    private static void ReadInputFiles(Compiler compiler, out DiagnosticQueue<Diagnostic> diagnostics) {
        diagnostics = new DiagnosticQueue<Diagnostic>();

        for (var i = 0; i < compiler.state.tasks.Length; i++) {
            ref var task = ref compiler.state.tasks[i];
            var opened = false;

            switch (task.stage) {
                case CompilerStage.Raw:
                case CompilerStage.Compiled:
                    for (var j = 0; j < 3; j++) {
                        try {
                            task.fileContent.text = File.ReadAllText(task.inputFileName);
                            opened = true;
                        } catch (IOException) {
                            Thread.Sleep(100);

                            if (j == 2)
                                break;
                        }
                    }

                    if (!opened)
                        diagnostics.Push(Belte.Diagnostics.Error.UnableToOpenFile(task.inputFileName));

                    break;
                case CompilerStage.Assembled:
                    for (var j = 0; j < 3; j++) {
                        try {
                            task.fileContent.bytes = File.ReadAllBytes(task.inputFileName).ToList();
                            opened = true;
                        } catch (IOException) {
                            Thread.Sleep(100);

                            if (j == 2)
                                break;
                        }
                    }

                    if (!opened)
                        diagnostics.Push(Belte.Diagnostics.Error.UnableToOpenFile(task.inputFileName));

                    break;
                case CompilerStage.Finished:
                    diagnostics.Push(Belte.Diagnostics.Info.IgnoringCompiledFile(task.inputFileName));
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
        var arguments = new string[] { };

        var specifyStage = false;
        var specifyOut = false;
        var specifyModule = false;
        DiagnosticSeverity? severity = null;

        var tempDialogs = new ShowDialogs {
            help = false,
            machine = false,
            version = false,
            error = null
        };

        multipleExplains = false;

        state.buildMode = BuildMode.AutoRun;
        state.finishStage = CompilerStage.Finished;
        state.outputFilename = "a.exe";
        state.moduleName = "defaultModuleName";
        state.noOut = false;

        void DecodeSimpleOption(string arg) {
            switch (arg) {
                case "-s":
                    specifyStage = true;
                    state.finishStage = CompilerStage.Compiled;
                    break;
                case "-c":
                    specifyStage = true;
                    state.finishStage = CompilerStage.Assembled;
                    break;
                case "-r":
                case "--repl":
                    state.buildMode = BuildMode.Repl;
                    break;
                case "-n":
                    state.buildMode = BuildMode.Independent;
                    break;
                case "-i":
                    state.buildMode = BuildMode.AutoRun;
                    break;
                case "--script":
                    state.buildMode = BuildMode.Interpret;
                    break;
                case "--evaluate":
                    state.buildMode = BuildMode.Evaluate;
                    break;
                case "--execute":
                    state.buildMode = BuildMode.Execute;
                    break;
                case "-t":
                case "--transpile":
                    state.buildMode = BuildMode.CSharpTranspile;
                    break;
                case "-d":
                case "--dotnet":
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
                case "--noout":
                    state.noOut = true;
                    break;
                default:
                    diagnosticsCL.Push(Belte.Diagnostics.Error.UnrecognizedOption(arg));
                    break;
            }
        }

        for (var i = 0; i < args.Length; i++) {
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
                if (arg != "--ref" && arg != "--ref=" && arg.StartsWith("--ref="))
                    references.Add(arg.Substring(6));
                else if (arg != "--reference" && arg != "--reference=" && arg.StartsWith("--reference="))
                    references.Add(arg.Substring(12));
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
            } else if (arg == "--") {
                if (args.Length > i + 1)
                    arguments = args[(i + 1)..];

                break;
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
        state.arguments = arguments;
        state.severity = severity ??
            (state.buildMode == BuildMode.Independent ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning);

        if (!specifyOut && state.buildMode == BuildMode.CSharpTranspile)
            state.outputFilename = "a.cs";

        if (args.Length > 1 && state.buildMode == BuildMode.Repl)
            diagnostics.Push(Belte.Diagnostics.Info.ReplInvokeIgnore());

        if (specifyStage && state.buildMode == BuildMode.Dotnet)
            diagnostics.Push(Belte.Diagnostics.Fatal.CannotSpecifyWithDotnet());

        if (specifyOut && specifyStage && state.tasks.Length > 1 && state.buildMode != BuildMode.Dotnet)
            diagnostics.Push(Belte.Diagnostics.Fatal.CannotSpecifyWithMultipleFiles());

        if ((specifyStage || specifyOut) &&
            state.buildMode is BuildMode.AutoRun or BuildMode.Interpret or BuildMode.Evaluate or BuildMode.Execute) {
            diagnostics.Push(Belte.Diagnostics.Fatal.CannotSpecifyWithInterpreter());
        }

        if (state.tasks.Length > 1 && state.buildMode == BuildMode.Interpret)
            diagnostics.Push(Belte.Diagnostics.Fatal.CannotInterpretWithMultipleFiles());
        else if (state.buildMode == BuildMode.Interpret && state.tasks?[0].stage != CompilerStage.Raw)
            diagnostics.Push(Belte.Diagnostics.Fatal.CannotInterpretFile());

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
        var fileNames = new List<string>();
        var diagnostics = new DiagnosticQueue<Diagnostic>();

        if (Directory.Exists(name)) {
            fileNames.AddRange(Directory.GetFiles(name));
        } else if (File.Exists(name)) {
            fileNames.Add(name);
        } else {
            diagnostics.Push(Belte.Diagnostics.Error.NoSuchFileOrDirectory(name));
            return diagnostics;
        }

        foreach (var fileName in fileNames) {
            var task = new FileState {
                inputFileName = fileName
            };

            var parts = task.inputFileName.Split('.');
            var type = parts[parts.Length - 1];

            switch (type) {
                case "belte":
                case "blt":
                    task.stage = CompilerStage.Raw;
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
                    task.stage = CompilerStage.Finished;
                    break;
                default:
                    diagnostics.Push(Belte.Diagnostics.Info.IgnoringUnknownFileType(task.inputFileName));
                    continue;
            }

            tasks.Add(task);
        }

        return diagnostics;
    }
}

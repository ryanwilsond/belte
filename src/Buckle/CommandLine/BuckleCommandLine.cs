using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Buckle;
using Buckle.Diagnostics;
using Diagnostics;
using Repl;

namespace CommandLine;

/// <summary>
/// Handles all command-line interaction, argument parsing, and <see cref="Compiler" /> invocation.
/// </summary>
public static partial class BuckleCommandLine {
    private const int SuccessExitCode = 0;
    private const int ErrorExitCode = 1;
    private const int FatalExitCode = 2;
    private const int RuntimeErrorExitCode = 3;

    private static readonly DiagnosticInfo[] WarningLevel1 = {
        new DiagnosticInfo(1, "BU"),
        new DiagnosticInfo(26, "BU"),
    };

    private static readonly DiagnosticInfo[] WarningLevel2 = {
        new DiagnosticInfo(2, "BU"),
    };

    /// <summary>
    /// Processes/decodes command-line arguments, and invokes <see cref="Compiler" />.
    /// </summary>
    /// <param name="args">Command-line arguments from Main.</param>
    /// <returns>Error code, 0 = success.</returns>
    public static int ProcessArgs(string[] args) {
        int err;

        var processName = Process.GetCurrentProcess().ProcessName;
        var state = DecodeOptions(args, out var diagnostics, out var dialogs, out var multipleExplains);

        var compiler = new Compiler(state) {
            me = processName
        };

        var hasDialog = dialogs.machine || dialogs.version || dialogs.help || dialogs.error is not null;

        if (multipleExplains)
            ResolveDiagnostic(Belte.Diagnostics.Error.MultipleExplains(), processName, state);

        if (hasDialog) {
            diagnostics.Clear();
            diagnostics.Move(ShowDialogs(dialogs, multipleExplains));
            ResolveDiagnostics(diagnostics, processName, state);

            return SuccessExitCode;
        }

        if (state.verboseMode && !state.noOut)
            ShowDialogs(new ShowDialogs() { machine = true, version = true }, false);

        // Only mode that does not go through one-time compilation
        if (state.buildMode == BuildMode.Repl) {
            ResolveDiagnostics(diagnostics, processName, state);

            if (!state.noOut) {
                using var repl = new BelteRepl(compiler, ResolveDiagnostics);
                repl.Run();
            }

            return SuccessExitCode;
        }

        err = ResolveDiagnostics(diagnostics, processName, state);

        if (err > 0)
            return err;

        if (!state.noOut)
            CleanOutputFiles(compiler);

        ReadInputFiles(compiler, out diagnostics);

        err = ResolveDiagnostics(diagnostics, processName, state);

        if (err > 0)
            return err;

        if (state.verboseMode && !state.noOut)
            LogTasks(state.tasks);

        compiler.Compile();

        err = ResolveDiagnostics(compiler);

        if (err > 0)
            return err;

        if (compiler.exceptions.Count > 0) {
            foreach (var exception in compiler.exceptions)
                DiagnosticFormatter.PrettyPrintException(exception);

            return RuntimeErrorExitCode;
        }

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

        if (dialogs.error is not null && !multipleExplains)
            ShowErrorHelp(dialogs.error, out diagnostics);

        return diagnostics;
    }

    private static void ShowErrorHelp(string _, out DiagnosticQueue<Diagnostic> diagnostics) {
        diagnostics = new DiagnosticQueue<Diagnostic>();
        diagnostics.Push(new Diagnostic(DiagnosticSeverity.Error, "--explain is not implemented"));
        return;

        // string prefix;

        // if (error.Length < 3 || (char.IsDigit(error[0]) && char.IsDigit(error[1]))) {
        //     prefix = "BU";
        //     error = prefix + error;
        // } else {
        //     prefix = error.Substring(0, 2);
        // }

        // diagnostics = new DiagnosticQueue<Diagnostic>();
        // var errorCode = 0;

        // try {
        //     errorCode = Convert.ToInt32(error.Substring(2));
        // } catch (Exception e) when (e is FormatException || e is OverflowException) {
        //     diagnostics.Push(Belte.Diagnostics.Error.InvalidErrorCode(error));
        //     return;
        // }

        // string allDescriptions = null;

        // foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
        //     var foundDescriptions = assembly.GetManifestResourceNames()
        //         .Where(r => r.EndsWith($"Resources.ErrorDescriptions{prefix}.txt"));

        //     if (foundDescriptions.Any()) {
        //         using var stream = assembly.GetManifestResourceStream(foundDescriptions.First());
        //         using var reader = new StreamReader(stream);
        //         allDescriptions = reader.ReadToEnd();
        //         break;
        //     }
        // }

        // if (allDescriptions is null) {
        //     diagnostics.Push(Belte.Diagnostics.Error.InvalidErrorCode(error));
        //     return;
        // }

        // var messages = new Dictionary<int, string>();

        // foreach (var message in allDescriptions.Split($"${prefix}")) {
        //     try {
        //         var code = message.Substring(0, 4);
        //         messages[Convert.ToInt32(code)] = message.Substring(4);
        //     } catch (ArgumentOutOfRangeException) { }
        // }

        // if (!messages.ContainsKey(errorCode)) {
        //     diagnostics.Push(Belte.Diagnostics.Error.UnusedErrorCode(error));
        //     return;
        // }

        // var foundMessage = messages[errorCode].Substring(2);

        // if (foundMessage.EndsWith(Environment.NewLine))
        //     foundMessage = foundMessage.Substring(0, foundMessage.Length - 1);

        // var lines = foundMessage.Split(Environment.NewLine);
        // var count = 0;

        // while (count < lines.Length) {
        //     // First -1 is required, second -1 is because we are printing -- More --
        //     // -2 is to account for the next terminal input line
        //     if (count > Console.WindowHeight - 1 - 1 - 2) {
        //         var key = ' ';

        //         do {
        //             Console.Write("-- More --");
        //             key = Console.ReadKey().KeyChar;
        //             var currentLineCursor = Console.CursorTop;
        //             Console.SetCursorPosition(0, Console.CursorTop);
        //             // * Does not need -1 in some terminals
        //             // Unfortunately the program cant tell what terminal is being used
        //             Console.Write(new string(' ', Console.WindowWidth - 1));
        //             Console.SetCursorPosition(0, currentLineCursor);
        //         } while (key != '\n' && key != '\r');
        //     }

        //     var line = lines[count++];
        //     Console.WriteLine(line);
        // }
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

    private static DiagnosticSeverity ResolveDiagnostic<Type>(
        Type diagnostic,
        string me,
        CompilerState state,
        ConsoleColor? textColor = null)
        where Type : Diagnostic {
        var previous = Console.ForegroundColor;

        void ResetColor() {
            if (textColor is not null)
                Console.ForegroundColor = textColor.Value;
            else
                Console.ResetColor();
        }

        var severity = diagnostic.info.severity;
        ResetColor();

        var ignoreDiagnostic = (int)state.severity > (int)severity;
        ignoreDiagnostic |= CheckWarningLevel(diagnostic.info, state);
        ignoreDiagnostic &= !WarningIncluded(diagnostic.info, state);
        ignoreDiagnostic |= WarningExcluded(diagnostic.info, state);

        if (ignoreDiagnostic) {
        } else if (diagnostic.info.module != "BU" || (diagnostic is BelteDiagnostic bd && bd.location is null)) {
            Console.Write($"{me}: ");

            switch (severity) {
                case DiagnosticSeverity.Debug:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write("debug");
                    break;
                case DiagnosticSeverity.Info:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("info");
                    break;
                case DiagnosticSeverity.Warning:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write("warning");
                    break;
                case DiagnosticSeverity.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("error");
                    break;
                case DiagnosticSeverity.Fatal:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("fatal");
                    break;
            }

            Console.Write(
                $"{(diagnostic.info.code is null && diagnostic.info.module is null ? "" : $" {diagnostic.info}")}: "
            );

            ResetColor();
            Console.WriteLine(diagnostic.message);
        } else {
            DiagnosticFormatter.PrettyPrint(diagnostic as BelteDiagnostic, textColor);
        }

        Console.ForegroundColor = previous;

        return severity;
    }

    private static bool CheckWarningLevel(DiagnosticInfo info, CompilerState state) {
        if (info.severity != DiagnosticSeverity.Warning)
            return false;

        if (state.warningLevel == 0)
            return true;
        else if (state.warningLevel == 1)
            return !WarningInWarningList(WarningLevel1, info);
        else if (state.warningLevel == 2)
            return false;

        throw new UnreachableException();
    }

    private static bool WarningIncluded(DiagnosticInfo info, CompilerState state) {
        if (info.severity != DiagnosticSeverity.Warning)
            return false;

        return WarningInWarningList(state.includeWarnings, info);
    }

    private static bool WarningExcluded(DiagnosticInfo info, CompilerState state) {
        if (info.severity != DiagnosticSeverity.Warning)
            return false;

        return WarningInWarningList(state.excludeWarnings, info);
    }

    private static bool WarningInWarningList(DiagnosticInfo[] warnings, DiagnosticInfo info) {
        foreach (var warning in warnings) {
            if (warning.ToString() == info.ToString())
                return true;
        }

        return false;
    }

    private static int ResolveDiagnostics<Type>(
        DiagnosticQueue<Type> diagnostics,
        string me,
        CompilerState state,
        ConsoleColor? textColor = null)
        where Type : Diagnostic {
        if (diagnostics.Count == 0)
            return SuccessExitCode;

        var worst = diagnostics.ToList().Select(d => (int)d.info.severity).Max();
        var diagnostic = diagnostics.Pop();

        while (diagnostic is not null) {
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
        Compiler compiler,
        string me = null,
        ConsoleColor textColor = ConsoleColor.White) {
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

    private static void LogTasks(FileState[] tasks) {
        Console.WriteLine($"File Tasks ({tasks.Length}):");

        foreach (var task in tasks) {
            Console.Write("    ");
            Console.WriteLine(
                $"{task.inputFileName}{(task.outputFilename is null ? "" : $" -> {task.outputFilename}")}: {task.stage}"
            );
        }

        Console.WriteLine();
    }

    private static CompilerState DecodeOptions(
        string[] args,
        out DiagnosticQueue<Diagnostic> diagnostics,
        out ShowDialogs dialogs,
        out bool multipleExplains) {
        var state = new CompilerState();
        var tasks = new List<FileState>();
        var references = new List<string>();
        var diagnosticsCL = new DiagnosticQueue<Diagnostic>();
        diagnostics = new DiagnosticQueue<Diagnostic>();
        var arguments = Array.Empty<string>();
        var includeWarnings = new List<DiagnosticInfo>();
        var excludeWarnings = new List<DiagnosticInfo>();

        var specifyStage = false;
        var specifyOut = false;
        var specifyModule = false;
        var specifyBuildMode = false;
        var specifyWarningLevel = false;

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
        state.moduleName = "a";
        state.noOut = false;
        state.warningLevel = 1;
        state.severity = DiagnosticSeverity.Warning;
        state.projectType = OutputKind.ConsoleApplication;
        state.verboseMode = false;

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
                    specifyBuildMode = true;
                    state.buildMode = BuildMode.Repl;
                    break;
                case "-n":
                    specifyBuildMode = true;
                    state.buildMode = BuildMode.Independent;
                    break;
                case "-i":
                    specifyBuildMode = true;
                    state.buildMode = BuildMode.AutoRun;
                    break;
                case "--script":
                    specifyBuildMode = true;
                    state.buildMode = BuildMode.Interpret;
                    break;
                case "--evaluate":
                    specifyBuildMode = true;
                    state.buildMode = BuildMode.Evaluate;
                    break;
                case "--execute":
                    specifyBuildMode = true;
                    state.buildMode = BuildMode.Execute;
                    break;
                case "-t":
                case "--transpile":
                    specifyBuildMode = true;
                    state.buildMode = BuildMode.CSharpTranspile;
                    break;
                case "-d":
                case "--dotnet":
                    specifyBuildMode = true;
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
                case "--verbose":
                    state.verboseMode = true;
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
                if (tempDialogs.error is not null) {
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
                    state.severity = severityLevel;
                else
                    diagnostics.Push(Belte.Diagnostics.Error.UnrecognizedSeverity(severityString));
            } else if (arg.StartsWith("--warnlevel")) {
                if (arg == "--warnlevel" || arg == "--warnlevel=") {
                    diagnostics.Push(Belte.Diagnostics.Error.MissingWarningLevel(arg));
                    continue;
                }

                var warningString = arg.Substring(12);

                if (int.TryParse(warningString, out var warningLevel) && 0 <= warningLevel && warningLevel <= 2) {
                    specifyWarningLevel = true;
                    state.warningLevel = warningLevel;
                } else {
                    diagnostics.Push(Belte.Diagnostics.Error.InvalidWarningLevel(warningString));
                }
            } else if (arg.StartsWith("--wignore")) {
                if (arg == "--wignore" || arg == "--wignore=") {
                    diagnostics.Push(Belte.Diagnostics.Error.MissingWIgnoreCode(arg));
                    continue;
                }

                excludeWarnings.AddRange(ParseAndVerifyWarningCodes(arg.Substring(10), diagnosticsCL));
            } else if (arg.StartsWith("--winclude")) {
                if (arg == "--winclude" || arg == "--winclude=") {
                    diagnostics.Push(Belte.Diagnostics.Error.MissingWIncludeCode(arg));
                    continue;
                }

                includeWarnings.AddRange(ParseAndVerifyWarningCodes(arg.Substring(11), diagnosticsCL));
            } else if (arg.StartsWith("--type")) {
                if (arg == "--type" || arg == "--type=") {
                    diagnostics.Push(Belte.Diagnostics.Error.MissingType(arg));
                    continue;
                }

                var type = arg.Substring(7).ToLower();

                if (type == "console")
                    state.projectType = OutputKind.ConsoleApplication;
                else if (type == "graphics")
                    state.projectType = OutputKind.GraphicsApplication;
                else if (type == "dll")
                    state.projectType = OutputKind.DynamicallyLinkedLibrary;
                else
                    diagnostics.Push(Belte.Diagnostics.Error.UnrecognizedType(type));
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

        if (dialogs.machine || dialogs.help || dialogs.version || dialogs.error is not null)
            return state;

        state.tasks = tasks.ToArray();
        state.references = references.ToArray();
        state.arguments = arguments;
        state.includeWarnings = includeWarnings.ToArray();
        state.excludeWarnings = excludeWarnings.ToArray();

        if (state.projectType == OutputKind.DynamicallyLinkedLibrary) {
            if (!specifyBuildMode)
                state.buildMode = BuildMode.Dotnet;

            if (state.buildMode != BuildMode.Dotnet)
                diagnostics.Push(Belte.Diagnostics.Fatal.DLLWithWrongBuildMode());

            if (!specifyOut)
                state.outputFilename = "a.dll";
        }

        if (!specifyWarningLevel &&
            state.buildMode is BuildMode.AutoRun or BuildMode.Interpret or BuildMode.Evaluate or BuildMode.Execute) {
            state.warningLevel = 0;
        }

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

        if (references.Count > 0 && state.buildMode != BuildMode.Dotnet)
            diagnostics.Push(Belte.Diagnostics.Fatal.CannotSpecifyReferencesWithoutDotnet());

        if (state.tasks.Length == 0 && !(state.buildMode == BuildMode.Repl))
            diagnostics.Push(Belte.Diagnostics.Fatal.NoInputFiles());

        if (state.projectType == OutputKind.DynamicallyLinkedLibrary) {
            if (specifyOut && specifyModule)
                diagnostics.Push(Belte.Diagnostics.Fatal.CannotSpecifyOutAndModuleWithDll());
            else if (!specifyOut)
                state.outputFilename = state.moduleName + ".dll";
            else if (!specifyModule)
                state.moduleName = Path.GetFileNameWithoutExtension(state.outputFilename);
        }

        state.outputFilename = state.outputFilename.Trim();

        if (state.verboseMode)
            state.severity = DiagnosticSeverity.All;

        return state;
    }

    private static List<DiagnosticInfo> ParseAndVerifyWarningCodes(
        string codesString,
        DiagnosticQueue<Diagnostic> diagnostics) {
        var codes = codesString.Split(',');
        var infos = new List<DiagnosticInfo>();

        foreach (var code in codes) {
            var invalid = false;
            var prefix = "";
            var codeNumber = 0;

            if (code.Length < 3) {
                invalid = true;
            } else {
                prefix = code.Substring(0, 2);

                if (prefix != "BU" && prefix != "RE" && prefix != "CL")
                    invalid = true;
            }

            if (!invalid) {
                if (!int.TryParse(code.Substring(2), out codeNumber))
                    invalid = true;
            }

            if (invalid) {
                diagnostics.Push(Belte.Diagnostics.Error.InvalidErrorCode(code));
                continue;
            }

            var enumPrefix = "";

            if (prefix == "BU")
                enumPrefix = Enum.GetName(typeof(DiagnosticCode), codeNumber);
            else if (prefix == "CL")
                enumPrefix = Enum.GetName(typeof(Belte.Diagnostics.DiagnosticCode), codeNumber);
            else if (prefix == "RE")
                enumPrefix = Enum.GetName(typeof(Repl.Diagnostics.DiagnosticCode), codeNumber);

            if (enumPrefix is null) {
                diagnostics.Push(Belte.Diagnostics.Error.UnusedErrorCode(code));
                continue;
            }

            if (!enumPrefix.StartsWith("WRN")) {
                diagnostics.Push(Belte.Diagnostics.Error.CodeIsNotWarning(code));
                continue;
            }

            infos.Add(new DiagnosticInfo(codeNumber, prefix));
        }

        return infos;
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

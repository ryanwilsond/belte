using System.IO;
using System.Linq;
using System.Collections.Generic;
using Buckle;
using Buckle.Diagnostics;

namespace Belte.CmdLine;

public static partial class CommandLine {
    private static CompilerState DecodeOptions(
        string[] args, out DiagnosticQueue diagnostics, out bool showHelp,
        out bool showMachine, out bool showVersion) {
        CompilerState state = new CompilerState();
        List<FileState> tasks = new List<FileState>();
        List<string> references = new List<string>();
        List<string> options = new List<string>();
        diagnostics = new DiagnosticQueue();

        bool specifyStage = false;
        bool specifyOut = false;
        bool specifyModule = false;
        showHelp = false;
        showMachine = false;
        showVersion = false;
        state.buildMode = BuildMode.Independent;
        state.finishStage = CompilerStage.Linked;
        state.outputFilename = "a.exe";
        state.moduleName = "a";

        for (int i = 0; i < args.Length; i++) {
            string arg = args[i];

            if (arg.StartsWith('-')) {
                if (arg.StartsWith("-o")) {
                    specifyOut = true;

                    if (arg == "-o") {
                        if (i >= args.Length - 1)
                            diagnostics.Push(DiagnosticType.Error, "missing filename after '-o'");
                        else
                            state.outputFilename = args[++i];
                    } else {
                        state.outputFilename = arg.Substring(2);
                    }
                } else if (arg.StartsWith("--modulename")) {
                    if (arg == "--modulename" || arg == "--modulename=") {
                        diagnostics.Push(
                            DiagnosticType.Error, $"missing name after '{arg}' (usage: '--modulename=<name>')");
                    } else {
                        specifyModule = true;
                        state.moduleName = arg.Substring(13);
                    }
                } else if (arg.StartsWith("--ref")) {
                    if (arg == "--ref" || arg == "--ref=")
                        diagnostics.Push(DiagnosticType.Error, $"missing name after '{arg}' (usage: '--ref=<name>')");
                    else
                        references.Add(arg.Substring(6));
                } else if (arg.StartsWith("--entry")) {
                    if (arg == "--entry" || arg == "--entry=") {
                        diagnostics.Push(
                            DiagnosticType.Error, $"missing symbol after '{arg}' (usage: '--entry=<symbol>')");
                    } else {
                        state.entryPoint = arg.Substring(8);
                    }
                } else if (arg.StartsWith("-W")) {
                    if (arg.Length == 2) {
                        diagnostics.Push(DiagnosticType.Error, "must specify option after '-W' (usage: '-W<options>'");
                    } else {
                        string[] wArgs = arg.Substring(2).Split(',');

                        foreach (string wArg in wArgs) {
                            if (!AllowedOptions.Contains(wArg))
                                diagnostics.Push(DiagnosticType.Error, $"unrecognized option '{wArg}'");
                            else
                                options.Add(wArg);
                        }
                    }
                } else {
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
                            state.buildMode = BuildMode.Interpreter;
                            break;
                        case "-d":
                            state.buildMode = BuildMode.Dotnet;
                            break;
                        case "-h":
                        case "--help":
                            showHelp = true;
                            break;
                        case "--dumpmachine":
                            showMachine = true;
                            break;
                        case "--version":
                            showVersion = true;
                            break;
                        default:
                            diagnostics.Push(DiagnosticType.Error, $"unrecognized command line option '{arg}'");
                            break;
                    }
                }
            } else {
                diagnostics.Move(ResolveInputFileOrDir(arg, ref tasks));
            }
        }

        if (showMachine || showHelp || showVersion)
            return state;

        state.tasks = tasks.ToArray();
        state.references = references.ToArray();
        state.options = options.ToArray();

        if (specifyOut) {
            string[] parts = state.outputFilename.Split('.');
            state.moduleName = string.Join('.', parts[0..(parts.Length-2)]);
        }

        if (args.Length > 1 && state.buildMode == BuildMode.Repl)
            diagnostics.Push(DiagnosticType.Warning, "all arguments are ignored when invoking the repl");

        if (specifyStage && state.buildMode == BuildMode.Dotnet)
            diagnostics.Push(DiagnosticType.Fatal, "cannot specify '-p', '-s', or '-c' with .NET integration");

        if (specifyOut && specifyStage && state.tasks.Length > 1 && !(state.buildMode == BuildMode.Dotnet))
            diagnostics.Push(
                DiagnosticType.Fatal, "cannot specify output file with '-p', '-s', or '-c' with multiple files");

        if ((specifyStage || specifyOut) && state.buildMode == BuildMode.Interpreter)
            diagnostics.Push(
                DiagnosticType.Fatal, "cannot specify outfile or use '-p', '-s', or '-c' with interpreter");

        if (specifyModule && state.buildMode != BuildMode.Dotnet)
            diagnostics.Push(DiagnosticType.Fatal, "cannot specify module name without .NET integration");

        if (references.Count != 0 && state.buildMode != BuildMode.Dotnet)
            diagnostics.Push(DiagnosticType.Fatal, "cannot specify references without .NET integration");

        if (state.tasks.Length == 0 && !(state.buildMode == BuildMode.Repl))
            diagnostics.Push(DiagnosticType.Fatal, "no input files");

        return state;
    }

    private static DiagnosticQueue ResolveInputFileOrDir(string name, ref List<FileState> tasks) {
        List<string> filenames = new List<string>();
        DiagnosticQueue diagnostics = new DiagnosticQueue();

        if (Directory.Exists(name)) {
            filenames.AddRange(Directory.GetFiles(name));
        } else if (File.Exists(name)) {
            filenames.Add(name);
        } else {
            diagnostics.Push(DiagnosticType.Error, $"{name}: no such file or directory");
            return diagnostics;
        }

        foreach (string filename in filenames) {
            FileState task = new FileState();
            task.inputFilename = filename;

            string[] parts = task.inputFilename.Split('.');
            string type = parts[parts.Length - 1];

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
                    diagnostics.Push(
                        DiagnosticType.Warning, $"unknown file type of input file '{task.inputFilename}'; ignoring");
                    break;
            }

            tasks.Add(task);
        }

        return diagnostics;
    }
}

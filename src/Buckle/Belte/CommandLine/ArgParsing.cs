using System.IO;
using System.Linq;
using System.Collections.Generic;
using Buckle;
using Diagnostics;
using System;

namespace Belte.CommandLine;

/// <summary>
/// Flags that tell command line what dialogs to display.
/// </summary>
public struct ShowDialogs {
    public bool help;
    public bool machine;
    public bool version;
    public string error;
}

public static partial class BuckleCommandLine {
    private static CompilerState DecodeOptions(
        string[] args, out DiagnosticQueue<Diagnostic> diagnostics, out ShowDialogs dialogs) {
        CompilerState state = new CompilerState();
        List<FileState> tasks = new List<FileState>();
        List<string> references = new List<string>();
        List<string> options = new List<string>();
        DiagnosticQueue<Diagnostic> diagnosticsCL = new DiagnosticQueue<Diagnostic>();
        diagnostics = new DiagnosticQueue<Diagnostic>();

        bool specifyStage = false;
        bool specifyOut = false;
        bool specifyModule = false;

        ShowDialogs tempDialogs = new ShowDialogs();

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
                    state.buildMode = BuildMode.Interpreter;
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
            string arg = args[i];

            if (arg.StartsWith('-')) {
                if (arg.StartsWith("-o")) {
                    specifyOut = true;

                    if (arg == "-o") {
                        if (i >= args.Length - 1)
                            diagnostics.Push(Belte.Diagnostics.Error.MissingFilenameO());
                        else
                            state.outputFilename = args[++i];
                    } else {
                        state.outputFilename = arg.Substring(2);
                    }
                } else if (arg.StartsWith("--explain")) {
                    if (tempDialogs.error != null) {
                        diagnostics.Push(Belte.Diagnostics.Error.MultipleExplains());
                        continue;
                    }

                    if (arg == "--explain") {
                        if (i >= args.Length - 1) {
                            diagnostics.Push(Belte.Diagnostics.Error.MissingCodeExplain());
                        } else {
                            i++;
                            tempDialogs.error = args[i];
                        }
                    } else {
                        var errorCode = args[i].Substring(9);
                        tempDialogs.error = errorCode;
                    }
                } else if (arg.StartsWith("--modulename")) {
                    if (arg == "--modulename" || arg == "--modulename=") {
                        diagnostics.Push(Belte.Diagnostics.Error.MissingModuleName(arg));
                    } else {
                        specifyModule = true;
                        state.moduleName = arg.Substring(13);
                    }
                } else if (arg.StartsWith("--ref")) {
                    if (arg == "--ref" || arg == "--ref=")
                        diagnostics.Push(Belte.Diagnostics.Error.MissingReference(arg));
                    else
                        references.Add(arg.Substring(6));
                } else if (arg.StartsWith("--entry")) {
                    if (arg == "--entry" || arg == "--entry=") {
                        diagnostics.Push(Belte.Diagnostics.Error.MissingEntrySymbol(arg));
                    } else {
                        state.entryPoint = arg.Substring(8);
                    }
                } else if (arg.StartsWith("-W")) {
                    if (arg.Length == 2) {
                        diagnostics.Push(Belte.Diagnostics.Error.NoOptionAfterW());
                    } else {
                        string[] wArgs = arg.Substring(2).Split(',');

                        foreach (string wArg in wArgs) {
                            if (!AllowedOptions.Contains(wArg))
                                diagnostics.Push(Belte.Diagnostics.Error.UnrecognizedWOption(wArg));
                            else
                                options.Add(wArg);
                        }
                    }
                } else {
                    DecodeSimpleOption(arg);
                }
            } else {
                diagnostics.Move(ResolveInputFileOrDir(arg, ref tasks));
            }
        }

        dialogs = tempDialogs;
        diagnostics.Move(diagnosticsCL);

        if (dialogs.machine || dialogs.help || dialogs.version || dialogs.error != null)
            return state;

        if (state.buildMode == BuildMode.Dotnet) {
            references.AddRange(new string[] {
                "C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/3.1.0/ref/netcoreapp3.1/System.Console.dll",
                "C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/3.1.0/ref/netcoreapp3.1/System.Runtime.dll",
                "C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/3.1.0/ref/netcoreapp3.1/System.Runtime.Extensions.dll"
            });
        }

        state.tasks = tasks.ToArray();
        state.references = references.ToArray();
        state.options = options.ToArray();

        if (specifyOut) {
            string[] parts = state.outputFilename.Split('.');
            // ? Not sure if there are consequences of making moduleName default to 'a'
            // state.moduleName = string.Join('.', parts[0..(parts.Length-2)]);
        }

        if (args.Length > 1 && state.buildMode == BuildMode.Repl)
            diagnostics.Push(Belte.Diagnostics.Warning.ReplInvokeIgnore());

        if (specifyStage && state.buildMode == BuildMode.Dotnet)
            diagnostics.Push(Belte.Diagnostics.Error.CannotSpecifyWithDotnet());

        if (specifyOut && specifyStage && state.tasks.Length > 1 && !(state.buildMode == BuildMode.Dotnet))
            diagnostics.Push(Belte.Diagnostics.Error.CannotSpecifyWithMultipleFiles());

        if ((specifyStage || specifyOut) && state.buildMode == BuildMode.Interpreter)
            diagnostics.Push(Belte.Diagnostics.Error.CannotSpecifyWithInterpreter());

        if (specifyModule && state.buildMode != BuildMode.Dotnet)
            diagnostics.Push(Belte.Diagnostics.Error.CannotSpecifyModuleNameWithDotnet());

        if (references.Count != 0 && state.buildMode != BuildMode.Dotnet)
            diagnostics.Push(Belte.Diagnostics.Error.CannotSpecifyReferencesWithDotnet());

        if (state.tasks.Length == 0 && !(state.buildMode == BuildMode.Repl))
            diagnostics.Push(Belte.Diagnostics.Error.NoInputFiles());

        state.outputFilename = state.outputFilename.Trim();

        return state;
    }

    private static DiagnosticQueue<Diagnostic> ResolveInputFileOrDir(string name, ref List<FileState> tasks) {
        List<string> filenames = new List<string>();
        DiagnosticQueue<Diagnostic> diagnostics = new DiagnosticQueue<Diagnostic>();

        if (Directory.Exists(name)) {
            filenames.AddRange(Directory.GetFiles(name));
        } else if (File.Exists(name)) {
            filenames.Add(name);
        } else {
            diagnostics.Push(Belte.Diagnostics.Error.NoSuchFileOrDirectory(name));
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
                    diagnostics.Push(Belte.Diagnostics.Warning.IgnoringUnknownFileType(task.inputFilename));
                    continue;
            }

            tasks.Add(task);
        }

        return diagnostics;
    }
}

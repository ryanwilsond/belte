using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using Buckle;
using Buckle.Building;
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

    private const long MaxBuildCacheSize = 3 * 1_000_000_000L;

    private static readonly DiagnosticInfo[] WarningLevel1 = [
        new DiagnosticInfo(0001, "BU"),
        new DiagnosticInfo(0026, "BU"),
        new DiagnosticInfo(0133, "BU"),
        new DiagnosticInfo(0180, "BU"),
        new DiagnosticInfo(0239, "BU"),
        new DiagnosticInfo(0243, "BU"),
        new DiagnosticInfo(0244, "BU"),
        new DiagnosticInfo(0247, "BU"),
        new DiagnosticInfo(0248, "BU"),
        new DiagnosticInfo(0252, "BU"),
        new DiagnosticInfo(0272, "BU"),
        new DiagnosticInfo(0273, "BU"),
        new DiagnosticInfo(0274, "BU"),
        new DiagnosticInfo(0276, "BU"),
        new DiagnosticInfo(0277, "BU"),
        new DiagnosticInfo(0286, "BU"),
        new DiagnosticInfo(0287, "BU"),
        new DiagnosticInfo(0288, "BU"),
        new DiagnosticInfo(0289, "BU"),
        new DiagnosticInfo(0290, "BU"),
        new DiagnosticInfo(0321, "BU"),
        new DiagnosticInfo(0425, "BU"),
    ];

    private static readonly DiagnosticInfo[] WarningLevel2 = [
        new DiagnosticInfo(0053, "BU"),
        new DiagnosticInfo(0198, "BU"),
        new DiagnosticInfo(0263, "BU"),
        new DiagnosticInfo(0264, "BU"),
        new DiagnosticInfo(0265, "BU"),
        new DiagnosticInfo(0416, "BU"),
        new DiagnosticInfo(0041, "CL"),
        new DiagnosticInfo(0447, "BU"),
    ];

    private static readonly DiagnosticInfo[] WarningLevel3 = [
        new DiagnosticInfo(0002, "BU"),
    ];

    /// <summary>
    /// Processes/decodes command-line arguments, and invokes <see cref="Compiler" />.
    /// </summary>
    /// <param name="args">Command-line arguments from Main.</param>
    /// <returns>Error code, 0 = success.</returns>
    public static int ProcessArgs(string[] args) {
        int err;

        var processName = Process.GetCurrentProcess().ProcessName;

        if (args.Length > 0) {
            switch (args[0]) {
                case "new":
                    return ProcessNewArgs(processName, args);
                case "build":
                    return ProcessBuildArgs(processName, args);
                case "run":
                    return ProcessRunArgs(processName, args);
            }
        }

        var state = DecodeOptions(
            args,
            out var diagnostics,
            out var dialogs,
            out var multipleExplains,
            out var sae,
            out var pendingReferenceCopies
        );

        var compiler = new Compiler(state) {
            me = processName
        };

        var hasDialog = dialogs.machine ||
                        dialogs.version ||
                        dialogs.help ||
                        dialogs.error is not null ||
                        dialogs.clearCache;

        if (multipleExplains)
            ResolveDiagnostic(Belte.Diagnostics.Error.MultipleExplains(), processName, state);

        if (dialogs.clearSubmissions)
            ShowClearSubmissionsDialog();

        if (hasDialog) {
            diagnostics.Clear();
            diagnostics.Move(ShowDialogs(dialogs, multipleExplains));
            ResolveDiagnostics(diagnostics, processName, state);

            ResolveSae(sae);
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

            ResolveSae(sae);
            return SuccessExitCode;
        }

        err = ResolveDiagnostics(diagnostics, processName, state);

        if (err > 0) {
            ResolveSae(sae);
            return err;
        }

        if (state.tasks.Length == 0 && dialogs.clearSubmissions) {
            ResolveSae(sae);
            return SuccessExitCode;
        }

        if (!state.noOut)
            CleanOutputFiles(compiler, diagnostics);

        ReadInputFiles(compiler, diagnostics);

        err = ResolveDiagnostics(diagnostics, processName, state);

        if (err > 0) {
            ResolveSae(sae);
            return err;
        }

        if (state.verboseMode && !state.noOut)
            LogCompilerState(state, pendingReferenceCopies);

        compiler.Compile();

        err = ResolveDiagnostics(compiler);

        if (err > 0) {
            ResolveSae(sae);
            return err;
        }

        if (compiler.exceptions.Count > 0) {
            foreach (var exception in compiler.exceptions)
                DiagnosticFormatter.PrettyPrintException(exception);

            ResolveSae(sae);
            return RuntimeErrorExitCode;
        }

        ResolveReferenceCopies(state.outputFilename, pendingReferenceCopies, processName, state);

        ResolveSae(sae);
        return SuccessExitCode;
    }

    private static int ProcessNewArgs(string processName, string[] args) {
        int err;

        var name = DecodeNewOptions(args, out var diagnostics, out var outputKind);
        const string BuildScriptName = "Build.blt";
        const string SrcName = "src";
        const string ProgramName = "Program.blt";

        // We don't compile anything but we still need diagnostic reporting rules
        var state = new CompilerState {
            diagnosticOptions = new TaskDiagnosticOptions() {
                warningLevel = 1,
                severity = DiagnosticSeverity.Warning,
            },
            time = false,
        };

        if (File.Exists(BuildScriptName))
            diagnostics.Push(Belte.Diagnostics.Error.CannotCreateNew(BuildScriptName));

        if (Directory.Exists(SrcName))
            diagnostics.Push(Belte.Diagnostics.Error.CannotCreateNew(SrcName));

        err = ResolveDiagnostics(diagnostics, processName, state);

        if (err > 0)
            return err;

        var isGraphics = outputKind == OutputKind.GraphicsApplication;
        var buildMode = isGraphics ? BuildMode.Execute : BuildMode.Dotnet;

        var buildScriptContent =
@$"using Buckle;
using Buckle.Building;

void Build(Builder builder) {{
    builder.AddInput(""src"");{(isGraphics ? "" : $"\n    builder.SetOutput(\"bin/{name}\");")}
    builder.buildMode = BuildMode.{buildMode};
    builder.outputKind = OutputKind.{outputKind};
    // builder.AddRef(""lib"", RefOptions.Copy);
}}
";

        string programContent;

        switch (outputKind) {
            case OutputKind.ConsoleApplication:
                programContent =
@$"
namespace {name};
static class Program;

void Main(string[]! args) {{
    Console.PrintLine(""Hello, world!"");
}}
";

                break;
            case OutputKind.GraphicsApplication:
                programContent =
@$"
namespace {name};
class Program;

void Main(string[]! args) {{
    Graphics.Initialize(""{name}"", 1280, 720, false);
}}

void Update(decimal deltaTime) {{
    Graphics.Fill(0, 0, 0);
}}
";

                break;
            case OutputKind.DynamicallyLinkedLibrary:
                programContent =
@$"
namespace {name};

public class {name} {{

}}
";

                break;
            default:
                throw new UnreachableException();
        }

        File.WriteAllText(BuildScriptName, buildScriptContent);
        Directory.CreateDirectory(SrcName);
        File.WriteAllText(Path.Combine(SrcName, ProgramName), programContent);

        if (!isGraphics && !Directory.Exists("bin"))
            Directory.CreateDirectory("bin");

        if (!Directory.Exists("lib"))
            Directory.CreateDirectory("lib");

        return SuccessExitCode;
    }

    private static int ProcessBuildArgs(string processName, string[] args) {
        return ProcessBuildArgs(processName, args, out _);
    }

    private static int ProcessBuildArgs(string processName, string[] args, out CompilerState state) {
        int err;

        var buildState = DecodeBuildOptions(args, out var diagnostics, out var arguments, out var debugMode);
        state = new CompilerState {
            noOut = false,
            diagnosticOptions = new TaskDiagnosticOptions() {
                warningLevel = 1,
                severity = DiagnosticSeverity.Warning,
            },
            verboseMode = buildState.showInfo,
            reducedVerboseMode = buildState.showInfo,
            time = buildState.showTime,
            debugMode = false,
            concurrentBuild = false,
        };

        var inputFileName = buildState.buildScript;

        if (!File.Exists(inputFileName)) {
            diagnostics.Push(Belte.Diagnostics.Error.NoSuchFileOrDirectory(inputFileName));
        } else {
            var opened = false;

            for (var j = 1; j < 4; j++) {
                try {
                    buildState.buildScriptText = File.ReadAllText(inputFileName);
                    opened = true;
                    break;
                } catch (IOException) {
                    if (j < 3)
                        Thread.Sleep(j * 10);
                }
            }

            if (!opened)
                diagnostics.Push(Belte.Diagnostics.Error.UnableToOpenFile(inputFileName));
        }

        err = ResolveDiagnostics(diagnostics, processName, state);

        if (err > 0)
            return err;

        buildState.buildDirectory = GetBuildDirectory();
        var hash = HashStrings(GetVersionString(), buildState.buildScriptText);
        hash.Append(HashVersion(BuildInfo.APIVersion));
        buildState.buildHash = hash.GetCurrentHashAsUInt64();

        if (buildState.showInfo)
            LogBuildState(buildState);

        err = GetOrCreateBuildScript(processName, buildState, diagnostics, out var compiler, out var builder);

        if (err > 0)
            return err;

        err = ResolveDiagnostics(diagnostics, processName, state);

        if (err > 0)
            return err;

        compiler.state = ToCompilerState(diagnostics, builder, out var pendingReferenceCopies);
        state = compiler.state;
        state.arguments = arguments;
        state.debugMode |= debugMode;

        if (state.verboseMode && !state.noOut) {
            ShowMachineDialog();
            ShowVersionDialog();
        }

        err = ResolveDiagnostics(diagnostics, processName, state);

        if (err > 0)
            return err;

        if (!state.noOut)
            CleanOutputFiles(compiler, diagnostics);

        ReadInputFiles(compiler, diagnostics);

        err = ResolveDiagnostics(diagnostics, processName, state);

        if (err > 0)
            return err;

        if (state.verboseMode && !state.noOut)
            LogCompilerState(state, pendingReferenceCopies);

        compiler.Compile();

        err = ResolveDiagnostics(compiler);

        if (err > 0)
            return err;

        if (compiler.exceptions.Count > 0) {
            foreach (var exception in compiler.exceptions)
                DiagnosticFormatter.PrettyPrintException(exception);

            return RuntimeErrorExitCode;
        }

        ResolveReferenceCopies(state.outputFilename, pendingReferenceCopies, processName, state);

        return SuccessExitCode;
    }

    private static int ProcessRunArgs(string processName, string[] args) {
        int err;

        err = ProcessBuildArgs(processName, args, out var state);

        if (err > 0)
            return err;

        switch (state.buildMode) {
            case BuildMode.CSharpTranspile:
            case BuildMode.Repl:
            case BuildMode.None:
                err = (int)ResolveDiagnostic(Belte.Diagnostics.Error.CannotRunBuildMode(), processName, state);
                break;
            case BuildMode.AutoRun:
            case BuildMode.Evaluate:
            case BuildMode.Execute:
            case BuildMode.Interpret:
                // Already executed from build
                break;
            case BuildMode.Dotnet:
                err = ExecuteRun(processName, state);
                break;
            case BuildMode.Independent:
            default:
                throw new UnreachableException();
        }

        if (err > 0)
            return err;

        return SuccessExitCode;
    }

    private static int ExecuteRun(string processName, CompilerState state) {
        var outputFilename = Path.ChangeExtension(state.outputFilename, ".exe");

        if (!File.Exists(outputFilename))
            return (int)ResolveDiagnostic(Belte.Diagnostics.Error.UnableToOpenFile(outputFilename), processName, state);

        var startInfo = new ProcessStartInfo() {
            CreateNoWindow = false,
            UseShellExecute = false,
            FileName = outputFilename,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        foreach (var arg in state.arguments)
            startInfo.ArgumentList.Add(arg);

        try {
            var process = Process.Start(startInfo);

            process.WaitForExit();
            return process.ExitCode;
        } catch (Win32Exception e) {
            return (int)ResolveDiagnostic(Belte.Diagnostics.Error.UnableToRun(e.Message), processName, state);
        }
    }

    private static XxHash64 HashStrings(params string[] strings) {
        var hasher = new XxHash64();

        foreach (var s in strings) {
            var bytes = Encoding.UTF8.GetBytes(s);
            // Currently always exactly 2 strings so no overflow potential
#pragma warning disable CA2014
            Span<byte> len = stackalloc byte[4];
#pragma warning restore CA2014
            BitConverter.TryWriteBytes(len, bytes.Length);

            hasher.Append(len);
            hasher.Append(bytes);
        }

        return hasher;
    }

    private static byte[] HashVersion(Version version) {
        using var ms = new MemoryStream();

        WriteInt(ms, version.Major);
        WriteInt(ms, version.Minor);
        WriteInt(ms, version.Build);
        WriteInt(ms, version.Revision);

        return ms.ToArray();

        static void WriteInt(Stream s, int value) {
            Span<byte> buffer = stackalloc byte[4];
            BitConverter.TryWriteBytes(buffer, value);
            s.Write(buffer);
        }
    }

    private static CompilerState ToCompilerState(
        DiagnosticQueue<Diagnostic> diagnostics,
        Builder builder,
        out string[] pendingReferenceCopies) {
        var references = new List<string>();
        var copies = new List<string>();

        foreach (var (reference, options) in builder.refs) {
            if (Directory.Exists(reference)) {
                var files = Directory.GetFiles(
                    reference,
                    "*.dll",
                    ((options & RefOptions.Flat) != 0) ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories
                );

                references.AddRange(files);

                if ((options & RefOptions.Copy) != 0)
                    copies.AddRange(files);
            } else if (File.Exists(reference)) {
                references.Add(reference);

                if ((options & RefOptions.Copy) != 0)
                    copies.Add(reference);
            } else {
                diagnostics.Push(Belte.Diagnostics.Error.NoSuchFileOrDirectory(reference));
            }
        }

        pendingReferenceCopies = copies.ToArray();

        references.AddRange(Compiler.ResolveLibraryLevel(builder.l));

        var outputFilename = builder.output ?? "a.exe";
        var moduleName = Path.GetFileNameWithoutExtension(outputFilename);

        var tasks = new List<FileState>();
        var taskDiagnosticOptions = new Dictionary<string, TaskDiagnosticOptions>();
        var globalDiagnosticOptions = TranslateDiagnosticOptions(diagnostics, builder.diagnosticOptions);

        foreach (var (input, options, diagnosticOptions) in builder.inputs) {
            var sourceTasks = ResolveInputFileOrDir(
                input,
                tasks,
                null,
                diagnostics,
                recursive: (options & InputOptions.Flat) == 0
            );

            if (diagnosticOptions != builder.diagnosticOptions) {
                var localDiagnosticOptions = TranslateDiagnosticOptions(diagnostics, diagnosticOptions);

                foreach (var task in sourceTasks)
                    taskDiagnosticOptions.Add(task.inputFileName, localDiagnosticOptions);
            }
        }

        var verboseMode = builder.verboseMode is VerboseMode.Normal or VerboseMode.Reduced;

        var maxCores = builder.maxCores > 0 ? builder.maxCores : Environment.ProcessorCount - 2;
        var concurrentBuild = maxCores > 1;

        return new CompilerState() {
            buildMode = builder.buildMode,
            moduleName = moduleName,
            references = references.ToArray(),
            debugMode = builder.debugBuild,
            diagnosticOptions = globalDiagnosticOptions,
            finishStage = CompilerStage.Finished,
            outputFilename = outputFilename,
            tasks = tasks.ToArray(),
            noOut = false,
            arguments = [],
            projectType = builder.outputKind,
            verboseMode = verboseMode,
            reducedVerboseMode = builder.verboseMode == VerboseMode.Reduced,
            verbosePath = builder.vPath,
            time = builder.verboseMode != VerboseMode.Off,
            concurrentBuild = concurrentBuild,
            maxCores = maxCores,
            entryName = builder.entryName,
            noStdLib = !builder.includeStdLib,
            taskDiagnosticOptions = taskDiagnosticOptions
        };
    }

    private static TaskDiagnosticOptions TranslateDiagnosticOptions(
        DiagnosticQueue<Diagnostic> diagnostics, DiagnosticOptions diagnosticOptions) {
        var excludeWarningsAsErrors = ParseAndVerifyWarningCodes(diagnosticOptions.werrexcludes.ToArray(), diagnostics);

        if (diagnosticOptions.warningsAsErrors)
            AddDefaultExcludeWarningsAsErrors(excludeWarningsAsErrors, diagnosticOptions.wErrorLevel);

        return new TaskDiagnosticOptions() {
            severity = diagnosticOptions.severity,
            warningLevel = diagnosticOptions.warningLevel,
            includeWarnings = ParseAndVerifyWarningCodes(diagnosticOptions.wincludes.ToArray(), diagnostics)
                .ToArray(),
            excludeWarnings = ParseAndVerifyWarningCodes(diagnosticOptions.wexcludes.ToArray(), diagnostics)
                .ToArray(),
            warningsAsErrors = diagnosticOptions.warningsAsErrors,
            includeWarningsAsErrors = ParseAndVerifyWarningCodes(diagnosticOptions.werrincludes.ToArray(), diagnostics)
                .ToArray(),
            excludeWarningsAsErrors = excludeWarningsAsErrors
                .ToArray(),
        };
    }

    private static int GetOrCreateBuildScript(
        string processName,
        BuildState state,
        DiagnosticQueue<Diagnostic> diagnostics,
        out Compiler compiler,
        out Builder builder) {
        var cacheDirectory = Path.Combine(state.buildDirectory, state.buildHash.ToString());
        var reuse = false;

        var index = LoadOrBuildIndex(state.buildDirectory);

        state.dllPath = Path.Combine(cacheDirectory, "build.dll");
        state.metaPath = Path.Combine(cacheDirectory, "meta.json");

        if (Directory.Exists(cacheDirectory)) {
            if (state.showInfo)
                Console.WriteLine("Reusing existing build artifacts");

            if (!File.Exists(state.dllPath) || !File.Exists(state.metaPath)) {
                if (state.showInfo)
                    Console.WriteLine("    Existing cache data is malformed: clearing and recreating");

                Directory.Delete(cacheDirectory);
                reuse = false;
            } else {
                UpdateLastAccess(
                    state.buildDirectory,
                    index,
                    state.dllPath,
                    state.metaPath,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                );

                reuse = true;
            }
        }

        var buildManager = new BuildManager(processName, state);

        compiler = buildManager.compiler;

        if (!reuse) {
            if (state.showInfo)
                Console.WriteLine("Creating new build artifacts");

            buildManager.CompileBuildScript(cacheDirectory, index);

            var err = ResolveDiagnostics(buildManager.compiler);

            if (err > 0) {
                builder = null;
                return err;
            }
        }

        builder = buildManager.RunBuildScript(diagnostics);
        PruneCache(state.buildDirectory, index);
        return SuccessExitCode;
    }

    private static void UpdateLastAccess(
        string cacheRoot,
        CacheIndex index,
        string entryPath,
        string metaPath,
        long newLastAccess) {
        if (!index.entries.TryGetValue(entryPath, out var entry))
            return;

        entry.lastAccess = newLastAccess;

        var json = File.ReadAllText(metaPath);
        var meta = JsonSerializer.Deserialize<CacheMetadata>(json);
        meta.lastAccess = newLastAccess;
        File.WriteAllText(metaPath, JsonSerializer.Serialize(meta));

        BuildManager.SaveIndex(cacheRoot, index);
    }

    private static void PruneCache(string cacheRoot, CacheIndex index) {
        if (index.totalSizeBytes <= MaxBuildCacheSize)
            return;

        var targetSize = (long)(MaxBuildCacheSize * 0.5);

        var ordered = index.entries.Values
            .OrderBy(e => e.lastAccess)
            .ToList();

        foreach (var entry in ordered) {
            if (index.totalSizeBytes <= targetSize)
                break;

            try {
                if (Directory.Exists(entry.path))
                    Directory.Delete(entry.path, recursive: true);
            } catch {
                continue;
            }

            index.totalSizeBytes -= entry.sizeBytes;
            index.entries.Remove(entry.path);
        }

        BuildManager.SaveIndex(cacheRoot, index);
    }

    private static CacheIndex LoadOrBuildIndex(string cacheRoot) {
        var indexPath = Path.Combine(cacheRoot, "index.json");

        if (File.Exists(indexPath))
            return JsonSerializer.Deserialize<CacheIndex>(File.ReadAllText(indexPath));

        var index = new CacheIndex();

        foreach (var dir in Directory.EnumerateDirectories(cacheRoot)) {
            var metaPath = Path.Combine(dir, "meta.json");

            if (!File.Exists(metaPath))
                continue;

            try {
                var meta = JsonSerializer.Deserialize<CacheMetadata>(File.ReadAllText(metaPath));

                var entry = new CacheIndexEntry {
                    path = dir,
                    lastAccess = meta.lastAccess,
                    sizeBytes = meta.sizeBytes
                };

                index.entries[dir] = entry;
                index.totalSizeBytes += meta.sizeBytes;
            } catch {
                // ignore corrupt entries
            }
        }

        BuildManager.SaveIndex(cacheRoot, index);
        return index;
    }

    private static string GetBuildDirectory() {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var buildFolder = Path.Combine(localAppData, "Buckle", "Build");

        if (!Directory.Exists(buildFolder))
            Directory.CreateDirectory(buildFolder);

        return buildFolder;
    }

    private static void ResolveSae(bool sae) {
        if (sae) {
            Console.Write("'--sae' specified: Press any key to continue...");
            Console.ReadKey();
            Console.WriteLine();
        }
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

        if (dialogs.clearCache)
            ShowClearCacheDialog();

        return diagnostics;
    }

    private static void ShowClearSubmissionsDialog() {
        var submissionCount = BelteRepl.ClearSubmissions();

        var previous = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("Cleared ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(submissionCount);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(" submissions");
        Console.ForegroundColor = previous;
    }

    private static void ShowErrorHelp(string _, out DiagnosticQueue<Diagnostic> diagnostics) {
        diagnostics = new DiagnosticQueue<Diagnostic>();
        diagnostics.Push(new Diagnostic(DiagnosticSeverity.Error, "--explain is not implemented"));
        return;
    }

    private static void ShowClearCacheDialog() {
        var cacheRoot = GetBuildDirectory();
        var indexPath = Path.Combine(cacheRoot, "index.json");
        long? size = null;

        if (File.Exists(indexPath))
            size = JsonSerializer.Deserialize<CacheIndex>(File.ReadAllText(indexPath)).totalSizeBytes;

        Directory.Delete(cacheRoot, recursive: true);

        if (size is null)
            Console.WriteLine("Deleted build cache");
        else
            Console.WriteLine($"Deleted build cache ({size.Value} bytes)");
    }

    private static void ShowHelpDialog() {
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream("CommandLine.Resources.HelpPrompt.txt");
        using var reader = new StreamReader(stream);
        Console.WriteLine(reader.ReadToEnd().TrimEnd());
    }

    private static void ShowMachineDialog() {
        var machineMessage = $"Host: {RuntimeInformation.RuntimeIdentifier}";
        Console.WriteLine(machineMessage);
    }

    private static void ShowVersionDialog() {
        Console.WriteLine($"Version: Buckle {GetVersionString()}");
    }

    private static string GetVersionString() {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("CommandLine.Resources.Version.txt");
        using var reader = new StreamReader(stream);
        return reader.ReadLine();
    }

    private static DiagnosticSeverity ResolveDiagnostic<Type>(
        Type diagnostic,
        string me,
        CompilerState state,
        ConsoleColor? textColor = null)
        where Type : Diagnostic {
        var previous = Console.ForegroundColor;

        if (textColor is not null)
            Console.ForegroundColor = textColor.Value;
        else
            Console.ResetColor();

        var info = diagnostic.info;
        var diagnosticOptions = state.diagnosticOptions;

        if (state.taskDiagnosticOptions is not null &&
            diagnostic is BelteDiagnostic diagnosticWithLocation &&
            diagnosticWithLocation.location is not null) {
            if (state.taskDiagnosticOptions.TryGetValue(diagnosticWithLocation.location.fileName, out var value))
                diagnosticOptions = value;
        }

        var ignoreDiagnostic = CheckDiagnosticSeverity(info, state, diagnosticOptions.severity);
        ignoreDiagnostic |= CheckWarningLevel(info, diagnosticOptions.warningLevel);
        ignoreDiagnostic &= !WarningIncluded(info, diagnosticOptions.includeWarnings);
        ignoreDiagnostic |= WarningExcluded(info, diagnosticOptions.excludeWarnings);

        if (!ignoreDiagnostic) {
            if (info.module != "BU") {
                Console.Write($"{me}: ");
                DiagnosticFormatter.PrettyPrint(diagnostic, textColor);
            } else {
                DiagnosticFormatter.PrettyPrint(diagnostic as BelteDiagnostic, textColor);
            }
        }

        Console.ForegroundColor = previous;
        return info.severity;
    }

    private static bool CheckDiagnosticSeverity(DiagnosticInfo info, CompilerState state, DiagnosticSeverity severity) {
        if (state.time && info.severity == DiagnosticSeverity.Debug)
            return false;

        return (int)severity > (int)info.severity;
    }

    private static bool CheckWarningLevel(DiagnosticInfo info, int warningLevel) {
        if (info.severity != DiagnosticSeverity.Warning)
            return false;

        if (warningLevel == 0)
            return true;
        else if (warningLevel == 1)
            return !WarningInWarningList(WarningLevel1, info);
        else if (warningLevel == 2)
            return !(WarningInWarningList(WarningLevel2, info) || WarningInWarningList(WarningLevel1, info));
        else if (warningLevel == 3)
            return false;

        throw new UnreachableException();
    }

    private static bool WarningIncluded(DiagnosticInfo info, DiagnosticInfo[] includeWarnings) {
        if (info.severity != DiagnosticSeverity.Warning)
            return false;

        return WarningInWarningList(includeWarnings, info);
    }

    private static bool WarningExcluded(DiagnosticInfo info, DiagnosticInfo[] excludeWarnings) {
        if (info.severity != DiagnosticSeverity.Warning)
            return false;

        return WarningInWarningList(excludeWarnings, info);
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

        var worst = diagnostics.ToList().Max(d => (int)d.info.severity);
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

    private static void CleanOutputFiles(Compiler compiler, DiagnosticQueue<Diagnostic> diagnostics) {
        if (compiler.state.finishStage == CompilerStage.Finished) {
            var path = compiler.state.outputFilename;

            if (File.Exists(path)) {
                File.Delete(path);
                return;
            } else if (Directory.Exists(path)) {
                diagnostics.Push(Belte.Diagnostics.Fatal.OutputIsDirectory(path));
                return;
            }

            var dirName = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(dirName) && !Directory.Exists(dirName))
                diagnostics.Push(Belte.Diagnostics.Error.NoSuchFileOrDirectory(dirName));

            return;
        }

        foreach (var file in compiler.state.tasks)
            File.Delete(file.outputFilename);
    }

    private static void ReadInputFiles(Compiler compiler, DiagnosticQueue<Diagnostic> diagnostics) {
        for (var i = 0; i < compiler.state.tasks.Length; i++) {
            ref var task = ref compiler.state.tasks[i];
            var opened = false;

            switch (task.stage) {
                case CompilerStage.Raw:
                case CompilerStage.Compiled:
                    for (var j = 1; j < 4; j++) {
                        try {
                            task.fileContent.text = File.ReadAllText(task.inputFileName);
                            opened = true;
                            break;
                        } catch (IOException) {
                            if (j < 3)
                                Thread.Sleep(j * 10);
                        }
                    }

                    if (!opened)
                        diagnostics.Push(Belte.Diagnostics.Error.UnableToOpenFile(task.inputFileName));

                    break;
                case CompilerStage.Assembled:
                    for (var j = 1; j < 4; j++) {
                        try {
                            task.fileContent.bytes = File.ReadAllBytes(task.inputFileName).ToList();
                            opened = true;
                            break;
                        } catch (IOException) {
                            if (j < 3)
                                Thread.Sleep(j * 10);
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

    private static void LogBuildState(BuildState state) {
        Console.WriteLine();
        Console.WriteLine("Build Script Information:");
        Console.WriteLine($"    Build script: \"{state.buildScript}\"");
        Console.WriteLine($"    Build hash: {state.buildHash}");
        Console.WriteLine();
    }

    private static void LogCompilerState(CompilerState state, string[] pendingReferenceCopies) {
        Console.WriteLine();
        Console.WriteLine($"Diagnostic reporting level: {Enum.GetName(state.diagnosticOptions.severity)}");
        Console.WriteLine($"Warning reporting level: {state.diagnosticOptions.warningLevel}");
        Console.WriteLine($"Included warnings: {string.Join(", ", state.diagnosticOptions.includeWarnings.AsEnumerable())}");
        Console.WriteLine($"Excluded warnings: {string.Join(", ", state.diagnosticOptions.excludeWarnings.AsEnumerable())}");
        Console.WriteLine($"Warnings as errors: {state.diagnosticOptions.warningsAsErrors}");
        Console.WriteLine($"Included warnings as errors: {string.Join(", ", state.diagnosticOptions.includeWarningsAsErrors.AsEnumerable())}");
        Console.WriteLine($"Excluded warnings as errors: {string.Join(", ", state.diagnosticOptions.excludeWarningsAsErrors.AsEnumerable())}");
        Console.WriteLine();
        Console.WriteLine($"Project type: {Enum.GetName(state.projectType)}");
        Console.WriteLine($"Build mode: {Enum.GetName(state.buildMode)}");
        Console.WriteLine($"Debug mode: {state.debugMode}");
        Console.WriteLine();
        Console.WriteLine($"Concurrent build: {state.concurrentBuild}");
        Console.WriteLine($"Max parallelism: {state.maxCores}");
        Console.WriteLine();
        Console.WriteLine(".NET Information:");
        Console.WriteLine($"    Module name: {state.moduleName}");
        Console.WriteLine($"    References: {string.Join(", ", state.references.Select(r => $"\"{r}\""))}");
        Console.WriteLine($"    Pending copies: ({pendingReferenceCopies.Length})");

        foreach (var pendingCopy in pendingReferenceCopies)
            Console.WriteLine($"        {pendingCopy} -> {Path.Join(state.outputFilename, Path.GetFileName(pendingCopy))}");

        Console.WriteLine();
        Console.WriteLine($"Verbose output path: \"{state.verbosePath}\"");
        Console.WriteLine();

        LogTasks(state);
    }

    private static void LogTasks(CompilerState state) {
        var tasks = state.tasks;
        Console.WriteLine($"File Tasks ({tasks.Length}) -> \"{state.outputFilename}\": {Enum.GetName(state.finishStage)}");

        foreach (var task in tasks) {
            Console.Write("    ");
            Console.WriteLine($"\"{task.inputFileName}\"{(task.outputFilename is null ? "" : $" -> \"{task.outputFilename}\"")}: {task.stage}");
        }

        Console.WriteLine();
    }

    private static string DecodeNewOptions(
        string[] args,
        out DiagnosticQueue<Diagnostic> diagnostics,
        out OutputKind outputKind) {
        outputKind = OutputKind.ConsoleApplication;
        diagnostics = new DiagnosticQueue<Diagnostic>();
        var name = "Project";

        for (var i = 1; i < args.Length; i++) {
            var arg = args[i];

            if (i == 1 && !arg.StartsWith('-')) {
                name = arg;
                continue;
            }

            if (arg.StartsWith("--type")) {
                if (arg == "--type" || arg == "--type=" || !arg.StartsWith("--type=")) {
                    diagnostics.Push(Belte.Diagnostics.Error.MissingType(arg));
                    continue;
                }

                var type = arg.Substring(7).ToLower();

                if (type == "console")
                    outputKind = OutputKind.ConsoleApplication;
                else if (type == "graphics")
                    outputKind = OutputKind.GraphicsApplication;
                else if (type == "dll")
                    outputKind = OutputKind.DynamicallyLinkedLibrary;
                else
                    diagnostics.Push(Belte.Diagnostics.Error.UnrecognizedType(type));
            } else {
                diagnostics.Push(Belte.Diagnostics.Error.UnrecognizedOption(arg));
            }
        }

        return name;
    }

    private static BuildState DecodeBuildOptions(
        string[] args,
        out DiagnosticQueue<Diagnostic> diagnostics,
        out string[] arguments,
        out bool debugMode) {
        var state = new BuildState {
            showTime = false,
            showInfo = false,
            buildScript = "Build.blt"
        };

        diagnostics = new DiagnosticQueue<Diagnostic>();
        arguments = Array.Empty<string>();
        debugMode = false;

        for (var i = 1; i < args.Length; i++) {
            var arg = args[i];

            if (arg == "--") {
                if (args.Length > i + 1)
                    arguments = args[(i + 1)..];

                break;
            }

            switch (arg) {
                case "--info":
                    state.showInfo = true;
                    break;
                case "--time":
                    state.showTime = true;
                    break;
                case "--debug":
                    debugMode = true;
                    break;
                default:
                    if (i == 1 && !arg.StartsWith('-'))
                        state.buildScript = arg;
                    else
                        diagnostics.Push(Belte.Diagnostics.Error.UnrecognizedOption(arg));

                    break;
            }
        }

        return state;
    }

    private static CompilerState DecodeOptions(
        string[] args,
        out DiagnosticQueue<Diagnostic> diagnostics,
        out ShowDialogs dialogs,
        out bool multipleExplains,
        out bool saExit,
        out string[] pendingReferenceCopies) {
        var state = new CompilerState();
        var tasks = new List<FileState>();
        var references = new List<string>();
        var copies = new List<string>();
        var diagnosticsCL = new DiagnosticQueue<Diagnostic>();
        diagnostics = new DiagnosticQueue<Diagnostic>();
        var arguments = Array.Empty<string>();
        var includeWarnings = new List<DiagnosticInfo>();
        var excludeWarnings = new List<DiagnosticInfo>();
        var includeWarningsAsErrors = new List<DiagnosticInfo>();
        var excludeWarningsAsErrors = new List<DiagnosticInfo>();

        var specifyStage = false;
        var specifyOut = false;
        var specifyModule = false;
        var specifyBuildMode = false;
        var specifyWarningLevel = false;
        var wErrorLevel = 2;

        var l = -1;
        var sae = false;

        string currentFileAssociation = null;

        var tempDialogs = new ShowDialogs {
            help = false,
            machine = false,
            version = false,
            clearSubmissions = false,
            clearCache = false,
            error = null,
        };

        multipleExplains = false;

        state.buildMode = BuildMode.AutoRun;
        state.finishStage = CompilerStage.Finished;
        state.outputFilename = "a.exe";
        state.moduleName = "a";
        state.noOut = false;
        state.diagnosticOptions = new TaskDiagnosticOptions() {
            warningLevel = 1,
            severity = DiagnosticSeverity.Warning,
            warningsAsErrors = false,
        };
        state.projectType = OutputKind.ConsoleApplication;
        state.verboseMode = false;
        state.reducedVerboseMode = false;
        state.time = false;
        state.debugMode = false;
        state.concurrentBuild = true;
        state.maxCores = Environment.ProcessorCount - 2;

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
                case "--debug":
                    state.debugMode = true;
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
                case "--clearsubmissions":
                    tempDialogs.clearSubmissions = true;
                    break;
                case "--clearcache":
                    tempDialogs.clearCache = true;
                    break;
                case "--noout":
                    state.noOut = true;
                    break;
                case "--verbose":
                    state.verboseMode = true;
                    break;
                case "--info":
                    state.verboseMode = true;
                    state.reducedVerboseMode = true;
                    break;
                case "--time":
                    state.time = true;
                    break;
                case "-l0":
                    l = 0;
                    break;
                case "-l1":
                    l = 1;
                    break;
                case "-lall":
                    l = 2;
                    break;
                case "--sae":
                    sae = true;
                    break;
                case "--nostdlib":
                    state.noStdLib = true;
                    break;
                default:
                    diagnosticsCL.Push(Belte.Diagnostics.Error.UnrecognizedOption(arg));
                    break;
            }
        }

        for (var i = 0; i < args.Length; i++) {
            var arg = args[i];

            if (!arg.StartsWith('-')) {
                ResolveInputFileOrDir(arg, tasks, currentFileAssociation, diagnostics);
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
                if (arg != "--modulename" && arg != "--modulename=" && arg.StartsWith("--modulename=")) {
                    specifyModule = true;
                    state.moduleName = arg.Substring(13);
                } else {
                    diagnostics.Push(Belte.Diagnostics.Error.MissingModuleName(arg));
                }
            } else if (arg.StartsWith("--ref")) {
                bool err;

                if (arg != "--reference" && arg != "--reference=" && arg.StartsWith("--reference"))
                    err = ResolveInputRefs(arg.Substring(11), references, copies, diagnostics);
                else if (arg != "--ref" && arg != "--ref=")
                    err = ResolveInputRefs(arg.Substring(5), references, copies, diagnostics);
                else
                    err = true;

                if (err)
                    diagnostics.Push(Belte.Diagnostics.Error.MissingReference(arg));
            } else if (arg.StartsWith("--severity")) {
                if (arg == "--severity" || arg == "--severity=" || !arg.StartsWith("--severity=")) {
                    diagnostics.Push(Belte.Diagnostics.Error.MissingSeverity(arg));
                    continue;
                }

                var severityString = arg.Substring(11);

                if (Enum.TryParse<DiagnosticSeverity>(severityString, true, out var severityLevel))
                    state.diagnosticOptions.severity = severityLevel;
                else
                    diagnostics.Push(Belte.Diagnostics.Error.UnrecognizedSeverity(severityString));
            } else if (arg.StartsWith("--warnlevel")) {
                if (arg == "--warnlevel" || arg == "--warnlevel=" || !arg.StartsWith("--warnlevel=")) {
                    diagnostics.Push(Belte.Diagnostics.Error.MissingWarningLevel(arg));
                    continue;
                }

                var warningString = arg.Substring(12);

                if (int.TryParse(warningString, out var warningLevel) && 0 <= warningLevel && warningLevel <= 3) {
                    specifyWarningLevel = true;
                    state.diagnosticOptions.warningLevel = warningLevel;
                } else {
                    diagnostics.Push(Belte.Diagnostics.Error.InvalidWarningLevel(warningString));
                }
            } else if (arg.StartsWith("--werror")) {
                state.diagnosticOptions.warningsAsErrors = true;

                if (arg == "--werror")
                    continue;

                if (arg == "--werror=" || !arg.StartsWith("--werror=")) {
                    diagnostics.Push(Belte.Diagnostics.Error.MissingWarningLevelAfterWError(arg));
                    continue;
                }

                var warningString = arg.Substring(9);

                if (int.TryParse(warningString, out var warningLevel) && 0 <= warningLevel && warningLevel <= 3)
                    wErrorLevel = warningLevel;
                else
                    diagnostics.Push(Belte.Diagnostics.Error.InvalidWarningLevel(warningString));
            } else if (arg.StartsWith("--wignore")) {
                if (arg == "--wignore" || arg == "--wignore=" || !arg.StartsWith("--wignore=")) {
                    diagnostics.Push(Belte.Diagnostics.Error.MissingWIgnoreCode(arg));
                    continue;
                }

                excludeWarnings.AddRange(ParseAndVerifyWarningCodes(arg.Substring(10), diagnosticsCL));
            } else if (arg.StartsWith("--winclude")) {
                if (arg == "--winclude" || arg == "--winclude=" || !arg.StartsWith("--winclude=")) {
                    diagnostics.Push(Belte.Diagnostics.Error.MissingWIncludeCode(arg));
                    continue;
                }

                includeWarnings.AddRange(ParseAndVerifyWarningCodes(arg.Substring(11), diagnosticsCL));
            } else if (arg.StartsWith("--werrignore")) {
                if (arg == "--werrignore" || arg == "--werrignore=" || !arg.StartsWith("--werrignore=")) {
                    diagnostics.Push(Belte.Diagnostics.Error.MissingWErrIgnoreCode(arg));
                    continue;
                }

                excludeWarningsAsErrors.AddRange(ParseAndVerifyWarningCodes(arg.Substring(13), diagnosticsCL));
            } else if (arg.StartsWith("--werrinclude")) {
                if (arg == "--werrinclude" || arg == "--werrinclude=" || !arg.StartsWith("--werrinclude=")) {
                    diagnostics.Push(Belte.Diagnostics.Error.MissingWErrIncludeCode(arg));
                    continue;
                }

                includeWarningsAsErrors.AddRange(ParseAndVerifyWarningCodes(arg.Substring(14), diagnosticsCL));
            } else if (arg.StartsWith("--type")) {
                if (arg == "--type" || arg == "--type=" || !arg.StartsWith("--type=")) {
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
            } else if (arg.StartsWith("--vpath")) {
                if (arg == "--vpath" || arg == "--vpath=" || !arg.StartsWith("--vpath=")) {
                    diagnostics.Push(Belte.Diagnostics.Error.MissingVerbosePath(arg));
                    continue;
                }

                var path = arg.Substring(8);

                if (!Directory.Exists(path)) {
                    diagnostics.Push(Belte.Diagnostics.Error.NoSuchFileOrDirectory(path));
                    continue;
                }

                state.verbosePath = path;
            } else if (arg.StartsWith("-m")) {
                if (arg == "-m" || arg == "-m:" || !arg.StartsWith("-m:")) {
                    diagnostics.Push(Belte.Diagnostics.Error.MissingMaxCoreCount(arg));
                    continue;
                }

                var maxCoresString = arg.Substring(3);

                if (!int.TryParse(maxCoresString, out var coreCount) || coreCount < 1)
                    diagnostics.Push(Belte.Diagnostics.Error.InvalidMaxCoreCount(maxCoresString));
                else
                    state.maxCores = Math.Min(coreCount, Environment.ProcessorCount);
            } else if (arg.StartsWith("--entry")) {
                if (arg == "--entry" || arg == "--entry=" || !arg.StartsWith("--entry=")) {
                    diagnostics.Push(Belte.Diagnostics.Error.MissingEntryName(arg));
                    continue;
                }

                state.entryName = arg.Substring(8);
            } else if (arg.StartsWith("-x") || arg.StartsWith("--lang")) {
                var isShorthand = arg.StartsWith("-x");

                if (isShorthand && arg != "-x") {
                    currentFileAssociation = GetFileAssociation(arg.Substring(2), diagnostics);
                    continue;
                }

                if (!isShorthand && arg != "--lang") {
                    currentFileAssociation = GetFileAssociation(arg.Substring(6), diagnostics);
                    continue;
                }

                if (i < args.Length - 1)
                    currentFileAssociation = GetFileAssociation(args[++i], diagnostics);
                else
                    diagnostics.Push(Belte.Diagnostics.Error.MissingFileAssociation(arg));
            } else if (arg.StartsWith("--flat")) {
                if (arg != "--flat") {
                    ResolveInputFileOrDir(arg.Substring(6), tasks, currentFileAssociation, diagnostics, false);
                    continue;
                }

                if (i < args.Length - 1)
                    ResolveInputFileOrDir(args[++i], tasks, currentFileAssociation, diagnostics, false);
                else
                    diagnostics.Push(Belte.Diagnostics.Error.MissingPathFlat());
            } else if (arg == "--") {
                if (args.Length > i + 1)
                    arguments = args[(i + 1)..];

                break;
            } else {
                DecodeSimpleOption(arg);
            }
        }

        saExit = sae;

        if (sae) {
            Console.Write("'--sae' specified: Press any key to continue...");
            Console.ReadKey();
        }

        if (state.maxCores == 1)
            state.concurrentBuild = false;

        references.AddRange(Compiler.ResolveLibraryLevel(l));
        pendingReferenceCopies = copies.ToArray();

        dialogs = tempDialogs;
        diagnostics.Move(diagnosticsCL);

        if (dialogs.machine || dialogs.help || dialogs.version || dialogs.error is not null || dialogs.clearCache)
            return state;

        state.tasks = tasks.ToArray();
        state.references = references.ToArray();
        state.arguments = arguments;
        state.diagnosticOptions.includeWarnings = includeWarnings.ToArray();
        state.diagnosticOptions.excludeWarnings = excludeWarnings.ToArray();

        if (state.diagnosticOptions.warningsAsErrors)
            AddDefaultExcludeWarningsAsErrors(excludeWarningsAsErrors, wErrorLevel);

        state.diagnosticOptions.includeWarningsAsErrors = includeWarningsAsErrors.ToArray();
        state.diagnosticOptions.excludeWarningsAsErrors = excludeWarningsAsErrors.ToArray();

        if (state.projectType == OutputKind.DynamicallyLinkedLibrary || state.buildMode == BuildMode.Dotnet) {
            if (!specifyBuildMode)
                state.buildMode = BuildMode.Dotnet;

            if (state.buildMode != BuildMode.Dotnet)
                diagnostics.Push(Belte.Diagnostics.Fatal.DLLWithWrongBuildMode());

            if (!specifyOut)
                state.outputFilename = "a.dll";
        }

        if (!specifyWarningLevel &&
            state.buildMode is BuildMode.AutoRun or BuildMode.Interpret or BuildMode.Evaluate or BuildMode.Execute) {
            state.diagnosticOptions.warningLevel = 0;
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

        if (references.Count > 0 && state.buildMode is not BuildMode.Dotnet and not
                                                           BuildMode.AutoRun and not
                                                           BuildMode.Execute) {
            diagnostics.Push(Belte.Diagnostics.Fatal.CannotSpecifyReferencesWithoutDotnet());
        }

        foreach (var reference in references) {
            if (!File.Exists(reference))
                diagnostics.Push(Belte.Diagnostics.Error.NoSuchFileOrDirectory(reference));
        }

        if (state.projectType == OutputKind.DynamicallyLinkedLibrary) {
            if (specifyOut && specifyModule)
                diagnostics.Push(Belte.Diagnostics.Fatal.CannotSpecifyOutAndModuleWithDll());
            else if (!specifyOut)
                state.outputFilename = state.moduleName + ".dll";
            else if (!specifyModule)
                state.moduleName = Path.GetFileNameWithoutExtension(state.outputFilename);
        }

        state.outputFilename = state.outputFilename.Trim();

        if (state.verboseMode) {
            state.diagnosticOptions.severity = DiagnosticSeverity.All;
            state.diagnosticOptions.warningLevel = Math.Max(2, state.diagnosticOptions.warningLevel);
            state.time = true;
        }

        if (state.tasks.Length == 0) {
            if (state.buildMode == BuildMode.Repl || dialogs.clearSubmissions)
                // We don't want to resolve output files since they aren't used, so early return
                return state;

            diagnostics.Push(Belte.Diagnostics.Fatal.NoInputFiles());
        }

        ResolveOutputFileNames(state.tasks, state.finishStage, specifyOut ? state.outputFilename : null);

        return state;
    }

    private static void AddDefaultExcludeWarningsAsErrors(
        List<DiagnosticInfo> excludeWarningsAsErrors,
        int wErrorLevel) {
        if (wErrorLevel < 3)
            excludeWarningsAsErrors.AddRange(WarningLevel3);
        if (wErrorLevel < 2)
            excludeWarningsAsErrors.AddRange(WarningLevel2);
        if (wErrorLevel < 1)
            excludeWarningsAsErrors.AddRange(WarningLevel1);
    }

    private static List<DiagnosticInfo> ParseAndVerifyWarningCodes(
        string codesString,
        DiagnosticQueue<Diagnostic> diagnostics) {
        var codes = codesString.Split(',');
        return ParseAndVerifyWarningCodes(codes, diagnostics);
    }

    private static List<DiagnosticInfo> ParseAndVerifyWarningCodes(
        string[] codes,
        DiagnosticQueue<Diagnostic> diagnostics) {
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

    private static void ResolveReferenceCopies(string outputPath, string[] references, string me, CompilerState state) {
        var path = Path.GetDirectoryName(outputPath);

        foreach (var reference in references) {
            var destination = Path.Join(path, Path.GetFileName(reference));
            var opened = false;

            for (var j = 1; j < 4; j++) {
                try {
                    File.Copy(reference, destination, overwrite: true);
                    opened = true;
                    break;
                } catch (IOException) {
                    if (j < 3)
                        Thread.Sleep(j * 10);
                }
            }

            if (!opened)
                // ? We are moments away from exiting so we will just call resolve ourselves instead of creating a queue
                ResolveDiagnostic(Belte.Diagnostics.Warning.UnableToCopyFile(reference, destination), me, state);
        }
    }

    private static void ResolveOutputFileNames(
        FileState[] tasks,
        CompilerStage finishStage,
        string outputFilename) {
        if (tasks.Length == 1 && outputFilename is not null) {
            tasks[0].outputFilename = outputFilename;
            return;
        }

        var ext = finishStage switch {
            CompilerStage.Assembled => "o",
            CompilerStage.Compiled => "s",
            CompilerStage.Finished => "exe",
            _ => null
        };

        for (var i = 0; i < tasks.Length; i++) {
            var fileName = string.Join('.', tasks[i].inputFileName.Split('.').SkipLast(1));

            if (ext is not null)
                tasks[i].outputFilename = string.Join('.', fileName, ext);
            else
                tasks[i].outputFilename = fileName;
        }
    }

    private static string GetFileAssociation(string arg, DiagnosticQueue<Diagnostic> diagnostics) {
        switch (arg) {
            case "blt":
            case "belte":
            case "s":
            case "asm":
            case "o":
            case "obj":
            case "exe":
                return arg;
            case "none":
                return null;
            default:
                diagnostics.Push(Belte.Diagnostics.Error.UnrecognizedFileAssociation(arg));
                return null;
        }
    }

    private static bool ResolveInputRefs(
        string arg,
        List<string> references,
        List<string> copies,
        DiagnosticQueue<Diagnostic> diagnostics) {
        var flat = false;
        var copy = false;

        string name;

        if (arg.StartsWith('=')) {
            name = arg.Substring(1);
        } else if (arg.StartsWith(",flat,copy=") || arg.StartsWith(",copy,flat=")) {
            flat = true;
            copy = true;
            name = arg.Substring(11);
        } else if (arg.StartsWith(",flat=")) {
            flat = true;
            name = arg.Substring(6);
        } else if (arg.StartsWith(",copy=")) {
            copy = true;
            name = arg.Substring(6);
        } else {
            return true;
        }

        if (Directory.Exists(name)) {
            var files = Directory.GetFiles(
                name,
                "*.dll",
                flat ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories
            );

            references.AddRange(files);

            if (copy)
                copies.AddRange(files);
        } else if (File.Exists(name)) {
            references.Add(name);

            if (copy)
                copies.Add(name);
        } else {
            diagnostics.Push(Belte.Diagnostics.Error.NoSuchFileOrDirectory(name));
        }

        return false;
    }

    private static FileState[] ResolveInputFileOrDir(
        string name,
        List<FileState> tasks,
        string fileAssociation,
        DiagnosticQueue<Diagnostic> diagnostics,
        bool recursive = true) {
        var fileNames = new List<string>();
        var fileStates = new List<FileState>();

        if (Directory.Exists(name)) {
            fileNames.AddRange(Directory.GetFiles(
                name,
                "*",
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly
            ));
        } else if (File.Exists(name)) {
            fileNames.Add(name);
        } else {
            diagnostics.Push(Belte.Diagnostics.Error.NoSuchFileOrDirectory(name));
            return [];
        }

        foreach (var fileName in fileNames) {
            var task = new FileState {
                inputFileName = fileName
            };

            string type;

            if (fileAssociation is null) {
                var parts = task.inputFileName.Split('.');
                type = parts[parts.Length - 1];
            } else {
                type = fileAssociation;
            }

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
            fileStates.Add(task);
        }

        return fileStates.ToArray();
    }
}

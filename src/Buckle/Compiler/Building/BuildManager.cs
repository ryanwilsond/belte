using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Buckle.Diagnostics;
using Diagnostics;

namespace Buckle.Building;

public sealed class BuildManager {
    private readonly BuildState _state;

    public BuildManager(string processName, BuildState state) {
        _state = state;

        compiler = new Compiler(new CompilerState()) {
            me = processName
        };
    }

    public Compiler compiler { get; }

    public void CompileBuildScript(string cacheDirectoryToCreate, CacheIndex index) {
        var outputFilename = _state.dllPath;

        var task = new FileState() {
            inputFileName = _state.buildScript,
            outputFilename = outputFilename,
            stage = CompilerStage.Raw,
            fileContent = new FileContent() {
                text = _state.buildScriptText
            }
        };

        var compilerState = new CompilerState() {
            buildMode = BuildMode.Dotnet,
            moduleName = "build",
            references = Compiler.ResolveLibraryLevel(1),
            debugMode = false,
            diagnosticOptions = new TaskDiagnosticOptions() {
                severity = DiagnosticSeverity.Error,
                warningLevel = 1,
                includeWarnings = [],
                excludeWarnings = [],
                includeWarningsAsErrors = [],
                excludeWarningsAsErrors = [],
            },
            finishStage = CompilerStage.Finished,
            outputFilename = outputFilename,
            tasks = [task],
            noOut = false,
            arguments = [],
            projectType = OutputKind.DynamicallyLinkedLibrary,
            verboseMode = _state.showInfo,
            reducedVerboseMode = _state.showInfo,
            verbosePath = null,
            time = _state.showTime,
            concurrentBuild = false,
            maxCores = 1,
            entryName = null,
            noStdLib = false
        };

        compiler.state = compilerState;

        Directory.CreateDirectory(cacheDirectoryToCreate);

        if (compiler.Compile() != 0) {
            Directory.Delete(cacheDirectoryToCreate);
            return;
        }

        var sizeInBytes = new FileInfo(_state.dllPath).Length;

        var meta = new CacheMetadata {
            lastAccess = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            sizeBytes = sizeInBytes
        };

        var options = new JsonSerializerOptions {
            WriteIndented = false
        };

        var json = JsonSerializer.Serialize(meta, options);

        File.WriteAllText(_state.metaPath, json);

        AddCacheEntry(_state.buildDirectory, index, _state.dllPath, meta);
    }

    private static void AddCacheEntry(
        string cacheRoot,
        CacheIndex index,
        string entryPath,
        CacheMetadata meta) {
        var entry = new CacheIndexEntry {
            path = entryPath,
            lastAccess = meta.lastAccess,
            sizeBytes = meta.sizeBytes
        };

        index.entries[entryPath] = entry;
        index.totalSizeBytes += meta.sizeBytes;

        SaveIndex(cacheRoot, index);
    }

    public static void SaveIndex(string cacheRoot, CacheIndex index) {
        var indexPath = Path.Combine(cacheRoot, "index.json");

        File.WriteAllText(indexPath,
            JsonSerializer.Serialize(index, new JsonSerializerOptions {
                WriteIndented = false
            })
        );
    }

    public Builder RunBuildScript(DiagnosticQueue<Diagnostic> diagnostics) {
        var builder = new Builder();

        var assembly = Assembly.LoadFrom(_state.dllPath);
        var buildMethod = assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .FirstOrDefault(m => (m.Name == "Build" || m.Name.StartsWith("<Main>ss__Build")) &&
                m.GetParameters().Length == 1 &&
                typeof(Builder).IsAssignableFrom(m.GetParameters()[0].ParameterType));

        if (buildMethod is not null && (buildMethod.IsGenericMethod || buildMethod.ReturnType != typeof(void)))
            buildMethod = null;

        if (buildMethod is null) {
            diagnostics.Push(Error.NoBuildMethod());
            // TODO We could hook into the compilation of the script to check for the correct symbols instead of doing this post-hoc
            // But this approach has the benefit of not having to touch the main compiler APIs
            File.Delete(_state.dllPath);
            File.Delete(_state.metaPath);
        } else {
            buildMethod.Invoke(null, [builder]);
        }

        return builder;
    }
}

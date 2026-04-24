using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Emitting;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Diagnostics;
using Shared;

namespace Buckle;

/// <summary>
/// Handles compiling and handling a single <see cref="CompilerState" />.
/// Multiple can be created and run asynchronously.
/// </summary>
public sealed class Compiler {
    private const int SuccessExitCode = 0;
    private const int ErrorExitCode = 1;
    private const int FatalExitCode = 2;

    private Compilation _lazyCorLibrary;
    private BelteDiagnosticQueue _lazyCorLibraryDiagnostics;

    private CompilationOptions _options => new CompilationOptions(
        state.buildMode,
        state.projectType,
        state.arguments,
        false,
        !state.noOut,
        state.references,
        state.concurrentBuild,
        state.maxCores
    );

    /// <summary>
    /// Creates a new <see cref="Compiler" />, state needs to be set separately.
    /// </summary>
    public Compiler(CompilerState state) {
        this.state = state;
    }

    /// <summary>
    /// Compiler specific state that determines what to compile and how.
    /// Required to compile.
    /// </summary>
    public CompilerState state { get; set; }

    /// <summary>
    /// The name of the compiler (usually displayed with diagnostics).
    /// </summary>
    public string me { get; set; }

    /// <summary>
    /// The diagnostics from the most recent compiler operation.
    /// </summary>
    public BelteDiagnosticQueue diagnostics { get; private set; } = new BelteDiagnosticQueue();

    /// <summary>
    /// The exceptions from the most recent evaluation.
    /// </summary>
    public List<Exception> exceptions { get; private set; } = [];

    /// <summary>
    /// Handles compiling, assembling, and linking of a set of files.
    /// </summary>
    /// <returns>Error code, 0 = success.</returns>
    public int Compile() {
        lock (state) lock (me) {
            diagnostics.Clear();

            if (state.buildMode is BuildMode.AutoRun or BuildMode.Interpret or BuildMode.Evaluate or BuildMode.Execute)
                InternalInterpreter();
            else
                InternalCompiler();

            return CalculateExitCode(diagnostics);
        }
    }

    /// <summary>
    /// Gets .NET library paths for a given library level.
    /// </summary>
    public static string[] ResolveLibraryLevel(int l) {
        if (l < 0)
            return [];

        var tfm = DotnetReferenceResolver.GetTFM();
        var refPackPath = DotnetReferenceResolver.ResolveNetCoreAppRefPath(tfm, out _);

        var references = new List<string>();

        if (l >= 0) {
            references.Add(Path.Join(refPackPath, "System.Runtime.dll"));
            references.Add(Path.Join(refPackPath, "System.IO.dll"));
            references.Add(Path.Join(refPackPath, "System.Console.dll"));
            references.Add(Path.Join(refPackPath, "System.Collections.dll"));
        }

        if (l >= 1) {
            references.Add(Path.Join(AppContext.BaseDirectory, "Compiler.dll"));
            references.Add(Path.Join(AppContext.BaseDirectory, "Diagnostics.dll"));
            references.Add(Path.Join(AppContext.BaseDirectory, "Shared.dll"));
            references.Add(Path.Join(refPackPath, "System.Collections.Immutable.dll"));
        }

        return references.ToArray();
    }

    private static int CalculateExitCode(BelteDiagnosticQueue diagnostics) {
        var worst = SuccessExitCode;

        foreach (Diagnostic diagnostic in diagnostics) {
            if (diagnostic.info.severity == DiagnosticSeverity.Error)
                worst = ErrorExitCode;
        }

        return worst;
    }

    public void AddLibraryErrors(BelteDiagnosticQueue libraryDiagnostics) {
        diagnostics.PushRange(libraryDiagnostics.Errors());
        diagnostics.Push(Fatal.LibraryError());
    }

    private BelteDiagnosticQueue GetCorLibrary(out Compilation compilation) {
        if (_lazyCorLibrary is null || _lazyCorLibraryDiagnostics is null) {
            var corLibrary = LibraryHelpers.LoadLibraries(
                _options.buildMode,
                _options.concurrentBuild,
                _options.maxCoreCount
            );

            var corLibraryDiagnostics = corLibrary.GetDiagnostics();
            Interlocked.CompareExchange(ref _lazyCorLibrary, corLibrary, null);
            Interlocked.CompareExchange(ref _lazyCorLibraryDiagnostics, corLibraryDiagnostics, null);
        }

        compilation = _lazyCorLibrary;
        return _lazyCorLibraryDiagnostics;
    }

    private void ReportAndReturnLibraryErrors() {
        diagnostics.PushRange(_lazyCorLibraryDiagnostics);
        diagnostics.Push(Fatal.LibraryError());
    }

    private void InternalInterpreter() {
        var timer = (state.time && !state.noOut) ? Stopwatch.StartNew() : null;
        var textLength = 0;
        var textsCount = 0;

        foreach (var task in state.tasks) {
            if (task.fileContent.text is not null) {
                textLength += task.fileContent.text.Length;
                textsCount++;
            }
        }

        // From profiling we found:
        //      1) Interpreter is almost always the slowest option
        //      2) Evaluator is only better than Executor for trivially simple programs
        var buildMode = state.buildMode != BuildMode.AutoRun ? state.buildMode : BuildMode.Execute;

        var options = new CompilationOptions(
            buildMode,
            _options.outputKind,
            _options.arguments,
            _options.isScript,
            _options.enableOutput,
            _options.references,
            _options.concurrentBuild,
            _options.maxCoreCount
        );

        if (buildMode is BuildMode.Evaluate or BuildMode.Execute) {
            if (GetCorLibrary(out var corLibrary).AnyErrors()) {
                ReportAndReturnLibraryErrors();
                return;
            }

            var libTime = LogLibraryLoadTime(timer);

            var syntaxTrees = CreateSyntaxTrees(CompilerStage.Finished);
            var compilation = Compilation.Create(state.moduleName, options, corLibrary, syntaxTrees);

            var parseDiagnostics = compilation.GetParseDiagnostics();

            if (state.noOut || parseDiagnostics.AnyErrors()) {
                diagnostics.PushRange(parseDiagnostics);
                return;
            }

            LogParseTime(timer, libTime, syntaxTrees.Length);

            void Wrapper(object parameter) {
                if (buildMode == BuildMode.Evaluate) {
                    var result = compilation.Evaluate(
                        (ValueWrapper<bool>)parameter,
                        state.verboseMode,
                        state.time,
                        state.verbosePath,
                        state.reducedVerboseMode
                    );

                    exceptions = result.exceptions;
                    diagnostics.PushRange(result.diagnostics);
                } else {
                    diagnostics.PushRange(compilation.Execute(
                        state.verboseMode,
                        state.time,
                        state.verbosePath,
                        state.reducedVerboseMode
                    ));
                }
            }

            if (buildMode == BuildMode.Execute)
                Wrapper(false);
            else
                InternalInterpreterStart(Wrapper);
        } else {
            Debug.Assert(state.tasks.Length == 1, "multiple tasks while in script mode");

            if (GetCorLibrary(out var corLibrary).AnyErrors()) {
                ReportAndReturnLibraryErrors();
                return;
            }

            var libTime = LogLibraryLoadTime(timer);

            ref var task = ref state.tasks[0];
            var sourceText = new StringText(task.inputFileName, SourceText.DefaultEncoding, task.fileContent.text);
            var syntaxTree = new SyntaxTree(sourceText, SourceCodeKind.Regular, CreateParseOptions());
            task.stage = CompilerStage.Finished;

            var compilation = Compilation.CreateScript(state.moduleName, options, syntaxTree, corLibrary);

            var parseDiagnostics = compilation.GetParseDiagnostics();

            if (state.noOut || parseDiagnostics.AnyErrors()) {
                diagnostics.PushRange(compilation.GetParseDiagnostics());
                return;
            }

            LogParseTime(timer, libTime, 1);

            void Wrapper(object parameter) {
                var result = compilation.Interpret((ValueWrapper<bool>)parameter, state.time);
                diagnostics.PushRange(result.diagnostics);
            }

            InternalInterpreterStart(Wrapper);
        }

        LogCompilationTime(timer);
    }

    private void InternalCompiler() {
        var timer = (state.time && !state.noOut) ? Stopwatch.StartNew() : null;

        if (GetCorLibrary(out var corLibrary).AnyErrors()) {
            ReportAndReturnLibraryErrors();
            return;
        }

        var libTime = LogLibraryLoadTime(timer);

        var syntaxTrees = CreateSyntaxTrees(CompilerStage.Compiled);
        var compilation = Compilation.Create(state.moduleName, _options, corLibrary, syntaxTrees);

        var parseDiagnostics = compilation.GetParseDiagnostics();

        if (state.noOut || parseDiagnostics.AnyErrors()) {
            diagnostics.PushRange(parseDiagnostics);
            return;
        }

        LogParseTime(timer, libTime, syntaxTrees.Length);

        diagnostics.PushRange(compilation.Emit(
            state.outputFilename,
            state.debugMode,
            state.time,
            state.verboseMode,
            state.verbosePath,
            state.reducedVerboseMode
        ));

        LogCompilationTime(timer);
    }

    private SyntaxTree[] CreateSyntaxTrees(CompilerStage stageToSet) {
        var tasks = state.tasks;
        var length = tasks.Length;
        var builder = new SyntaxTree[length];

        var parseOptions = CreateParseOptions();

        if (state.concurrentBuild) {
            Parallel.For(0, length, new ParallelOptions { MaxDegreeOfParallelism = state.maxCores }, i => {
                var task = tasks[i];

                if (task.stage == CompilerStage.Raw)
                    builder[i] = SyntaxTree.Load(task.inputFileName, task.fileContent.text, parseOptions);
            });
        } else {
            for (var i = 0; i < length; i++) {
                var task = tasks[i];

                if (task.stage == CompilerStage.Raw)
                    builder[i] = SyntaxTree.Load(task.inputFileName, task.fileContent.text, parseOptions);
            }
        }

        var count = 0;

        for (var i = 0; i < length; i++) {
            ref var task = ref tasks[i];

            if (builder[i] is not null) {
                builder[count++] = builder[i];
                task.stage = stageToSet;
            }
        }

        Array.Resize(ref builder, count);
        return builder;
    }

    private void LogParseTime(Stopwatch timer, long libTime, int count) {
        if (timer is null)
            return;

        Log(timer, $"Loaded {count} syntax tree in {timer.ElapsedMilliseconds - libTime} ms");
    }

    private long LogLibraryLoadTime(Stopwatch timer) {
        if (timer is null)
            return 0;

        var libTime = timer.ElapsedMilliseconds;

        Log(timer, $"Loaded the Standard Library in {libTime} ms");

        return libTime;
    }

    private void LogCompilationTime(Stopwatch timer) {
        if (timer is null)
            return;

        Log(timer, $"Total compilation time: {timer.ElapsedMilliseconds} ms");
    }

    private void Log(Stopwatch timer, string message) {
        if (state.time) {
            timer.Stop();
            diagnostics.Push(new BelteDiagnostic(DiagnosticSeverity.Debug, message));
            timer.Start();
        }
    }

    private void InternalInterpreterStart(Action<object> wrapper) {
        ValueWrapper<bool> abort = false;

        void CtrlCHandler(object sender, ConsoleCancelEventArgs args) {
            abort.Value = true;
            args.Cancel = true;
        }

        Console.CancelKeyPress += new ConsoleCancelEventHandler(CtrlCHandler);

        var wrapperReference = new ParameterizedThreadStart(wrapper);
        var wrapperThread = new Thread(wrapperReference) {
            Name = "Compiler.InternalInterpreterStart"
        };

        wrapperThread.Start(abort);
        wrapperThread.Join();
    }

    private ParseOptions CreateParseOptions() {
        if (state.debugMode)
            return new ParseOptions(["DEBUG"]);
        else
            return new ParseOptions(["RELEASE"]);
    }
}

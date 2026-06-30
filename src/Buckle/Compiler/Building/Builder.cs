using System.Collections.Generic;
using Diagnostics;

namespace Buckle.Building;

/// <summary>
/// Builds information used to create a compilation.
/// </summary>
public sealed class Builder {
    private readonly DiagnosticOptions _globalDiagnosticOptions;
    private readonly DiagnosticOptions _currentDiagnosticOptions;
    private DiagnosticFlagMode _diagnosticFlagMode;

    /// <summary>
    /// Build mode of the compilation (default <see cref="BuildMode.Execute" />)
    /// </summary>
    public BuildMode buildMode;

    /// <summary>
    /// Output kind of the compilation (default <see cref="OutputKind.ConsoleApplication" />)
    /// </summary>
    public OutputKind outputKind;

    /// <summary>
    /// If to produce debug symbols and not perform certain optimizations (default false)
    /// </summary>
    public bool debugBuild;

    /// <summary>
    /// If to reference the .NET core libraries (default false)
    /// </summary>
    public bool includeStdLib;

    public Builder() {
        buildMode = BuildMode.Execute;
        outputKind = OutputKind.ConsoleApplication;
        inputs = [];
        refs = [];
        deps = [];
        l = 0;
        maxCores = 0;
        debugBuild = false;
        includeStdLib = true;
        _diagnosticFlagMode = DiagnosticFlagMode.Global;
        _globalDiagnosticOptions = new();
        _currentDiagnosticOptions = new();
    }

    public List<(string, InputOptions, DiagnosticOptions)> inputs { get; }

    public List<(string, RefOptions)> refs { get; }

    public List<(string, string, DepOptions)> deps { get; }

    public string output { get; private set; }

    public int l { get; private set; }

    public VerboseMode verboseMode { get; private set; }

    public string vPath { get; private set; }

    public int maxCores { get; private set; }

    public string entryName { get; private set; }

    public DiagnosticOptions diagnosticOptions => _globalDiagnosticOptions;

    public void AddInput(string path) {
        if (_diagnosticFlagMode == DiagnosticFlagMode.Global)
            inputs.Add((path, InputOptions.None, _globalDiagnosticOptions));
        else
            inputs.Add((path, InputOptions.None, _currentDiagnosticOptions.Copy()));
    }

    public void AddInput(string path, InputOptions options) {
        if (_diagnosticFlagMode == DiagnosticFlagMode.Global)
            inputs.Add((path, options, _globalDiagnosticOptions));
        else
            inputs.Add((path, options, _currentDiagnosticOptions.Copy()));
    }

    public void IncludeNETSDK() {
        l = 2;
    }

    public void SetOutput(string path) {
        output = path;
    }

    public void AddRef(string path, RefOptions options = RefOptions.Copy) {
        refs.Add((path, options));
    }

    public void AddDep(string path, DepOptions options = default) {
        deps.Add((path, null, options));
    }

    public void AddDep(string path, string filter, DepOptions options = default) {
        deps.Add((path, filter, options));
    }

    public void SetVerboseMode(VerboseMode mode) {
        verboseMode = mode;
    }

    public void SetVerboseArtifactPath(string path) {
        vPath = path;
    }

    public void SetEntryTypeName(string name) {
        entryName = name;
    }

    public void SetMaxCores(int coreCount) {
        maxCores = coreCount;
    }

    public void ExcludeWarnings(string[] codes) {
        if (_diagnosticFlagMode == DiagnosticFlagMode.Global)
            _globalDiagnosticOptions.wexcludes.AddRange(codes);
        else
            _currentDiagnosticOptions.wexcludes.AddRange(codes);
    }

    public void IncludeWarnings(string[] codes) {
        if (_diagnosticFlagMode == DiagnosticFlagMode.Global)
            _globalDiagnosticOptions.wincludes.AddRange(codes);
        else
            _currentDiagnosticOptions.wincludes.AddRange(codes);
    }

    public void ExcludeWarningsAsErrors(string[] codes) {
        if (_diagnosticFlagMode == DiagnosticFlagMode.Global)
            _globalDiagnosticOptions.werrexcludes.AddRange(codes);
        else
            _currentDiagnosticOptions.werrexcludes.AddRange(codes);
    }

    public void IncludeWarningsAsErrors(string[] codes) {
        if (_diagnosticFlagMode == DiagnosticFlagMode.Global)
            _globalDiagnosticOptions.werrincludes.AddRange(codes);
        else
            _currentDiagnosticOptions.werrincludes.AddRange(codes);
    }

    public void IncludeWarningsAsErrors(int warningLevel = 2) {
        if (_diagnosticFlagMode == DiagnosticFlagMode.Global) {
            _globalDiagnosticOptions.warningsAsErrors = true;
            _globalDiagnosticOptions.wErrorLevel = warningLevel;
        } else {
            _currentDiagnosticOptions.warningsAsErrors = true;
            _currentDiagnosticOptions.warningLevel = warningLevel;
        }
    }

    public void SetWarningLevel(int level) {
        if (_diagnosticFlagMode == DiagnosticFlagMode.Global)
            _globalDiagnosticOptions.warningLevel = level;
        else
            _currentDiagnosticOptions.warningLevel = level;
    }

    public void SetDiagnosticSeverity(DiagnosticSeverity severity) {
        if (_diagnosticFlagMode == DiagnosticFlagMode.Global)
            _globalDiagnosticOptions.severity = severity;
        else
            _currentDiagnosticOptions.severity = severity;
    }

    public void SetDiagnosticFlagMode(DiagnosticFlagMode mode) {
        _diagnosticFlagMode = mode;
    }
}

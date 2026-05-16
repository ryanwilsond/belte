using System.Collections.Generic;
using Diagnostics;

namespace Buckle.Building;

public sealed class Builder {
    private readonly DiagnosticOptions _globalDiagnosticOptions;
    private readonly DiagnosticOptions _currentDiagnosticOptions;
    private DiagnosticFlagMode _diagnosticFlagMode;

    public BuildMode buildMode;
    public OutputKind outputKind;

    public Builder() {
        buildMode = BuildMode.Execute;
        outputKind = OutputKind.ConsoleApplication;
        inputs = [];
        refs = [];
        l = 0;
        maxCores = 0;
        _diagnosticFlagMode = DiagnosticFlagMode.Global;
        _globalDiagnosticOptions = new();
        _currentDiagnosticOptions = new();
    }

    public List<(string, InputOptions, DiagnosticOptions)> inputs { get; }

    public List<(string, RefOptions)> refs { get; }

    public string output { get; private set; }

    public int l { get; private set; }

    public VerboseMode verboseMode { get; private set; }

    public int maxCores { get; private set; }

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

    public void AddRef(string path, RefOptions options) {
        refs.Add((path, options));
    }

    public void SetVerboseMode(VerboseMode mode) {
        verboseMode = mode;
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

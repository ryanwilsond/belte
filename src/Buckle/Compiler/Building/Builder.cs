using System.Collections.Generic;

namespace Buckle.Building;

public sealed class Builder {
    public BuildMode buildMode;
    public OutputKind outputKind;

    public Builder() {
        buildMode = BuildMode.Execute;
        outputKind = OutputKind.ConsoleApplication;
        inputs = [];
        refs = [];
        wincludes = [];
        wexcludes = [];
        l = 0;
        maxCores = 0;
        warningLevel = 1;
    }

    public List<(string, InputOptions)> inputs { get; }

    public List<(string, RefOptions)> refs { get; }

    public string output { get; private set; }

    public int l { get; private set; }

    public VerboseMode verboseMode { get; private set; }

    public int maxCores { get; private set; }

    public List<string> wincludes { get; private set; }

    public List<string> wexcludes { get; private set; }

    public int warningLevel { get; private set; }

    public void AddInput(string path) {
        inputs.Add((path, InputOptions.None));
    }

    public void AddInput(string path, InputOptions options) {
        inputs.Add((path, options));
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
        wexcludes.AddRange(codes);
    }

    public void IncludeWarnings(string[] codes) {
        wincludes.AddRange(codes);
    }

    public void SetWarningLevel(int level) {
        warningLevel = level;
    }
}

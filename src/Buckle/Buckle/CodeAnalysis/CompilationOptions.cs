using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis;

/// <summary>
/// Options and flags specific to a compilation.
/// </summary>
internal struct CompilationOptions {
    internal CompilationOptions(BuildMode buildMode, bool isScript, bool enableOutput) {
        topLevelBinderFlags = BinderFlags.None;
        this.buildMode = buildMode;
        this.isScript = isScript;
        isTranspiling = buildMode == BuildMode.CSharpTranspile;
        this.enableOutput = enableOutput;
    }

    /// <summary>
    /// Default <see cref="Binder" /> flags for a global scope.
    /// </summary>
    internal BinderFlags topLevelBinderFlags { get; }

    internal BuildMode buildMode { get; }

    /// <summary>
    /// If the compilation is a script instead of a full compilation.
    /// </summary>
    internal bool isScript { get; set; }

    /// <summary>
    /// If the <see cref="buildMode" /> is a transpiler.
    /// </summary>
    internal bool isTranspiling { get; }

    /// <summary>
    /// If output should be produced.
    /// </summary>
    internal bool enableOutput { get; }
}

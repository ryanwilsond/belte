using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis;

/// <summary>
/// Options and flags specific to a compilation.
/// </summary>
public struct CompilationOptions {
    /// <summary>
    /// Specifies options and flags to a housing <see cref="Compilation" />.
    /// </summary>
    /// <param name="buildMode">See <see cref="buildMode" />.</param>
    /// <param name="projectType">See <see cref="ProjectType" />.</param>
    /// <param name="arguments">Arguments to be passed to the program at runtime.</param>
    /// <param name="isScript">
    /// If the housing <see cref="Compilation" /> is a partial compilation, that will not be emitted.
    /// </param>
    /// <param name="enableOutput">If output is enabled, otherwise only code checking is performed.</param>
    /// <param name="isLibrary">If the code is a library versus executable.</param>
    public CompilationOptions(
        BuildMode buildMode,
        ProjectType projectType,
        string[] arguments,
        bool isScript,
        bool enableOutput,
        bool isLibrary = false) {
        topLevelBinderFlags = BinderFlags.None;
        this.buildMode = buildMode;
        this.projectType = projectType;
        this.arguments = arguments;
        this.isScript = isScript;
        isTranspiling = buildMode == BuildMode.CSharpTranspile;
        this.enableOutput = enableOutput;
        this.isLibrary = isLibrary;
    }

    /// <summary>
    /// Default <see cref="Binder" /> flags for a global scope.
    /// </summary>
    internal BinderFlags topLevelBinderFlags { get; }

    /// <summary>
    /// See <see cref="BuildMode" />.
    /// </summary>
    internal BuildMode buildMode { get; }

    /// <summary>
    /// See <see cref="ProjectType" />.
    /// </summary>
    internal ProjectType projectType { get; }

    /// <summary>
    /// Arguments to be passed to the program at runtime.
    /// </summary>
    internal string[] arguments { get; }

    /// <summary>
    /// If the compilation is a script instead of a full compilation.
    /// </summary>
    internal bool isScript { get; set; }

    /// <summary>
    /// If the <see cref="buildMode" /> is a transpiler.
    /// </summary>
    internal bool isTranspiling { get; }

    /// <summary>
    /// If this <see cref="Compilation" /> is apart of the STL.
    /// </summary>
    internal bool isLibrary { get; }

    /// <summary>
    /// If output should be produced.
    /// </summary>
    internal bool enableOutput { get; }
}

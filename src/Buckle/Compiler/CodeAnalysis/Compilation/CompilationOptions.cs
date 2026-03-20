using Buckle.CodeAnalysis.Binding;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

public struct CompilationOptions {
    public CompilationOptions(
        BuildMode buildMode,
        OutputKind outputKind = OutputKind.ConsoleApplication,
        string[] arguments = null,
        bool isScript = false,
        bool enableOutput = true,
        string[] references = null) {
        topLevelBinderFlags = BinderFlags.None;
        this.buildMode = buildMode;
        this.outputKind = outputKind;
        this.arguments = arguments ?? [];
        this.isScript = isScript;
        isTranspiling = buildMode == BuildMode.CSharpTranspile;
        this.enableOutput = enableOutput;
        this.references = references ?? [];
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
    /// See <see cref="OutputKind" />.
    /// </summary>
    internal OutputKind outputKind { get; }

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
    /// If output should be produced.
    /// </summary>
    internal bool enableOutput { get; }

    internal string[] references { get; }

    internal string cryptoKeyFile { get; }

    internal string cryptoKeyContainer { get; }

    internal bool publicSign { get; }

    internal StrongNameProvider strongNameProvider { get; }
}

using Buckle.CodeAnalysis.Binding;
using Buckle.Diagnostics;
using Diagnostics;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed class NativeEmitter {
    /// <summary>
    /// Emits a program to a native assembly.
    /// </summary>
    /// <param name="program"><see cref="BoundProgram" /> to emit.</param>
    /// <param name="outputPath">Where to put the emitted assembly.</param>
    /// <param name="link">Whether or not to link the assembles.</param>
    /// <returns>Diagnostics.</returns>
    internal static BelteDiagnosticQueue Emit(BoundProgram program, string outputPath, CompilerStage finishStage) {
        if (program.diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return program.diagnostics;

        program.diagnostics.Push(Warning.Unsupported.Assembling());

        if (finishStage == CompilerStage.Linked)
            program.diagnostics.Push(Warning.Unsupported.Linking());

        var emitter = new NativeEmitter();

        // TODO

        return program.diagnostics;
    }
}

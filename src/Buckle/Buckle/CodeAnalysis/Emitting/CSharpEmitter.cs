using Buckle.CodeAnalysis.Binding;
using Buckle.Diagnostics;
using Diagnostics;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed class CSharpEmitter {
    /// <summary>
    /// Emits a program to a C# source.
    /// </summary>
    /// <param name="program"><see cref="BoundProgram" /> to emit.</param>
    /// <param name="outputPath">Where to put the emitted assembly.</param>
    /// <returns>Diagnostics.</returns>
    internal static BelteDiagnosticQueue Emit(BoundProgram program, string outputPath) {
        if (program.diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return program.diagnostics;

        var emitter = new CSharpEmitter();

        // TODO

        return program.diagnostics;
    }
}

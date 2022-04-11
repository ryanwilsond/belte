
using System;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Emitting {
    internal static class Emitter {
        internal static DiagnosticQueue Emit(
            BoundProgram program, string moduleName, string[] references, string outputPath) {
            var diagnostics = new DiagnosticQueue();
            diagnostics.Move(program.diagnostics);

            return diagnostics;
        }
    }
}

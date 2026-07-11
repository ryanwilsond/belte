using System;
using Buckle.CodeAnalysis.Emitting;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Evaluating;

// TODO Run CIL directly instead of using the Evaluator
internal sealed class Emulator : ILEmitter {
    internal Emulator(BoundProgram program, string[] arguments, BelteDiagnosticQueue diagnostics)
        : base(
            program,
            assemblySimpleName: "EmulatingAssembly",
            debugMode: program.compilation.options.optimizationLevel == OptimizationLevel.Debug,
            reduced: program.compilation.options.noStdLib,
            diagnostics) {

    }

    internal object Emulate(bool verbose, bool logTime, string verbosePath, bool noArtifacts) {
        EmitInternal();

        var entryPoint = _assemblyDefinition.EntryPoint;

        Console.WriteLine("Emulating");

        return null;
    }
}

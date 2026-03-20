using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class LocalFunctionRewriter {
    [DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
    internal sealed class ClosureEnvironment {
        internal readonly SetWithInsertionOrder<Symbol> capturedVariables;
        internal readonly bool isStruct;

        internal ClosureEnvironment parent;
        internal SynthesizedClosureEnvironment synthesizedEnvironment;

        internal ClosureEnvironment(IEnumerable<Symbol> capturedVariables, bool isStruct) {
            this.capturedVariables = [.. capturedVariables];
            this.isStruct = isStruct;
        }

        internal bool capturesParent => parent is not null;

        private string GetDebuggerDisplay() {
            var depth = 0;
            var current = parent;

            while (current is not null) {
                depth++;
                current = current.parent;
            }

            return $"{depth}: captures [{string.Join(", ", capturedVariables.Select(v => v.name))}]";
        }
    }
}

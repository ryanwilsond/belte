using System.Collections.Generic;
using Diagnostics;

namespace Buckle.Diagnostics;

public partial class BelteDiagnosticQueue {
    private sealed class DiscardedDiagnosticQueue : BelteDiagnosticQueue {
        internal DiscardedDiagnosticQueue() : base([]) { }

        public override DiagnosticInfo Push<T>(T diagnostic) {
            return diagnostic?.info;
        }

        public override DiagnosticInfo Push(BelteDiagnostic diagnostic) {
            return diagnostic?.info;
        }

        public override void PushRange(BelteDiagnosticQueue diagnostics) { }

        internal override void PushRangeAndFree(BelteDiagnosticQueue diagnostics) {
            diagnostics.Free();
        }

        public override void PushRange(IEnumerable<BelteDiagnostic> diagnostics) { }

        public override void Move(BelteDiagnosticQueue diagnostics) {
            diagnostics.Clear();
        }
    }
}

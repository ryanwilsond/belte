
namespace Buckle.CodeAnalysis.CodeGeneration;

internal sealed partial class ILOptimizer {
    internal readonly struct LocalDefUseSpan {
        internal readonly int start;
        internal readonly int end;

        internal LocalDefUseSpan(int start) : this(start, start) { }

        private LocalDefUseSpan(int start, int end) {
            this.start = start;
            this.end = end;
        }

        internal LocalDefUseSpan WithEnd(int end) {
            return new LocalDefUseSpan(start, end);
        }

        internal bool ConflictsWith(LocalDefUseSpan other) {
            return Contains(other.start) ^ Contains(other.end);
        }

        private bool Contains(int val) {
            return start < val && end > val;
        }

        internal bool ConflictsWithDummy(LocalDefUseSpan dummy) {
            return Includes(dummy.start) ^ Includes(dummy.end);
        }

        private bool Includes(int val) {
            return start <= val && end >= val;
        }

        public override string ToString() {
            return "[" + start + " ," + end + ")";
        }
    }
}

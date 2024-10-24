using System.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal static partial class ConstantEvaluationHelpers {
    [DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
    internal readonly struct FieldInfo {
        internal readonly SourceFieldSymbolWithSyntaxReference field;
        internal readonly bool startsCycle;

        internal FieldInfo(SourceFieldSymbolWithSyntaxReference field, bool startsCycle) {
            this.field = field;
            this.startsCycle = startsCycle;
        }

        private string GetDebuggerDisplay() {
            var value = field.ToString();

            if (startsCycle)
                value += " [cycle]";

            return value;
        }
    }
}

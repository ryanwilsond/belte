using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal static partial class ConstraintsHelpers {
    internal readonly struct CheckConstraintsArgs {
        public readonly Compilation currentCompilation;
        public readonly Conversions conversions;
        public readonly bool includeNullability;
        public readonly TextLocation location;
        public readonly BelteDiagnosticQueue diagnostics;

        internal CheckConstraintsArgs(
            Compilation currentCompilation,
            Conversions conversions,
            TextLocation location,
            BelteDiagnosticQueue diagnostics)
            : this(currentCompilation, conversions, true, location, diagnostics) { }

        internal CheckConstraintsArgs(
            Compilation currentCompilation,
            Conversions conversions,
            bool includeNullability,
            TextLocation location,
            BelteDiagnosticQueue diagnostics) {
            this.currentCompilation = currentCompilation;
            this.conversions = conversions;
            this.includeNullability = includeNullability;
            this.location = location;
            this.diagnostics = diagnostics;
        }
    }
}

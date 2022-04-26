using Buckle.CodeAnalysis.Text;

namespace Buckle {
    internal static class Warning {
        public static Diagnostic UnreachableCode(TextLocation location) {
            string msg = $"unreachable code";
            return new Diagnostic(DiagnosticType.Warning, location, msg);
        }
    }
}

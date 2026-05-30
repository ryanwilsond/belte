using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Diagnostics;

internal class Info {
    internal static BelteDiagnostic StructInefficiency(TextLocation location, int actualSize, int optimalSize) {
        var message = $"struct layout could be reduced from {actualSize} bytes to {optimalSize} bytes by reordering fields";
        return CreateInfo(DiagnosticCode.INF_StructInefficiency, location, message);
    }

    private static BelteDiagnostic CreateInfo(DiagnosticCode code, TextLocation location, string message) {
        return new BelteDiagnostic(InfoInfo(code), location, message);
    }

    private static DiagnosticInfo InfoInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "BU", DiagnosticSeverity.Info);
    }
}

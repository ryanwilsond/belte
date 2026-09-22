using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Diagnostics;

internal class Info {
    internal static BelteDiagnostic StructInefficiency(TextLocation location, TypeSymbol type, int actualSize, int optimalSize) {
        var message = $"'{type.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}': struct layout could be reduced from {actualSize} bytes to {optimalSize} bytes by reordering fields";
        return CreateInfo(DiagnosticCode.INF_StructInefficiency, location, message);
    }

    internal static BelteDiagnostic CompileTimeExpressionThrew(TextLocation location) {
        var message = $"expression threw an uncaught exception when evaluating at compile time; code will be executed at runtime";
        return CreateInfo(DiagnosticCode.INF_CompileTimeExpressionThrew, location, message);
    }

    internal static BelteDiagnostic InvalidCompileTimeExpressionStack(TextLocation location) {
        var message = $"evaluation of compile-time expression caused stack overflow; code will be executed at runtime";
        return CreateInfo(DiagnosticCode.INF_InvalidCompileTimeExpressionStack, location, message);
    }

    private static BelteDiagnostic CreateInfo(DiagnosticCode code, TextLocation location, string message) {
        return new BelteDiagnostic(InfoInfo(code), location, message);
    }

    private static DiagnosticInfo InfoInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "BU", DiagnosticSeverity.Info);
    }
}

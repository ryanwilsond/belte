using System.Linq;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Diagnostics;

namespace Buckle.Diagnostics;

internal static class Warning {
    internal static class Unsupported {
        internal static BelteDiagnostic Assembling() {
            var message = "assembling not supported (yet); skipping";
            return new BelteDiagnostic(WarningInfo(DiagnosticCode.UNS_Assembling), message);
        }

        internal static BelteDiagnostic Linking() {
            var message = "linking not supported (yet); skipping";
            return new BelteDiagnostic(WarningInfo(DiagnosticCode.UNS_Linking), message);
        }
    }

    internal static BelteDiagnostic UnreachableCode(SyntaxNode node) {
        if (node.kind == SyntaxKind.Block) {
            var firstStatement = ((BlockStatementSyntax)node).statements.FirstOrDefault();
            // Report just for non empty blocks.
            if (firstStatement != null)
                return UnreachableCode(firstStatement);

            return null;
        } else if (node.kind == SyntaxKind.EmptyExpression) {
            return null;
        }

        return UnreachableCode(node.location);
    }

    private static DiagnosticInfo WarningInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "BU", DiagnosticType.Warning);
    }

    internal static BelteDiagnostic UnreachableCode(TextLocation location) {
        var message = "unreachable code";
        return new BelteDiagnostic(WarningInfo(DiagnosticCode.WRN_UnreachableCode), location, message);
    }

    internal static BelteDiagnostic AlwaysValue(TextLocation location, object value) {
        var valueString = value == null ? "null" : value.ToString();

        if (value is bool)
            // False -> false
            valueString = valueString.ToLower();

        var message = $"expression will always result to '{valueString}'";

        return new BelteDiagnostic(WarningInfo(DiagnosticCode.WRN_AlwaysValue), location, message);
    }

    internal static BelteDiagnostic NullDeference(TextLocation location) {
        var message = "deference of a possibly null value";
        return new BelteDiagnostic(WarningInfo(DiagnosticCode.WRN_NullDeference), location, message);
    }
}

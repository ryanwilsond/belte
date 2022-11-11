using System.Linq;
using Buckle.CodeAnalysis.Text;
using Buckle.CodeAnalysis.Syntax;
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

    internal static BelteDiagnostic UnreachableCode(Node node) {
        if (node.type == SyntaxType.BLOCK) {
            var firstStatement = ((BlockStatement)node).statements.FirstOrDefault();
            // Report just for non empty blocks.
            if (firstStatement != null)
                return UnreachableCode(firstStatement);

            return null;
        } else if (node.type == SyntaxType.EMPTY_EXPRESSION) {
            return null;
        }

        return UnreachableCode(node.GetChildren().FirstOrDefault().location);
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

        var message = $"expression will always result to '{value}'";
        return new BelteDiagnostic(WarningInfo(DiagnosticCode.WRN_AlwaysValue), location, message);
    }
}

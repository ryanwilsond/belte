using System;
using System.Linq;
using Buckle.CodeAnalysis.Text;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.Diagnostics;

internal static class Warning {
    public static Diagnostic UnreachableCode(Node node) {
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
        return new DiagnosticInfo((int)code, DiagnosticType.Warning);
    }

    internal static Diagnostic UnreachableCode(TextLocation location) {
        var message = "unreachable code";
        return new Diagnostic(DiagnosticType.Warning, location, message);
    }

    internal static Diagnostic AlwaysValue(TextLocation location, object value) {
        var valueString = value.ToString();

        if (value is bool)
            valueString = valueString.ToLower(); // False -> false

        var message = $"expression will always result to '{value}'";
        return new Diagnostic(DiagnosticType.Warning, location, message);
    }
}

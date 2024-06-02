using System.Linq;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Diagnostics;

namespace Buckle.Diagnostics;

/// <summary>
/// All predefined warning messages that can be used by the compiler.
/// The return value for all methods is a new diagnostic that needs to be manually handled or added to a
/// <see cref="DiagnosticQueue<T>" />.
/// The parameters for all methods allow the warning messages to be more dynamic and represent the warning
/// more accurately.
/// </summary>
internal static class Warning {
    /// <summary>
    /// Temporary error messages.
    /// Once the compiler is finished, this class will be unnecessary.
    /// </summary>
    internal static class Unsupported {
        /// <summary>
        /// BU9001. Run `buckle --explain BU9001` on the command line for more info.
        /// </summary>
        internal static BelteDiagnostic Assembling() {
            var message = "assembling not supported (yet); skipping";
            return new BelteDiagnostic(WarningInfo(DiagnosticCode.UNS_Assembling), message);
        }

        /// <summary>
        /// BU9002. Run `buckle --explain BU9002` on the command line for more info.
        /// </summary>
        internal static BelteDiagnostic Linking() {
            var message = "linking not supported (yet); skipping";
            return new BelteDiagnostic(WarningInfo(DiagnosticCode.UNS_Linking), message);
        }
    }

    /// <summary>
    /// BU0001. Run `buckle --explain BU0001` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic AlwaysValue(TextLocation location, object value) {
        var valueString = value is null ? "null" : value.ToString();

        if (value is bool)
            // False -> false
            valueString = valueString.ToLower();

        var message = $"expression will always result to '{valueString}'";

        return new BelteDiagnostic(WarningInfo(DiagnosticCode.WRN_AlwaysValue), location, message);
    }

    /// <summary>
    /// BU0002. Run `buckle --explain BU0002` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic NullDeference(TextLocation location) {
        var message = "deference of a possibly null value";
        return new BelteDiagnostic(WarningInfo(DiagnosticCode.WRN_NullDeference), location, message);
    }

    /// <summary>
    /// BU0026. Run `buckle --explain BU0026` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic UnreachableCode(SyntaxNode node) {
        if (node.kind == SyntaxKind.BlockStatement) {
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

    /// <summary>
    /// BU0026. Run `buckle --explain BU0026` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic UnreachableCode(TextLocation location) {
        var message = "unreachable code";
        return new BelteDiagnostic(WarningInfo(DiagnosticCode.WRN_UnreachableCode), location, message);
    }

    /// <summary>
    /// BU0133. Run `buckle --explain BU0133` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic MemberShadowsNothing(TextLocation location, string signature, string typeName) {
        var message = $"the member '{typeName}.{signature}' does not hide a member; the 'new' keyword is unnecessary";
        return new BelteDiagnostic(WarningInfo(DiagnosticCode.WRN_MemberShadowsNothing), location, message);
    }

    private static DiagnosticInfo WarningInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "BU", DiagnosticSeverity.Warning);
    }
}

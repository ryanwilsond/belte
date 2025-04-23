using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal static class RefKindExtensions {
    internal static string ToParameterDisplayString(this RefKind kind) {
        return kind switch {
            RefKind.Ref => "ref",
            RefKind.RefConstParameter => "ref const",
            _ => throw ExceptionUtilities.UnexpectedValue(kind),
        };
    }
}

using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal static class RefKindExtensions {
    internal static string ToParameterDisplayString(this RefKind kind) {
        return kind switch {
            RefKind.Ref => "ref",
            RefKind.Out => "out",
            RefKind.RefConstParameter => "ref const",
            _ => throw ExceptionUtilities.UnexpectedValue(kind),
        };
    }

    internal static bool IsWritableReference(this RefKind refKind) {
        switch (refKind) {
            case RefKind.Ref:
                return true;
            case RefKind.None:
            case RefKind.RefConstParameter:
                return false;
            default:
                throw ExceptionUtilities.UnexpectedValue(refKind);
        }
    }
}

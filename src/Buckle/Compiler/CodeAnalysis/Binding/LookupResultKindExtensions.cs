
namespace Buckle.CodeAnalysis.Binding;

internal static class LookupResultKindExtensions {
    internal static LookupResultKind WorseResultKind(this LookupResultKind resultKind1, LookupResultKind resultKind2) {
        if (resultKind1 == LookupResultKind.Empty)
            return resultKind2;
        if (resultKind2 == LookupResultKind.Empty)
            return resultKind1;
        if (resultKind1 < resultKind2)
            return resultKind1;
        else
            return resultKind2;
    }
}

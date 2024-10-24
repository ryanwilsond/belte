
namespace Buckle.CodeAnalysis.Symbols;

internal partial class SourceMemberFieldSymbolFromDeclarator {
    private sealed class TypeAndRefKind {
        internal readonly RefKind refKind;
        internal readonly TypeWithAnnotations type;

        internal TypeAndRefKind(RefKind refKind, TypeWithAnnotations type) {
            this.refKind = refKind;
            this.type = type;
        }
    }
}

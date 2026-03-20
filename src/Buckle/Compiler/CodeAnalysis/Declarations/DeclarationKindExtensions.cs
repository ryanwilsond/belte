using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

internal static partial class DeclarationKindExtensions {
    internal static TypeKind ToTypeKind(this DeclarationKind kind) {
        switch (kind) {
            case DeclarationKind.Class:
            case DeclarationKind.Script:
            case DeclarationKind.ImplicitClass:
                return TypeKind.Class;
            case DeclarationKind.Struct:
                return TypeKind.Struct;
            default:
                throw ExceptionUtilities.UnexpectedValue(kind);
        }
    }
}

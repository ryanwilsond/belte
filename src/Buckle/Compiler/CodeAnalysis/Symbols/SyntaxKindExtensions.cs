using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal static class SyntaxKindExtensions {
    internal static TypeKind ToTypeKind(this SyntaxKind kind) {
        return kind switch {
            SyntaxKind.ClassDeclaration => TypeKind.Class,
            SyntaxKind.StructDeclaration => TypeKind.Struct,
            _ => throw ExceptionUtilities.UnexpectedValue(kind),
        };
    }
}

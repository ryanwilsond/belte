using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal static class SyntaxKindExtensions {
    internal static TypeKind ToTypeKind(this SyntaxKind kind) {
        return kind switch {
            SyntaxKind.ClassDeclaration => TypeKind.Class,
            SyntaxKind.FileScopedClassDeclaration => TypeKind.Class,
            SyntaxKind.EnumDeclaration => TypeKind.Enum,
            SyntaxKind.StructDeclaration => TypeKind.Struct,
            SyntaxKind.UnionDeclaration => TypeKind.Struct,
            SyntaxKind.InterfaceDeclaration => TypeKind.Interface,
            _ => throw ExceptionUtilities.UnexpectedValue(kind),
        };
    }
}

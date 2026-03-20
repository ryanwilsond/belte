using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

internal static class SyntaxKindExtensions {
    internal static DeclarationKind ToDeclarationKind(this SyntaxKind syntaxKind) {
        return syntaxKind switch {
            SyntaxKind.ClassDeclaration => DeclarationKind.Class,
            SyntaxKind.StructDeclaration => DeclarationKind.Struct,
            _ => throw ExceptionUtilities.UnexpectedValue(syntaxKind)
        };
    }
}

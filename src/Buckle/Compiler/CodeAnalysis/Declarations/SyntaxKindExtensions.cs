using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

internal static class SyntaxKindExtensions {
    internal static DeclarationKind ToDeclarationKind(this SyntaxKind syntaxKind) {
        switch (syntaxKind) {
            case SyntaxKind.ClassDeclaration:
            case SyntaxKind.FileScopedClassDeclaration:
                return DeclarationKind.Class;
            case SyntaxKind.StructDeclaration:
            case SyntaxKind.UnionDeclaration:
                return DeclarationKind.Struct;
            case SyntaxKind.NamespaceDeclaration:
            case SyntaxKind.FileScopedNamespaceDeclaration:
                return DeclarationKind.Namespace;
            case SyntaxKind.EnumDeclaration:
                return DeclarationKind.Enum;
            case SyntaxKind.InterfaceDeclaration:
                return DeclarationKind.Interface;
            default:
                throw ExceptionUtilities.UnexpectedValue(syntaxKind);
        }
    }
}

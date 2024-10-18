using System;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Syntax;

internal static class SyntaxExtensions {
    internal static void VisitRankSpecifiers<TArg>(
        this TypeSyntax type,
        Action<ArrayRankSpecifierSyntax, TArg> action, in TArg argument) {
        var stack = ArrayBuilder<SyntaxNode>.GetInstance();
        stack.Push(type);

        while (stack.Count > 0) {
            var current = stack.Pop();

            if (current is ArrayRankSpecifierSyntax rankSpecifier) {
                action(rankSpecifier, argument);
                continue;
            } else {
                type = (TypeSyntax)current;
            }

            switch (type.kind) {
                case SyntaxKind.ArrayType:
                    var arrayTypeSyntax = (ArrayTypeSyntax)type;

                    for (var i = arrayTypeSyntax.rankSpecifiers.Count - 1; i >= 0; i--)
                        stack.Push(arrayTypeSyntax.rankSpecifiers[i]);

                    stack.Push(arrayTypeSyntax.elementType);
                    break;
                case SyntaxKind.NonNullableType:
                    var nullableTypeSyntax = (NonNullableTypeSyntax)type;
                    stack.Push(nullableTypeSyntax.type);
                    break;
                case SyntaxKind.ReferenceType:
                    var referenceTypeSyntax = (ReferenceTypeSyntax)type;
                    stack.Push(referenceTypeSyntax.type);
                    break;
                case SyntaxKind.TemplateName:
                    var templateNameSyntax = (TemplateNameSyntax)type;

                    for (var i = templateNameSyntax.templateArgumentList.arguments.Count - 1; i >= 0; i--)
                        stack.Push(templateNameSyntax.templateArgumentList.arguments[i]);

                    break;
                case SyntaxKind.QualifiedName:
                    var qualifiedNameSyntax = (QualifiedNameSyntax)type;
                    stack.Push(qualifiedNameSyntax.right);
                    stack.Push(qualifiedNameSyntax.left);
                    break;
                case SyntaxKind.IdentifierName:
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(type.kind);
            }
        }

        stack.Free();
    }
}

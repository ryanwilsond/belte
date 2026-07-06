using System;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class EarlyWellKnownAttributeBinder : Binder {
    internal EarlyWellKnownAttributeBinder(Binder enclosing)
        : base(enclosing, enclosing.flags | BinderFlags.EarlyAttributeBinding) { }

    internal (AttributeData, BoundAttribute) GetAttribute(
        AttributeSyntax node, NamedTypeSymbol boundAttributeType,
        Action<AttributeSyntax> beforeAttributePartBound,
        Action<AttributeSyntax> afterAttributePartBound,
        out bool generatedDiagnostics) {
        var dummyDiagnosticBag = BelteDiagnosticQueue.GetInstance();

        var result = GetAttribute(
            node,
            boundAttributeType,
            beforeAttributePartBound,
            afterAttributePartBound,
            dummyDiagnosticBag
        );

        generatedDiagnostics = dummyDiagnosticBag.Any();
        dummyDiagnosticBag.Free();
        return result;
    }

    internal static bool CanBeValidAttributeArgument(ExpressionSyntax node) {
        switch (node.kind) {
            case SyntaxKind.ObjectCreationExpression:
            case SyntaxKind.SizeOfExpression:
            case SyntaxKind.TypeOfExpression:
            case SyntaxKind.LiteralExpression:
            case SyntaxKind.ExtendedLiteralExpression:
            case SyntaxKind.InterpolatedStringExpression:
            case SyntaxKind.IdentifierName:
            case SyntaxKind.TemplateName:
            case SyntaxKind.AliasQualifiedName:
            case SyntaxKind.QualifiedName:
            case SyntaxKind.MemberAccessExpression:
            case SyntaxKind.ParenthesizedExpression:
            case SyntaxKind.CastExpression:
            case SyntaxKind.DefaultExpression:
            case SyntaxKind.UnaryExpression:
            case SyntaxKind.BinaryExpression:
            case SyntaxKind.CallExpression:
            case SyntaxKind.TernaryExpression:
                return true;
            default:
                return false;
        }
    }
}

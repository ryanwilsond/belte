using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using static Buckle.CodeAnalysis.Binding.BoundFactory;
using static Buckle.CodeAnalysis.Symbols.SymbolUtilities;

namespace Buckle.Utilities;

internal static class LibraryUtilities {
    internal static ClassSymbol StaticClass(string name, ImmutableArray<Symbol> members) {
        return new ClassSymbol(
            [],
            members,
            [],
            CreateDeclaration(name),
            DeclarationModifiers.Static
        );
    }

    internal static ClassSymbol Class(string name, ImmutableArray<Symbol> members) {
        return new ClassSymbol(
            [],
            members,
            [],
            CreateDeclaration(name),
            DeclarationModifiers.None
        );
    }

    internal static MethodSymbol Constructor(List<(string, BoundType)> parameters) {
        return new MethodSymbol(
            WellKnownMemberNames.InstanceConstructorName,
            CreateParameterList(parameters),
            BoundType.Void,
            modifiers: DeclarationModifiers.None
        );
    }

    internal static BoundStatement FieldInitializer(
        TypeSymbol containing,
        FieldSymbol field,
        ParameterSymbol initializer) {
        return Statement(
            Assignment(
                MemberAccess(
                    new BoundThisExpression(new BoundType(containing, isReference: true)),
                    new BoundVariableExpression(field)
                ),
                new BoundVariableExpression(initializer)
            )
        );
    }

    internal static MethodSymbol StaticMethod(string name, BoundType type, List<(string, BoundType)> parameters) {
        return new MethodSymbol(
            name,
            CreateParameterList(parameters),
            type,
            modifiers: DeclarationModifiers.Static
        );
    }

    internal static FieldSymbol Field(string name, BoundType type) {
        return new FieldSymbol(
            name,
            type,
            null,
            DeclarationModifiers.None
        );
    }

    internal static FieldSymbol Constexpr(string name, BoundType type, object value) {
        return new FieldSymbol(
            name,
            BoundType.CopyWith(type, isConstantExpression: true),
            new BoundConstant(value),
            DeclarationModifiers.Constexpr
        );
    }

    internal static BoundType Nullable(TypeSymbol type) {
        return new BoundType(type, isNullable: true);
    }

    private static ClassDeclarationSyntax CreateDeclaration(string name) {
        return (ClassDeclarationSyntax)CodeAnalysis.Syntax.InternalSyntax.SyntaxFactory.ClassDeclaration(
            null, // Attributes
            null, // Modifiers
            CodeAnalysis.Syntax.InternalSyntax.SyntaxFactory.Token(SyntaxKind.ClassKeyword),
            CodeAnalysis.Syntax.InternalSyntax.SyntaxFactory.Token(SyntaxKind.IdentifierToken, name),
            CodeAnalysis.Syntax.InternalSyntax.SyntaxFactory.Token(SyntaxKind.OpenBraceToken),
            null, // Members
            CodeAnalysis.Syntax.InternalSyntax.SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
        ).CreateRed();
    }

    private static ImmutableArray<ParameterSymbol> CreateParameterList(List<(string, BoundType)> parameters) {
        var builder = ImmutableArray.CreateBuilder<ParameterSymbol>(parameters.Count);

        for (var i = 0; i < parameters.Count; i++) {
            builder.Add(new ParameterSymbol(
                parameters[i].Item1,
                parameters[i].Item2,
                i,
                NoDefault
            ));
        }

        return builder.ToImmutable();
    }
}

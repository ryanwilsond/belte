
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.Libraries;

internal sealed class CorLibrary {
    internal static class ObjectMembers {
        internal static ObjectMembers() {

        }

        internal static MethodSymbol Constructor => Object.members[0] as MethodSymbol;
        internal static new MethodSymbol ToString => Object.members[1] as MethodSymbol;
        internal static new MethodSymbol Equals => Object.members.ElementAtOrDefault(2) as MethodSymbol;
        internal static new MethodSymbol ReferenceEquals => Object.members.ElementAtOrDefault(3) as MethodSymbol;
        internal static MethodSymbol op_Equality => Object.members.ElementAtOrDefault(4) as MethodSymbol;
        internal static MethodSymbol op_Inequality => Object.members.ElementAtOrDefault(5) as MethodSymbol;
        internal static new MethodSymbol GetHashCode => Object.members.ElementAtOrDefault(6) as MethodSymbol;
    }

    internal static ClassSymbol Object = Class("Object",
        [
    /* 0 */ Constructor([], Accessibility.Protected),
    /* 1 */ Method(
                "ToString",
                BoundType.NullableString,
                [],
                DeclarationModifiers.Virtual,
                Accessibility.Public,
                MethodDeclaration(
                    null,
                    TokenList(Token(SyntaxKind.VirtualKeyword)),
                    IdentifierName("string"),
                    Identifier("ToString"),
                    TemplateParameterList(),
                    ParameterList(
                        Token(SyntaxKind.OpenParenToken),
                        SeparatedList<ParameterSyntax>(),
                        Token(SyntaxKind.CloseParenToken)
                    ),
                    ConstraintClauseList(),
                    Block(Return(Literal(""))),
                    Token(SyntaxKind.SemicolonToken)
                )
            ),
        ]
    );

    internal NamedTypeSymbol GetSpecialType(SpecialType specialType) {
        switch (specialType) {
            case SpecialType.String:
                return TypeSymbol.String;
        }
    }
}


using Buckle.CodeAnalysis.Symbols;

namespace Buckle.Libraries;

internal sealed class CorLibrary {
    private static readonly CorLibrary Instance = new CorLibrary();

    private CorLibrary() { }

    private static readonly TypeSymbol Error = new PrimitiveTypeSymbol("?", SpecialType.None);

    private static readonly TypeSymbol Int = new PrimitiveTypeSymbol("int", SpecialType.Int);

    private static readonly TypeSymbol Decimal = new PrimitiveTypeSymbol("decimal", SpecialType.Decimal);

    private static readonly TypeSymbol Bool = new PrimitiveTypeSymbol("bool", SpecialType.Bool);

    private static readonly TypeSymbol String = new PrimitiveTypeSymbol("string", SpecialType.String);

    private static readonly TypeSymbol Char = new PrimitiveTypeSymbol("char", SpecialType.Char);

    private static readonly TypeSymbol Any = new PrimitiveTypeSymbol("any", SpecialType.Any);

    private static readonly TypeSymbol Void = new PrimitiveTypeSymbol("void", SpecialType.Void);

    private static readonly TypeSymbol Type = new PrimitiveTypeSymbol("type", SpecialType.Type);

    private static readonly TypeSymbol Func = new PrimitiveTypeSymbol("Func", SpecialType.Func);

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

    internal static NamedTypeSymbol GetSpecialType(SpecialType specialType) {
        return Instance.GetSpecialType(specialType);
    }

    /// <summary>
    /// Assumes the type of a value.
    /// </summary>
    internal static TypeSymbol Assume(object value) {
        return Instance.Assume(value);
    }

    private NamedTypeSymbol GetSpecialType(SpecialType specialType) {
        switch (specialType) {
            case SpecialType.None:
                return null;
            case SpecialType.String:
                return TypeSymbol.String;
        }
    }

    private TypeSymbol Assume(object value) {
        if (value is bool) return Bool;
        if (value is int) return Int;
        if (value is string) return String;
        if (value is char) return Char;
        if (value is double) return Decimal;
        if (value is TypeSymbol) return Type;

        throw new BelteInternalException($"Assume: unexpected literal '{value}' of type '{value.GetType()}'");
    }
}

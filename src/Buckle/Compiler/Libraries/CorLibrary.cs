
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.Libraries;

internal sealed class CorLibrary {
    /// <summary>
    /// Error type (meaning something went wrong, not an actual type).
    /// </summary>
    internal static readonly TypeSymbol Error = new PrimitiveTypeSymbol("?", SpecialType.None);

    /// <summary>
    /// Integer type (any whole number, signed).
    /// </summary>
    internal static readonly TypeSymbol Int = new PrimitiveTypeSymbol("int", SpecialType.Int);

    /// <summary>
    /// Decimal type (any floating point number, precision TBD).
    /// </summary>
    internal static readonly TypeSymbol Decimal = new PrimitiveTypeSymbol("decimal", SpecialType.Decimal);

    /// <summary>
    /// Boolean type (true/false).
    /// </summary>
    internal static readonly TypeSymbol Bool = new PrimitiveTypeSymbol("bool", SpecialType.Bool);

    /// <summary>
    /// String type.
    /// </summary>
    internal static readonly TypeSymbol String = new PrimitiveTypeSymbol("string", SpecialType.String);

    /// <summary>
    /// Character type.
    /// </summary>
    internal static readonly TypeSymbol Char = new PrimitiveTypeSymbol("char", SpecialType.Char);

    /// <summary>
    /// Any type (effectively the object type).
    /// </summary>
    internal static readonly TypeSymbol Any = new PrimitiveTypeSymbol("any", SpecialType.Any);

    /// <summary>
    /// Void type (lack of type, exclusively used in method declarations).
    /// </summary>
    internal static readonly TypeSymbol Void = new PrimitiveTypeSymbol("void", SpecialType.Void);

    /// <summary>
    /// Type type (contains a type, e.g. type myVar = typeof(int) ).
    /// </summary>
    internal static readonly TypeSymbol Type = new PrimitiveTypeSymbol("type", SpecialType.Type);

    /// <summary>
    /// Type used to represent function (or method) signatures. Purely an implementation detail, cannot be used
    /// by users.
    /// </summary>
    internal static readonly TypeSymbol Func = new PrimitiveTypeSymbol("Func", SpecialType.Func);

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

    /// <summary>
    /// Assumes the type of a value.
    /// </summary>
    internal static TypeSymbol Assume(object value) {
        if (value is bool) return Bool;
        if (value is int) return Int;
        if (value is string) return String;
        if (value is char) return Char;
        if (value is double) return Decimal;
        if (value is TypeSymbol) return Type;

        throw new BelteInternalException($"Assume: unexpected literal '{value}' of type '{value.GetType()}'");
    }
}

using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using static Buckle.CodeAnalysis.Syntax.SyntaxFactory;
using static Buckle.Utilities.LibraryUtilities;

namespace Buckle.Libraries.Standard;

// ? Both types are defined in this one file because Console relies on Object, so static member ordering matters

internal static partial class StandardLibrary {
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
                    ParameterList(
                        Token(SyntaxKind.OpenParenToken),
                        SeparatedList<ParameterSyntax>(),
                        Token(SyntaxKind.CloseParenToken)
                    ),
                    Block(Return(Literal(""))),
                    Token(SyntaxKind.SemicolonToken)
                )
            ),
        ]
    );

    internal static ClassSymbol Console = StaticClass("Console",
        [
    /* 0 */ StaticClass("Color", [
                Constexpr("Black", BoundType.Int, 0),
                Constexpr("DarkBlue", BoundType.Int, 1),
                Constexpr("DarkGreen", BoundType.Int, 2),
                Constexpr("DarkCyan", BoundType.Int, 3),
                Constexpr("DarkRed", BoundType.Int, 4),
                Constexpr("DarkMagenta", BoundType.Int, 5),
                Constexpr("DarkYellow", BoundType.Int, 6),
                Constexpr("Gray", BoundType.Int, 7),
                Constexpr("DarkGray", BoundType.Int, 8),
                Constexpr("Blue", BoundType.Int, 9),
                Constexpr("Green", BoundType.Int, 10),
                Constexpr("Cyan", BoundType.Int, 11),
                Constexpr("Red", BoundType.Int, 12),
                Constexpr("Magenta", BoundType.Int, 13),
                Constexpr("Yellow", BoundType.Int, 14),
                Constexpr("White", BoundType.Int, 15)
            ]),
    /* 1 */ StaticMethod("PrintLine", BoundType.Void, [
                        ("message", BoundType.NullableString)
            ]),
    /* 2 */ StaticMethod("PrintLine", BoundType.Void, [
                ("value", BoundType.NullableAny)
            ]),
    /* 3 */ StaticMethod("PrintLine", BoundType.Void, [
                ("value", new BoundType(Object, isNullable: true))
            ]),
    /* 4 */ StaticMethod("PrintLine", BoundType.Void, []),
    /* 5 */ StaticMethod("Print", BoundType.Void, [
                ("message", BoundType.NullableString)
            ]),
    /* 6 */ StaticMethod("Print", BoundType.Void, [
                ("value", BoundType.NullableAny)
            ]),
    /* 7 */ StaticMethod("Print", BoundType.Void, [
                ("value", new BoundType(Object, isNullable: true))
            ]),
    /* 8 */ StaticMethod("Input", BoundType.String, []),
    /* 9 */ StaticMethod("SetForegroundColor", BoundType.Void, [
                ("color", BoundType.Int)
            ]),
   /* 10 */ StaticMethod("SetBackgroundColor", BoundType.Void, [
                ("color", BoundType.Int)
            ]),
   /* 11 */ StaticMethod("ResetColor", BoundType.Void, []),
   /* 12 */ StaticMethod("GetWidth", BoundType.Int, []),
   /* 13 */ StaticMethod("GetHeight", BoundType.Int, []),
   /* 14 */ StaticMethod("SetCursorPosition", BoundType.Void, [
                ("left", BoundType.NullableInt),
                ("top", BoundType.NullableInt)
            ]),
        ]
    );
}

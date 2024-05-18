using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using static Buckle.Utilities.LibraryUtilities;
using static Buckle.CodeAnalysis.Syntax.SyntaxFactory;

namespace Buckle.Libraries.Standard;

internal static partial class StandardLibrary {
    internal static ClassSymbol Object = Class("Object",
        [
    /* 0 */ Constructor([], Accessibility.Private),
    /* 1 */ Method("ToString", BoundType.NullableString, [], MethodDeclaration(
                null,
                TokenList(Token(SyntaxKind.VirtualKeyword)),
                IdentifierName("string"),
                Identifier("ToString"),
                ParameterList(
                    Token(SyntaxKind.OpenParenToken),
                    SeparatedList<ParameterSyntax>(),
                    Token(SyntaxKind.CloseParenToken)
                ),
                Block(Return(Literal("")))
            )),
        ]
    );
}

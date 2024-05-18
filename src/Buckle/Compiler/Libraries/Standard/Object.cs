using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using static Buckle.Utilities.LibraryUtilities;

namespace Buckle.Libraries.Standard;

internal static partial class StandardLibrary {
    internal static ClassSymbol Object = Class("Object",
        [
    /* 0 */ Constructor([], Accessibility.Private),
    /* 1 */ Method("ToString", BoundType.NullableString, [], SyntaxFactory.MethodDeclaration(
                null,
                null,
                SyntaxFactory.IdentifierName("string"),
                SyntaxFactory.Identifier("ToString"),
                SyntaxFactory.ParameterList(
                    SyntaxFactory.Token(SyntaxKind.OpenParenToken),
                    SyntaxFactory.SeparatedList<ParameterSyntax>(),
                    SyntaxFactory.Token(SyntaxKind.CloseParenToken)
                ),
                SyntaxFactory.Block(SyntaxFactory.Return(SyntaxFactory.Literal("")))
            )),
        ]
    );
}

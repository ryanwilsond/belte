using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class LocalFunctionSymbol : MethodSymbol {
    internal LocalFunctionSymbol(Binder binder, Symbol containingSymbol, LocalFunctionStatementSyntax syntax) : this() {

    }

    internal SyntaxToken identifier => ((LocalDeclarationStatementSyntax)syntaxReference.node).declaration.identifier;
}


using System.Reflection;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class LocalFunctionSymbol : MethodSymbol {
    internal LocalFunctionSymbol(Binder binder, Symbol containingSymbol, LocalFunctionStatementSyntax syntax) : this() {

    }
}

using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class ThisParameterSymbolBase : ParameterSymbol {
    internal const string SymbolName = "this";

    public sealed override string name => SymbolName;

    public sealed override int ordinal => -1;

    internal sealed override SyntaxReference syntaxReference => null;

    internal sealed override ConstantValue explicitDefaultConstantValue => null;

    internal sealed override bool isMetadataOptional => false;

    internal sealed override bool isThis => true;

    internal sealed override bool isImplicitlyDeclared => true;
}

using System.Diagnostics;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SynthesizedAccessorValueParameterSymbol : SourceComplexParameterSymbolBase {
    internal SynthesizedAccessorValueParameterSymbol(SourceMemberMethodSymbol accessor, int ordinal)
        : base(
            accessor,
            ordinal,
            RefKind.None,
            isConst: false,
            isConstExpr: false,
            ValueParameterName,
            syntax: null,
            location: accessor.location,
            scope: ScopedKind.None) {
        Debug.Assert(accessor.locations.Length <= 1);
    }

    internal override bool isImplicitlyDeclared => true;

    private protected override IAttributeTargetSymbol _attributeOwner => (SourceMemberMethodSymbol)containingSymbol;

    internal override OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations() {
        var accessor = (SourceMemberMethodSymbol)containingSymbol;
        return accessor.GetAttributeDeclarations();
    }
}

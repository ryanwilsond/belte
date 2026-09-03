using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class ErrorPropertySymbol : PropertySymbol {
    private readonly Symbol _containingSymbol;
    private readonly TypeWithAnnotations _typeWithAnnotations;
    private readonly string _name;

    internal ErrorPropertySymbol(Symbol containingSymbol, TypeSymbol type, string name) {
        _containingSymbol = containingSymbol;
        _typeWithAnnotations = new TypeWithAnnotations(type);
        _name = name;
    }

    public override string name => _name;

    internal override Symbol containingSymbol => _containingSymbol;

    internal override RefKind refKind => RefKind.None;

    internal override TypeWithAnnotations typeWithAnnotations => _typeWithAnnotations;

    internal override bool hasSpecialName => false;

    internal override MethodSymbol getMethod => null;

    internal override MethodSymbol setMethod => null;

    internal override ImmutableArray<TextLocation> locations => [];

    internal override TextLocation location => null;

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

    internal override SyntaxReference syntaxReference => null;

    internal override Accessibility declaredAccessibility => Accessibility.NotApplicable;

    internal override bool isStatic => false;

    internal override bool isVirtual => false;

    internal override bool isOverride => false;

    internal override bool isAbstract => false;

    internal override bool isSealed => false;

    internal override bool isExtern => false;

    internal override ImmutableArray<ParameterSymbol> parameters => [];

    internal override CallingConvention callingConvention => CallingConvention.Default;

    internal override bool mustCallMethodsDirectly => false;

    internal override ImmutableArray<PropertySymbol> explicitInterfaceImplementations => [];
}

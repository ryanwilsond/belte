using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Libraries;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SynthesizedStaticConstructor : MethodSymbol {
    internal SynthesizedStaticConstructor(NamedTypeSymbol containingType) {
        this.containingType = containingType;
    }

    public override string name => WellKnownMemberNames.StaticConstructorName;

    internal override bool hasSpecialName => true;

    public override ImmutableArray<TypeOrConstant> templateArguments => [];

    public override ImmutableArray<TemplateParameterSymbol> templateParameters => [];

    public override ImmutableArray<BoundExpression> templateConstraints => [];

    internal override int parameterCount => 0;

    public override int arity => 0;

    public override bool returnsVoid => true;

    public override MethodKind methodKind => MethodKind.StaticConstructor;

    internal override ImmutableArray<ParameterSymbol> parameters => [];

    internal override Accessibility declaredAccessibility => Accessibility.Private;

    internal override TextLocation location => null;

    internal override ImmutableArray<TextLocation> locations => [];

    internal override SyntaxReference syntaxReference => null;

    internal override TypeWithAnnotations returnTypeWithAnnotations
        => new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Void));

    internal override Symbol containingSymbol => containingType;

    internal override NamedTypeSymbol containingType { get; }

    internal override bool isSealed => false;

    internal override bool isAbstract => false;

    internal override bool isOverride => false;

    internal override bool isVirtual => false;

    internal override bool isStatic => true;

    internal override bool isExtern => false;

    internal override bool hidesBaseMethodsByName => false;

    internal override bool isDeclaredConst => false;

    public override RefKind refKind => RefKind.None;

    internal override bool hasUnscopedRefAttribute => false;

    internal override CallingConvention callingConvention => CallingConvention.Default;

    internal override bool isMetadataFinal => false;

    internal override LexicalSortKey GetLexicalSortKey() {
        return LexicalSortKey.SynthesizedCCtor;
    }

    internal override DllImportData GetDllImportData() {
        return null;
    }

    internal override bool IsMetadataVirtual(bool forceComplete = false) {
        return false;
    }

    internal override int CalculateLocalSyntaxOffset(int localPosition, SyntaxTree localTree) {
        var containingType = (SourceMemberContainerTypeSymbol)this.containingType;
        return containingType.CalculateSyntaxOffsetInSynthesizedConstructor(localPosition, localTree, isStatic: true);
    }
}

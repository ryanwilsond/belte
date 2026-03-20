using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SynthesizedSubstitutedTemplateParameterSymbol : SubstitutedTemplateParameterSymbol {
    internal SynthesizedSubstitutedTemplateParameterSymbol(
        Symbol owner,
        TemplateMap templateMap,
        TemplateParameterSymbol substitutedFrom,
        int ordinal) : base(owner, templateMap, substitutedFrom, ordinal) { }

    internal override bool isImplicitlyDeclared => true;

    internal override TemplateParameterKind templateParameterKind
        => containingSymbol is MethodSymbol ? TemplateParameterKind.Method : TemplateParameterKind.Type;

    internal override ImmutableArray<AttributeData> GetAttributes() {
        if (containingSymbol is SynthesizedMethodSymbolBase { inheritsBaseMethodAttributes: true })
            return underlyingTemplateParameter.GetAttributes();

        return [];
    }
}

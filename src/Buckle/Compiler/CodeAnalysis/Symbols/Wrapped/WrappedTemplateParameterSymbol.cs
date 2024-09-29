using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class WrappedTemplateParameterSymbol : TemplateParameterSymbol {
    internal WrappedTemplateParameterSymbol(TemplateParameterSymbol underlyingTemplateParameter) {
        this.underlyingTemplateParameter = underlyingTemplateParameter;
    }

    public override string name => underlyingTemplateParameter.name;

    internal TemplateParameterSymbol underlyingTemplateParameter { get; }

    internal override TemplateParameterKind templateParameterKind => underlyingTemplateParameter.templateParameterKind;

    internal override int ordinal => underlyingTemplateParameter.ordinal;

    internal override TypeWithAnnotations underlyingType => underlyingTemplateParameter.underlyingType;

    internal override ConstantValue defaultValue => underlyingTemplateParameter.defaultValue;

    internal override SyntaxReference syntaxReference => underlyingTemplateParameter.syntaxReference;

    internal override void EnsureConstraintsAreResolved() {
        underlyingTemplateParameter.EnsureConstraintsAreResolved();
    }
}

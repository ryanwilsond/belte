using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class WrappedTemplateParameterSymbol : TemplateParameterSymbol {
    internal WrappedTemplateParameterSymbol(TemplateParameterSymbol underlyingTemplateParameter) {
        this.underlyingTemplateParameter = underlyingTemplateParameter;
    }

    public override string name => underlyingTemplateParameter.name;

    internal TemplateParameterSymbol underlyingTemplateParameter { get; }

    internal override TemplateParameterKind templateParameterKind => underlyingTemplateParameter.templateParameterKind;

    internal override int ordinal => underlyingTemplateParameter.ordinal;

    internal override bool hasPrimitiveTypeConstraint => underlyingTemplateParameter.hasPrimitiveTypeConstraint;

    internal override bool hasObjectTypeConstraint => underlyingTemplateParameter.hasObjectTypeConstraint;

    internal override bool hasDefaultConstraint => underlyingTemplateParameter.hasDefaultConstraint;

    internal override bool hasConstructorConstraint => underlyingTemplateParameter.hasConstructorConstraint;

    internal override bool allowsRefLikeType => underlyingTemplateParameter.allowsRefLikeType;

    internal override bool hasNotNullConstraint => underlyingTemplateParameter.hasNotNullConstraint;

    internal override bool isOptional => underlyingTemplateParameter.isOptional;

    internal override bool isValueTypeFromConstraintTypes
        => underlyingTemplateParameter.isValueTypeFromConstraintTypes ||
            CalculateIsPrimitiveTypeFromConstraintTypes(constraintTypes);

    internal override bool isReferenceTypeFromConstraintTypes
        => underlyingTemplateParameter.isReferenceTypeFromConstraintTypes ||
            CalculateIsObjectTypeFromConstraintTypes(constraintTypes);

    internal override bool hasDefaultFromConstraintTypes
        => underlyingTemplateParameter.hasDefaultConstraint ||
            CalculateHasDefaultFromConstraintTypes(constraintTypes);

    internal override bool hasConstructorFromConstraintTypes
        => underlyingTemplateParameter.hasConstructorConstraint ||
            CalculateHasConstructorFromConstraintTypes(constraintTypes);

    internal override TypeWithAnnotations underlyingType => underlyingTemplateParameter.underlyingType;

    internal override TypeOrConstant defaultValue => underlyingTemplateParameter.defaultValue;

    internal override SyntaxReference syntaxReference => underlyingTemplateParameter.syntaxReference;

    internal override TextLocation location => underlyingTemplateParameter.location;

    internal override void EnsureConstraintsAreResolved() {
        underlyingTemplateParameter.EnsureConstraintsAreResolved();
    }

    internal override ImmutableArray<AttributeData> GetAttributes() {
        return underlyingTemplateParameter.GetAttributes();
    }
}

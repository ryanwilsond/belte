using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Libraries;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SynthesizedTemplateParameterSymbol : TemplateParameterSymbol {
    internal SynthesizedTemplateParameterSymbol(Symbol container, TypeWithAnnotations underlyingType, int ordinal) {
        this.underlyingType = underlyingType;
        this.ordinal = ordinal;
        containingSymbol = container;
    }

    internal override TypeWithAnnotations underlyingType { get; }

    internal override TemplateParameterKind templateParameterKind => TemplateParameterKind.Type;

    internal override int ordinal { get; }

    internal override bool hasPrimitiveTypeConstraint => false;

    internal override bool hasObjectTypeConstraint => false;

    internal override bool isPrimitiveTypeFromConstraintTypes => false;

    internal override bool isObjectTypeFromConstraintTypes => false;

    internal override bool hasNotNullConstraint => false;

    internal override bool allowsRefLikeType => false;

    internal override bool isOptional => false;

    internal override TypeOrConstant defaultValue => null;

    internal override Symbol containingSymbol { get; }

    internal override SyntaxReference syntaxReference => null;

    internal override TextLocation location => null;

    internal override void EnsureConstraintsAreResolved() { }

    internal override ImmutableArray<TypeWithAnnotations> GetConstraintTypes(
        ConsList<TemplateParameterSymbol> inProgress) {
        return [];
    }

    internal override TypeSymbol GetDeducedBaseType(ConsList<TemplateParameterSymbol> inProgress) {
        return CorLibrary.GetSpecialType(SpecialType.Object);
    }

    internal override NamedTypeSymbol GetEffectiveBaseClass(ConsList<TemplateParameterSymbol> inProgress) {
        return CorLibrary.GetSpecialType(SpecialType.Object);
    }
}

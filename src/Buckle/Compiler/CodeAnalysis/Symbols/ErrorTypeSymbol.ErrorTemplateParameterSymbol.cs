using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class ErrorTypeSymbol {
    private protected sealed class ErrorTemplateParameterSymbol : TemplateParameterSymbol {
        internal ErrorTemplateParameterSymbol(
            ErrorTypeSymbol container,
            string name,
            int ordinal,
            TypeWithAnnotations underlyingType) {
            containingSymbol = container;
            this.name = name;
            this.ordinal = ordinal;
            this.underlyingType = underlyingType;
        }

        public override string name { get; }

        internal override int ordinal { get; }

        internal override TemplateParameterKind templateParameterKind => TemplateParameterKind.Type;

        internal override Symbol containingSymbol { get; }

        internal override SyntaxReference syntaxReference => null;

        internal override TextLocation location => null;

        internal override bool isImplicitlyDeclared => true;

        internal override TypeWithAnnotations underlyingType { get; }

        internal override TypeOrConstant defaultValue => null;

        internal override bool hasObjectTypeConstraint => false;

        internal override bool hasPrimitiveTypeConstraint => false;

        internal override bool isObjectTypeFromConstraintTypes => false;

        internal override bool isPrimitiveTypeFromConstraintTypes => false;

        internal override bool hasNotNullConstraint => false;

        internal override bool allowsRefLikeType => false;

        internal override bool isOptional => false;

        internal override void EnsureConstraintsAreResolved() { }

        internal override ImmutableArray<TypeWithAnnotations> GetConstraintTypes(
            ConsList<TemplateParameterSymbol> inProgress) {
            return [];
        }

        internal override NamedTypeSymbol? GetEffectiveBaseClass(ConsList<TemplateParameterSymbol> inProgress) {
            return null;
        }

        internal override TypeSymbol GetDeducedBaseType(ConsList<TemplateParameterSymbol> inProgress) {
            return null;
        }

        public override int GetHashCode() {
            return Hash.Combine((containingSymbol as ErrorTypeSymbol).GetHashCode(), ordinal);
        }

        internal override bool Equals(TypeSymbol other, TypeCompareKind compareKind) {
            if (ReferenceEquals(this, other))
                return true;

            var otherAsError = other as ErrorTemplateParameterSymbol;

            return otherAsError is not null &&
                otherAsError.ordinal == ordinal &&
                otherAsError.containingType.Equals(containingType, compareKind);
        }
    }
}

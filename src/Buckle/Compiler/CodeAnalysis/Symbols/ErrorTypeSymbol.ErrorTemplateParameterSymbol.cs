using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class ErrorTypeSymbol {
    private protected sealed class ErrorTemplateParameterSymbol : TemplateParameterSymbol {
        internal ErrorTemplateParameterSymbol(ErrorTypeSymbol container, string name, int ordinal) {
            containingSymbol = container;
            this.name = name;
            this.ordinal = ordinal;
        }

        public override string name { get; }

        internal override int ordinal { get; }

        internal override TemplateParameterKind templateParameterKind => TemplateParameterKind.Type;

        internal override Symbol containingSymbol { get; }

        internal override SyntaxReference syntaxReference => null;

        internal override bool isImplicitlyDeclared => true;

        internal override TypeWithAnnotations underlyingType => null;

        internal override ConstantValue defaultValue => null;

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

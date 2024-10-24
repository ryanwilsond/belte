using System.Threading;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SubstitutedFieldSymbol : WrappedFieldSymbol {
    private readonly SubstitutedNamedTypeSymbol _containingType;

    private TypeWithAnnotations _lazyType;

    internal SubstitutedFieldSymbol(SubstitutedNamedTypeSymbol containingType, FieldSymbol substitutedFrom)
        : base(substitutedFrom.originalDefinition) {
        _containingType = containingType;
    }

    internal override Symbol containingSymbol => _containingType;

    internal override NamedTypeSymbol containingType => _containingType;

    internal override FieldSymbol originalDefinition => underlyingField;

    internal override TypeWithAnnotations GetFieldType(ConsList<FieldSymbol> fieldsBeingBound) {
        if (_lazyType is null) {
            var type = _containingType.templateSubstitution.SubstituteType(
                originalDefinition.GetFieldType(fieldsBeingBound)
            );

            Interlocked.CompareExchange(ref _lazyType, type.type, null);
        }

        return _lazyType;
    }

    internal override bool Equals(Symbol obj, TypeCompareKind compareKind) {
        if ((object)this == obj)
            return true;

        return obj is FieldSymbol other &&
            TypeSymbol.Equals(_containingType, other.containingType, compareKind) &&
            originalDefinition == other.originalDefinition;
    }

    public override int GetHashCode() {
        var code = originalDefinition.GetHashCode();
        var containingHashCode = _containingType.GetHashCode();

        if (containingHashCode != originalDefinition.containingType.GetHashCode())
            code = Hash.Combine(containingHashCode, code);

        return code;
    }
}

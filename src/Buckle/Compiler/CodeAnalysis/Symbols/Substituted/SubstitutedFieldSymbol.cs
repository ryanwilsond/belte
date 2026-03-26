using System.Collections.Immutable;
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

    internal override ImmutableArray<AttributeData> GetAttributes() {
        return originalDefinition.GetAttributes();
    }

    // TODO Technically reachable? Need to implement
    // internal override NamedTypeSymbol FixedImplementationType(PEModuleBuilder emitModule) {
    //     // This occurs rarely, if ever.  The scenario would be a generic struct
    //     // containing a fixed-size buffer.  Given the rarity there would be little
    //     // benefit to "optimizing" the performance of this by caching the
    //     // translated implementation type.
    //     return (NamedTypeSymbol)_containingType.TypeSubstitution.SubstituteType(OriginalDefinition.FixedImplementationType(emitModule)).Type;
    // }

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

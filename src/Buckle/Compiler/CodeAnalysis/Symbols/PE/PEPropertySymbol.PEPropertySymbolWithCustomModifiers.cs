using System.Reflection.Metadata;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class PEPropertySymbol {
    private sealed class PEPropertySymbolWithCustomModifiers : PEPropertySymbol {
        // private readonly ImmutableArray<CustomModifier> _refCustomModifiers;

        internal PEPropertySymbolWithCustomModifiers(
            PEModuleSymbol moduleSymbol,
            PENamedTypeSymbol containingType,
            PropertyDefinitionHandle handle,
            PEMethodSymbol getMethod,
            PEMethodSymbol setMethod,
            ParamInfo<TypeSymbol>[] propertyParams,
            MetadataDecoder metadataDecoder)
            : base(
                moduleSymbol,
                containingType,
                handle,
                getMethod,
                setMethod,
                propertyParams,
                metadataDecoder) {
            // var returnInfo = propertyParams[0];
            // _refCustomModifiers = CSharpCustomModifier.Convert(returnInfo.RefCustomModifiers);
        }

        // public override ImmutableArray<CustomModifier> RefCustomModifiers {
        //     get { return _refCustomModifiers; }
        // }
    }
}

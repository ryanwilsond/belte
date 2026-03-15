using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class PEParameterSymbol {
    private sealed class PEParameterSymbolWithCustomModifiers : PEParameterSymbol {
        // private readonly ImmutableArray<CustomModifier> _refCustomModifiers;

        internal PEParameterSymbolWithCustomModifiers(
            PEModuleSymbol moduleSymbol,
            Symbol containingSymbol,
            int ordinal,
            bool isByRef,
            ImmutableArray<ModifierInfo<TypeSymbol>> refCustomModifiers,
            TypeWithAnnotations type,
            ParameterHandle handle,
            Symbol nullableContext,
            bool isReturn,
            out bool isBad)
                : base(moduleSymbol, containingSymbol, ordinal, isByRef, type, handle, nullableContext,
                     refCustomModifiers.NullToEmpty().Length /*+ type.customModifiers.Length*/,
                     isReturn: isReturn, out isBad) {
            // _refCustomModifiers = CustomModifier.Convert(refCustomModifiers);
        }

        // internal override ImmutableArray<CustomModifier> refCustomModifiers {
        //     get {
        //         return _refCustomModifiers;
        //     }
        // }
    }
}

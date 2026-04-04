using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SymbolFactory<ModuleSymbol, TypeSymbol> where TypeSymbol : class {
    internal abstract TypeSymbol GetUnsupportedMetadataTypeSymbol(
        ModuleSymbol moduleSymbol,
        BadImageFormatException exception);

    internal abstract TypeSymbol MakeUnboundIfGeneric(ModuleSymbol moduleSymbol, TypeSymbol type);

    internal abstract TypeSymbol GetSZArrayTypeSymbol(
        ModuleSymbol moduleSymbol,
        TypeSymbol elementType,
        ImmutableArray<ModifierInfo<TypeSymbol>> customModifiers);

    internal abstract TypeSymbol GetMDArrayTypeSymbol(
        ModuleSymbol moduleSymbol,
        int rank,
        TypeSymbol elementType,
        ImmutableArray<ModifierInfo<TypeSymbol>> customModifiers,
        ImmutableArray<int> sizes,
        ImmutableArray<int> lowerBounds);

    internal abstract TypeSymbol SubstituteTypeParameters(
        ModuleSymbol moduleSymbol,
        TypeSymbol generic,
        ImmutableArray<KeyValuePair<TypeSymbol, ImmutableArray<ModifierInfo<TypeSymbol>>>> arguments,
        ImmutableArray<bool> refersToNoPiaLocalType);

    internal abstract TypeSymbol MakePointerTypeSymbol(
        ModuleSymbol moduleSymbol,
        TypeSymbol type,
        ImmutableArray<ModifierInfo<TypeSymbol>> customModifiers);

    internal abstract TypeSymbol MakeFunctionPointerTypeSymbol(
        ModuleSymbol moduleSymbol,
        CallingConvention callingConvention,
        ImmutableArray<ParamInfo<TypeSymbol>> returnAndParamTypes);

    internal abstract TypeSymbol GetSpecialType(ModuleSymbol moduleSymbol, SpecialType specialType);
}

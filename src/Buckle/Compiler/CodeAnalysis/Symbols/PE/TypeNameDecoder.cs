using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class TypeNameDecoder<ModuleSymbol, TypeSymbol>
    where ModuleSymbol : class
    where TypeSymbol : class {
    private readonly SymbolFactory<ModuleSymbol, TypeSymbol> _factory;
    private protected readonly ModuleSymbol _moduleSymbol;

    internal TypeNameDecoder(SymbolFactory<ModuleSymbol, TypeSymbol> factory, ModuleSymbol moduleSymbol) {
        _factory = factory;
        _moduleSymbol = moduleSymbol;
    }

    private protected abstract bool IsContainingAssembly(AssemblyIdentity identity);

    private protected abstract TypeSymbol LookupTopLevelTypeDefSymbol(ref MetadataTypeName emittedName, out bool isNoPiaLocalType);

    private protected abstract TypeSymbol LookupTopLevelTypeDefSymbol(int referencedAssemblyIndex, ref MetadataTypeName emittedName);

    private protected abstract TypeSymbol LookupNestedTypeDefSymbol(TypeSymbol container, ref MetadataTypeName emittedName);

    private protected abstract int GetIndexOfReferencedAssembly(AssemblyIdentity identity);

    internal TypeSymbol GetTypeSymbolForSerializedType(string s) {
        if (string.IsNullOrEmpty(s))
            return GetUnsupportedMetadataTypeSymbol();

        var fullName = MetadataHelpers.DecodeTypeName(s);
        return GetTypeSymbol(fullName, out _);
    }

    private protected TypeSymbol GetUnsupportedMetadataTypeSymbol(BadImageFormatException exception = null) {
        return _factory.GetUnsupportedMetadataTypeSymbol(_moduleSymbol, exception);
    }

    private protected TypeSymbol GetSZArrayTypeSymbol(
        TypeSymbol elementType,
        ImmutableArray<ModifierInfo<TypeSymbol>> customModifiers) {
        return _factory.GetSZArrayTypeSymbol(_moduleSymbol, elementType, customModifiers);
    }

    private protected TypeSymbol GetMDArrayTypeSymbol(
        int rank,
        TypeSymbol elementType,
        ImmutableArray<ModifierInfo<TypeSymbol>> customModifiers,
        ImmutableArray<int> sizes,
        ImmutableArray<int> lowerBounds) {
        return _factory.GetMDArrayTypeSymbol(_moduleSymbol, rank, elementType, customModifiers, sizes, lowerBounds);
    }

    private protected TypeSymbol GetSpecialType(SpecialType specialType) {
        return _factory.GetSpecialType(_moduleSymbol, specialType);
    }

    private protected TypeSymbol SubstituteWithUnboundIfGeneric(TypeSymbol type) {
        return _factory.MakeUnboundIfGeneric(_moduleSymbol, type);
    }

    private protected TypeSymbol MakePointerTypeSymbol(TypeSymbol type, ImmutableArray<ModifierInfo<TypeSymbol>> customModifiers) {
        return _factory.MakePointerTypeSymbol(_moduleSymbol, type, customModifiers);
    }

    private protected TypeSymbol MakeFunctionPointerTypeSymbol(
        CallingConvention callingConvention,
        ImmutableArray<ParamInfo<TypeSymbol>> retAndParamInfos) {
        return _factory.MakeFunctionPointerTypeSymbol(_moduleSymbol, callingConvention, retAndParamInfos);
    }

    private protected TypeSymbol SubstituteTypeParameters(
        TypeSymbol genericType,
        ImmutableArray<KeyValuePair<TypeSymbol, ImmutableArray<ModifierInfo<TypeSymbol>>>> arguments,
        ImmutableArray<bool> refersToNoPiaLocalType) {
        return _factory.SubstituteTypeParameters(_moduleSymbol, genericType, arguments, refersToNoPiaLocalType);
    }

    internal TypeSymbol GetTypeSymbol(
        MetadataHelpers.AssemblyQualifiedTypeName fullName,
        out bool refersToNoPiaLocalType) {
        int referencedAssemblyIndex;

        if (fullName.assemblyName is not null) {
            if (!AssemblyIdentity.TryParseDisplayName(fullName.assemblyName, out var identity)) {
                refersToNoPiaLocalType = false;
                return GetUnsupportedMetadataTypeSymbol();
            }

            referencedAssemblyIndex = GetIndexOfReferencedAssembly(identity);

            if (referencedAssemblyIndex == -1) {
                if (!IsContainingAssembly(identity)) {
                    refersToNoPiaLocalType = false;
                    return GetUnsupportedMetadataTypeSymbol();
                }
            }
        } else {
            referencedAssemblyIndex = -1;
        }

        var mdName = MetadataTypeName.FromFullName(fullName.topLevelType);
        var container = LookupTopLevelTypeDefSymbol(ref mdName, referencedAssemblyIndex, out refersToNoPiaLocalType);

        if (fullName.nestedTypes is not null) {
            if (refersToNoPiaLocalType) {
                refersToNoPiaLocalType = false;
                return GetUnsupportedMetadataTypeSymbol();
            }

            for (var i = 0; i < fullName.nestedTypes.Length; i++) {
                mdName = MetadataTypeName.FromTypeName(fullName.nestedTypes[i]);
                container = LookupNestedTypeDefSymbol(container, ref mdName);
            }
        }

        if (fullName.typeArguments is not null) {
            var typeArguments = ResolveTypeArguments(fullName.typeArguments, out var argumentRefersToNoPiaLocalType);
            container = SubstituteTypeParameters(container, typeArguments, argumentRefersToNoPiaLocalType);

            foreach (var flag in argumentRefersToNoPiaLocalType) {
                if (flag) {
                    refersToNoPiaLocalType = true;
                    break;
                }
            }
        } else {
            container = SubstituteWithUnboundIfGeneric(container);
        }

        // TODO Pointers
        // for (var i = 0; i < fullName.pointerCount; i++)
        //     container = MakePointerTypeSymbol(container, ImmutableArray<ModifierInfo<TypeSymbol>>.Empty);

        if (fullName.arrayRanks is not null) {
            foreach (var rank in fullName.arrayRanks) {
                container = rank == 0
                    ? GetSZArrayTypeSymbol(container, default)
                    : GetMDArrayTypeSymbol(rank, container, default, [], default);
            }
        }

        return container;
    }

    private ImmutableArray<KeyValuePair<TypeSymbol, ImmutableArray<ModifierInfo<TypeSymbol>>>> ResolveTypeArguments(
        MetadataHelpers.AssemblyQualifiedTypeName[] arguments,
        out ImmutableArray<bool> refersToNoPiaLocalType) {
        var count = arguments.Length;
        var typeArgumentsBuilder = ArrayBuilder<KeyValuePair<TypeSymbol, ImmutableArray<ModifierInfo<TypeSymbol>>>>
            .GetInstance(count);
        var refersToNoPiaBuilder = ArrayBuilder<bool>.GetInstance(count);

        foreach (var argument in arguments) {
            typeArgumentsBuilder.Add(
                new KeyValuePair<TypeSymbol, ImmutableArray<ModifierInfo<TypeSymbol>>>(
                    GetTypeSymbol(argument, out var refersToNoPia), []
                )
            );

            refersToNoPiaBuilder.Add(refersToNoPia);
        }

        refersToNoPiaLocalType = refersToNoPiaBuilder.ToImmutableAndFree();
        return typeArgumentsBuilder.ToImmutableAndFree();
    }

    private TypeSymbol LookupTopLevelTypeDefSymbol(
        ref MetadataTypeName emittedName,
        int referencedAssemblyIndex,
        out bool isNoPiaLocalType) {
        TypeSymbol container;

        if (referencedAssemblyIndex >= 0) {
            isNoPiaLocalType = false;
            container = LookupTopLevelTypeDefSymbol(referencedAssemblyIndex, ref emittedName);
        } else {
            container = LookupTopLevelTypeDefSymbol(ref emittedName, out isNoPiaLocalType);
        }

        return container;
    }
}

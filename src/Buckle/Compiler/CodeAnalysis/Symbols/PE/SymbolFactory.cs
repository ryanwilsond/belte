using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.Libraries;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SymbolFactory : SymbolFactory<PEModuleSymbol, TypeSymbol> {
    internal static readonly SymbolFactory Instance = new SymbolFactory();

    internal override TypeSymbol GetMDArrayTypeSymbol(
        PEModuleSymbol moduleSymbol,
        int rank,
        TypeSymbol elementType,
        ImmutableArray<ModifierInfo<TypeSymbol>> customModifiers,
        ImmutableArray<int> sizes,
        ImmutableArray<int> lowerBounds) {
        if (elementType is UnsupportedMetadataTypeSymbol)
            return elementType;

        return ArrayTypeSymbol.CreateMDArray(
            CreateType(elementType, customModifiers),
            rank,
            sizes,
            lowerBounds
        );
    }

    internal override TypeSymbol GetSpecialType(PEModuleSymbol moduleSymbol, SpecialType specialType) {
        return CorLibrary.GetSpecialType(specialType);
    }

    internal override TypeSymbol GetSZArrayTypeSymbol(
        PEModuleSymbol moduleSymbol,
        TypeSymbol elementType,
        ImmutableArray<ModifierInfo<TypeSymbol>> customModifiers) {
        if (elementType is UnsupportedMetadataTypeSymbol)
            return elementType;

        return ArrayTypeSymbol.CreateSZArray(CreateType(elementType, customModifiers));
    }

    internal override TypeSymbol GetUnsupportedMetadataTypeSymbol(
        PEModuleSymbol moduleSymbol,
        BadImageFormatException exception) {
        return new UnsupportedMetadataTypeSymbol(exception);
    }

    internal override TypeSymbol SubstituteTypeParameters(
        PEModuleSymbol moduleSymbol,
        TypeSymbol genericTypeDef,
        ImmutableArray<KeyValuePair<TypeSymbol, ImmutableArray<ModifierInfo<TypeSymbol>>>> arguments,
        ImmutableArray<bool> refersToNoPiaLocalType) {
        if (genericTypeDef is UnsupportedMetadataTypeSymbol)
            return genericTypeDef;

        foreach (var arg in arguments) {
            if (arg.Key.kind == SymbolKind.ErrorType &&
                arg.Key is UnsupportedMetadataTypeSymbol) {
                return new UnsupportedMetadataTypeSymbol();
            }
        }

        var genericType = (NamedTypeSymbol)genericTypeDef;
        var linkedAssemblies = moduleSymbol.containingAssembly.GetLinkedReferencedAssemblies();

        // var noPiaIllegalGenericInstantiation = false;

        if (!linkedAssemblies.IsDefaultOrEmpty || moduleSymbol.module.ContainsNoPiaLocalTypes()) {
            var typeToCheck = genericType;
            var argumentIndex = refersToNoPiaLocalType.Length - 1;

            for (var i = argumentIndex; i >= 0; i--) {
                if (refersToNoPiaLocalType[i] ||
                    (!linkedAssemblies.IsDefaultOrEmpty &&
                    MetadataDecoder.IsOrClosedOverATypeFromAssemblies(arguments[i].Key, linkedAssemblies))) {
                    // noPiaIllegalGenericInstantiation = true;
                    break;
                }
            }
        }

        var typeParameters = genericType.GetAllTypeParameters();

        if (typeParameters.Length != arguments.Length)
            return new UnsupportedMetadataTypeSymbol();

        var substitution = new TemplateMap(
            typeParameters,
            arguments.SelectAsArray(arg => new TypeOrConstant(CreateType(arg.Key, arg.Value)))
        );

        var constructedType = substitution.SubstituteNamedType(genericType);

        // TODO Error
        // if (noPiaIllegalGenericInstantiation)
        //     constructedType = new NoPiaIllegalGenericInstantiationSymbol(moduleSymbol, constructedType);

        return constructedType;
    }

    internal override TypeSymbol MakeUnboundIfGeneric(PEModuleSymbol moduleSymbol, TypeSymbol type) {
        return (type is NamedTypeSymbol namedType && namedType.isTemplateType) ? namedType.AsUnboundTemplateType() : type;
    }

    private static TypeWithAnnotations CreateType(TypeSymbol type, ImmutableArray<ModifierInfo<TypeSymbol>> customModifiers) {
        return new TypeWithAnnotations(type);
        // return TypeWithAnnotations.Create(type, NullableAnnotation.Oblivious, CSharpCustomModifier.Convert(customModifiers));
    }

    internal override TypeSymbol MakeFunctionPointerTypeSymbol(
        PEModuleSymbol moduleSymbol,
        CallingConvention callingConvention,
        ImmutableArray<ParamInfo<TypeSymbol>> retAndParamTypes) {
        return FunctionPointerTypeSymbol.CreateFromMetadata(moduleSymbol, callingConvention, retAndParamTypes);
    }

    internal override TypeSymbol MakePointerTypeSymbol(
        PEModuleSymbol moduleSymbol,
        TypeSymbol type,
        ImmutableArray<ModifierInfo<TypeSymbol>> customModifiers) {
        if (type is UnsupportedMetadataTypeSymbol)
            return type;

        return new PointerTypeSymbol(CreateType(type, customModifiers));
    }
}

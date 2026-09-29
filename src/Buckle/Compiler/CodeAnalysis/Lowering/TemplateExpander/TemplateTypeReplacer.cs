using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class TemplateTypeReplacer<TKey, TValue, TReturn>
    where TKey : TypeSymbol
    where TValue : TypeSymbol
    where TReturn : TypeSymbol {
    private readonly Dictionary<TKey, TValue> _map;
    private readonly Func<TKey, TValue, TReturn> _afterFoundTransformation;

    private TemplateTypeReplacer(Dictionary<TKey, TValue> map, Func<TKey, TValue, TReturn> afterFoundTransformation) {
        _map = map;
        _afterFoundTransformation = afterFoundTransformation;
    }

    internal static TypeWithAnnotations Replace(
        TypeWithAnnotations source,
        Dictionary<TKey, TValue> map,
        Func<TKey, TValue, TReturn> afterFoundTransformation = null) {
        var replacer = new TemplateTypeReplacer<TKey, TValue, TReturn>(map, afterFoundTransformation);
        return replacer.ReplaceTypeWithAnnotations(source);
    }

    internal static TypeSymbol Replace(
        TypeSymbol source,
        Dictionary<TKey, TValue> map,
        Func<TKey, TValue, TReturn> afterFoundTransformation = null) {
        var replacer = new TemplateTypeReplacer<TKey, TValue, TReturn>(map, afterFoundTransformation);
        return replacer.ReplaceType(source);
    }

    private TypeSymbol ReplaceType(TypeSymbol source) {
        if (source is TKey t && _map.TryGetValue(t, out var replacement)) {
            if (_afterFoundTransformation is not null)
                return _afterFoundTransformation(t, replacement);

            return replacement;
        }

        TypeSymbol result;

        switch (source.kind) {
            case SymbolKind.NamedType:
                result = ReplaceNamedType((NamedTypeSymbol)source);
                break;
            case SymbolKind.TemplateParameter:
                result = source;
                break;
            case SymbolKind.ArrayType:
                result = ReplaceArrayType((ArrayTypeSymbol)source);
                break;
            case SymbolKind.PointerType:
                result = ReplacePointerType((PointerTypeSymbol)source);
                break;
            case SymbolKind.FunctionPointerType:
                result = ReplaceFunctionPointerType((FunctionPointerTypeSymbol)source);
                break;
            case SymbolKind.FunctionType:
                result = ReplaceFunctionType((FunctionTypeSymbol)source);
                break;
            case SymbolKind.ErrorType:
            default:
                result = source;
                break;
        }

        return result;
    }

    private NamedTypeSymbol ReplaceNamedType(NamedTypeSymbol source) {
        if (source is null)
            return null;

        var oldConstructedFrom = source.constructedFrom;
        var newConstructedFrom = ReplaceTypeDeclaration(oldConstructedFrom);

        var oldTemplateArguments = source.templateArguments;
        var changed = !ReferenceEquals(oldConstructedFrom, newConstructedFrom);
        var newTypeArguments = ArrayBuilder<TypeOrConstant>.GetInstance(oldTemplateArguments.Length);

        for (var i = 0; i < oldTemplateArguments.Length; i++) {
            var oldArgument = oldTemplateArguments[i];

            if (oldArgument.isConstant) {
                newTypeArguments.Add(oldArgument);
                continue;
            }

            var newArgument = ReplaceTypeWithAnnotations(oldArgument.type);

            if (!changed && !oldArgument.type.IsSameAs(newArgument))
                changed = true;

            newTypeArguments.Add(new TypeOrConstant(newArgument));
        }

        if (!changed)
            return source;

        return newConstructedFrom.ConstructIfGeneric(newTypeArguments.ToImmutableAndFree()).WithTupleDataFrom(source);
    }

    private TypeWithAnnotations ReplaceTypeWithAnnotations(TypeWithAnnotations typeWithAnnotations) {
        var type = typeWithAnnotations.type;
        var typeSymbol = type.StrippedType();
        var newType = new TypeWithAnnotations(ReplaceType(typeSymbol));

        if (type.IsNullableType() && !newType.IsNullableType())
            newType = newType.SetIsAnnotated();

        if (!typeSymbol.IsTemplateParameter()) {
            if (typeSymbol.Equals(newType.type, TypeCompareKind.ConsiderEverything))
                return typeWithAnnotations;
            else if (typeSymbol.IsNullableType() && typeWithAnnotations.isNullable)
                return newType;

            return new TypeWithAnnotations(newType.type, typeWithAnnotations.isNullable);
        }

        if ((object)newType == (TemplateParameterSymbol)typeSymbol)
            return typeWithAnnotations;
        else if ((object)this == (TemplateParameterSymbol)typeSymbol)
            return new TypeWithAnnotations(newType.type);

        return new TypeWithAnnotations(newType.type, typeWithAnnotations.isNullable || newType.isNullable);
    }

    private NamedTypeSymbol ReplaceTypeDeclaration(NamedTypeSymbol previous) {
        var newContainingType = ReplaceNamedType(previous.containingType);

        if ((object)newContainingType is null)
            return previous;

        return previous.originalDefinition.AsMember(newContainingType);
    }

    private ArrayTypeSymbol ReplaceArrayType(ArrayTypeSymbol previous) {
        var oldElement = previous.elementTypeWithAnnotations;
        var element = ReplaceTypeWithAnnotations(oldElement);

        if (element.IsSameAs(oldElement))
            return previous;

        if (previous.isSZArray)
            return ArrayTypeSymbol.CreateSZArray(element, previous.baseType);

        return ArrayTypeSymbol.CreateMDArray(
            element,
            previous.rank,
            previous.sizes,
            previous.lowerBounds,
            previous.baseType
        );
    }

    private PointerTypeSymbol ReplacePointerType(PointerTypeSymbol t) {
        var oldPointedAtType = t.pointedAtTypeWithAnnotations;
        var pointedAtType = ReplaceTypeWithAnnotations(oldPointedAtType);

        if (pointedAtType.IsSameAs(oldPointedAtType))
            return t;

        return new PointerTypeSymbol(pointedAtType);
    }

    private FunctionPointerTypeSymbol ReplaceFunctionPointerType(FunctionPointerTypeSymbol f) {
        var substitutedReturnType = ReplaceTypeWithAnnotations(f.signature.returnTypeWithAnnotations);

        var parameterTypesWithAnnotations = f.signature.parameterTypesWithAnnotations;
        var replacedParamTypes = ReplaceTypes(parameterTypesWithAnnotations);

        if (!CollectionsEqual(replacedParamTypes, parameterTypesWithAnnotations) ||
            !f.signature.returnTypeWithAnnotations.IsSameAs(substitutedReturnType)) {
            f = f.ReplaceTypeSymbol(substitutedReturnType, replacedParamTypes);
        }

        return f;
    }

    private FunctionTypeSymbol ReplaceFunctionType(FunctionTypeSymbol f) {
        var substitutedReturnType = ReplaceTypeWithAnnotations(f.signature.returnTypeWithAnnotations);

        var parameterTypesWithAnnotations = f.signature.parameterTypesWithAnnotations;
        var replacedParamTypes = ReplaceTypes(parameterTypesWithAnnotations);

        if (!CollectionsEqual(replacedParamTypes, parameterTypesWithAnnotations) ||
            !f.signature.returnTypeWithAnnotations.IsSameAs(substitutedReturnType)) {
            f = f.ReplaceTypeSymbol(substitutedReturnType, replacedParamTypes);
        }

        return f;
    }

    private ImmutableArray<TypeWithAnnotations> ReplaceTypes(ImmutableArray<TypeWithAnnotations> original) {
        if (original.IsDefault)
            return default;

        var result = ArrayBuilder<TypeWithAnnotations>.GetInstance(original.Length);

        foreach (var type in original)
            result.Add(ReplaceTypeWithAnnotations(type));

        return result.ToImmutableAndFree();
    }

    private static bool CollectionsEqual(
        ImmutableArray<TypeWithAnnotations> collection1,
        ImmutableArray<TypeWithAnnotations> collection2) {
        if (collection1.Length != collection2.Length)
            return false;

        for (var i = 0; i < collection1.Length; i++) {
            var typeWithAnnotations1 = collection1[i];
            var typeWithAnnotations2 = collection2[i];

            if (!typeWithAnnotations1.Equals(typeWithAnnotations2))
                return false;
        }

        return true;
    }
}

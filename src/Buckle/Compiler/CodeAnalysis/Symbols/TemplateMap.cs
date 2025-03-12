using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class TemplateMap {
    private static readonly TemplateMap _empty = new TemplateMap();
    private static readonly Dictionary<TemplateParameterSymbol, TypeOrConstant> _emptyDictionary
        = new Dictionary<TemplateParameterSymbol, TypeOrConstant>(ReferenceEqualityComparer.Instance);

    internal static TemplateMap Empty => _empty;

    private readonly Dictionary<TemplateParameterSymbol, TypeOrConstant> _mapping;

    private TemplateMap() {
        _mapping = _emptyDictionary;
    }

    internal TemplateMap(ImmutableArray<TemplateParameterSymbol> from, ImmutableArray<TypeOrConstant> to) {
        _mapping = new Dictionary<TemplateParameterSymbol, TypeOrConstant>(ReferenceEqualityComparer.Instance);

        for (var i = 0; i < from.Length; i++) {
            var tp = from[i];
            var ta = to[i];
            _mapping.Add(tp, ta);
        }
    }

    internal TemplateMap(
        NamedTypeSymbol containingType,
        ImmutableArray<TemplateParameterSymbol> from,
        ImmutableArray<TypeOrConstant> to) {
        _mapping = ForType(containingType);

        for (var i = 0; i < from.Length; i++) {
            var templateParameter = from[i];
            var templateArgument = to[i];

            if (!templateArgument.Equals(templateParameter))
                _mapping.Add(templateParameter, templateArgument);
        }
    }

    internal TemplateMap(ImmutableArray<TemplateParameterSymbol> from, ImmutableArray<TemplateParameterSymbol> to)
        : this(from, TemplateParametersAsTypeOrConstants(to)) { }

    private static Dictionary<TemplateParameterSymbol, TypeOrConstant> ForType(NamedTypeSymbol containingType) {
        return containingType is SubstitutedNamedTypeSymbol substituted
            ? new Dictionary<TemplateParameterSymbol, TypeOrConstant>(
                substituted.templateSubstitution._mapping, ReferenceEqualityComparer.Instance)
            : new Dictionary<TemplateParameterSymbol, TypeOrConstant>(ReferenceEqualityComparer.Instance);
    }

    private TemplateMap(Dictionary<TemplateParameterSymbol, TypeOrConstant> mapping) {
        _mapping = mapping;
    }

    internal ImmutableArray<TemplateParameterSymbol> SubstituteTemplateParameters(
        ImmutableArray<TemplateParameterSymbol> original) {
        return original.SelectAsArray(
            (tp, m) => (TemplateParameterSymbol)m.SubstituteTemplateParameter(tp).type.type, this
        );
    }

    internal TypeOrConstant SubstituteTemplateParameter(TemplateParameterSymbol templateParameter) {
        if (_mapping.TryGetValue(templateParameter, out var result))
            return result;

        return null;
    }

    internal TypeOrConstant SubstituteType(TypeSymbol previous) {
        if (previous is null)
            return null;

        TypeSymbol result;

        switch (previous.kind) {
            case SymbolKind.NamedType:
                result = SubstituteNamedType((NamedTypeSymbol)previous);
                break;
            case SymbolKind.TemplateParameter:
                return SubstituteTemplateParameter((TemplateParameterSymbol)previous);
            case SymbolKind.ArrayType:
                result = SubstituteArrayType((ArrayTypeSymbol)previous);
                break;
            default:
                result = previous;
                break;
        }

        return new TypeOrConstant(result);
    }

    internal ImmutableArray<TypeOrConstant> SubstituteTypes(ImmutableArray<TypeWithAnnotations> original) {
        if (original.IsDefault)
            return default;

        var result = ArrayBuilder<TypeOrConstant>.GetInstance(original.Length);

        foreach (var type in original)
            result.Add(SubstituteType(type));

        return result.ToImmutableAndFree();
    }

    internal TypeOrConstant SubstituteType(TypeWithAnnotations previous) {
        return previous.SubstituteType(this);
    }

    internal ArrayTypeSymbol SubstituteArrayType(ArrayTypeSymbol previous) {
        var oldElement = previous.elementTypeWithAnnotations;
        var element = oldElement.SubstituteType(this).type;

        if (element.IsSameAs(oldElement))
            return previous;

        if (previous.isSZArray)
            return ArrayTypeSymbol.CreateSZArray(element, previous.baseType);

        return ArrayTypeSymbol.CreateMDArray(
            element,
            previous.rank,
            previous.sizes,
            previous.lowerBounds,
            previous.baseType);
    }

    internal NamedTypeSymbol SubstituteNamedType(NamedTypeSymbol previous) {
        if (previous is null)
            return null;

        var oldConstructedFrom = previous.constructedFrom;
        var newConstructedFrom = SubstituteTypeDeclaration(oldConstructedFrom);

        var oldTemplateArguments = previous.templateArguments;
        var changed = !ReferenceEquals(oldConstructedFrom, newConstructedFrom);
        var newTypeArguments = ArrayBuilder<TypeOrConstant>.GetInstance(oldTemplateArguments.Length);

        for (var i = 0; i < oldTemplateArguments.Length; i++) {
            var oldArgument = oldTemplateArguments[i];

            if (oldArgument.isConstant) {
                newTypeArguments.Add(oldArgument);
                continue;
            }

            var newArgument = oldArgument.type.SubstituteType(this).type;

            if (!changed && !oldArgument.type.IsSameAs(newArgument))
                changed = true;

            newTypeArguments.Add(new TypeOrConstant(newArgument));
        }

        if (!changed)
            return previous;

        return newConstructedFrom.ConstructIfGeneric(newTypeArguments.ToImmutableAndFree());
    }

    internal void SubstituteConstraintTypesDistinctWithoutModifiers(
        ImmutableArray<TypeWithAnnotations> original,
        ArrayBuilder<TypeWithAnnotations> result) {
        if (original.Length == 0) {
            return;
        } else if (original.Length == 1) {
            var type = original[0];
            result.Add(SubstituteType(type).type);
        } else {
            var map = PooledDictionary<TypeSymbol, int>.GetInstance();

            foreach (var type in original) {
                var substituted = SubstituteType(type).type;

                if (!map.TryGetValue(substituted.type, out var mergeWith)) {
                    map.Add(substituted.type, result.Count);
                    result.Add(substituted);
                } else {
                    result[mergeWith] = substituted.isNullable ? result[mergeWith] : substituted;
                }
            }

            map.Free();
        }
    }

    internal NamedTypeSymbol SubstituteTypeDeclaration(NamedTypeSymbol previous) {
        var newContainingType = SubstituteNamedType(previous.containingType);

        if ((object)newContainingType is null)
            return previous;

        return previous.originalDefinition.AsMember(newContainingType);
    }

    internal TemplateMap WithAlphaRename(
        MethodSymbol oldOwner,
        Symbol newOwner,
        out ImmutableArray<TemplateParameterSymbol> newTemplateParameters) {
        return WithAlphaRename(oldOwner.originalDefinition.templateParameters, newOwner, out newTemplateParameters);
    }

    internal TemplateMap WithAlphaRename(
        NamedTypeSymbol oldOwner,
        NamedTypeSymbol newOwner,
        out ImmutableArray<TemplateParameterSymbol> newTemplateParameters) {
        return WithAlphaRename(oldOwner.originalDefinition.templateParameters, newOwner, out newTemplateParameters);
    }

    internal TemplateMap WithAlphaRename(
        ImmutableArray<TemplateParameterSymbol> oldTemplateParameters,
        Symbol newOwner,
        out ImmutableArray<TemplateParameterSymbol> newTemplateParameters) {
        if (oldTemplateParameters.Length == 0) {
            newTemplateParameters = [];
            return this;
        }

        var result = new TemplateMap(_mapping);
        var newTypeParametersBuilder = ArrayBuilder<TemplateParameterSymbol>.GetInstance();

        var isSynthesized = !ReferenceEquals(
            oldTemplateParameters[0].containingSymbol.originalDefinition,
            newOwner.originalDefinition
        );

        var ordinal = 0;

        foreach (var templateParameter in oldTemplateParameters) {
            var newTemplateParameter = isSynthesized
                ? new SynthesizedSubstitutedTemplateParameterSymbol(newOwner, result, templateParameter, ordinal)
                : new SubstitutedTemplateParameterSymbol(newOwner, result, templateParameter, ordinal);

            result._mapping.Add(templateParameter, new TypeOrConstant(newTemplateParameter));
            newTypeParametersBuilder.Add(newTemplateParameter);
            ordinal++;
        }

        newTemplateParameters = newTypeParametersBuilder.ToImmutableAndFree();
        return result;
    }

    internal TemplateMap WithConcatAlphaRename(
        MethodSymbol oldOwner,
        Symbol newOwner,
        out ImmutableArray<TemplateParameterSymbol> newTypeParameters,
        out ImmutableArray<TemplateParameterSymbol> oldTypeParameters,
        MethodSymbol stopAt = null) {
        var parameters = ArrayBuilder<TemplateParameterSymbol>.GetInstance();

        while (oldOwner is not null && oldOwner != stopAt) {
            var currentParameters = oldOwner.originalDefinition.templateParameters;

            for (var i = currentParameters.Length - 1; i >= 0; i--)
                parameters.Add(currentParameters[i]);

            oldOwner = oldOwner.containingSymbol.originalDefinition as MethodSymbol;
        }

        parameters.ReverseContents();

        oldTypeParameters = parameters.ToImmutableAndFree();
        return WithAlphaRename(oldTypeParameters, newOwner, out newTypeParameters);
    }

    internal static ImmutableArray<TypeOrConstant> TemplateParametersAsTypeOrConstants(
        ImmutableArray<TemplateParameterSymbol> templateParameters) {
        return templateParameters.SelectAsArray(static (tp) => new TypeOrConstant(tp));
    }
}

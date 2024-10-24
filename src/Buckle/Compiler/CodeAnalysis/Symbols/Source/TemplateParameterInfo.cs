using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class TemplateParameterInfo {
    internal static readonly TemplateParameterInfo Empty = new TemplateParameterInfo {
        lazyTemplateParameters = [],
        lazyTypeParameterConstraintTypes = [],
        lazyTypeParameterConstraintKinds = [],
    };

    internal ImmutableArray<TemplateParameterSymbol> lazyTemplateParameters;

    internal ImmutableArray<ImmutableArray<TypeWithAnnotations>> lazyTypeParameterConstraintTypes;

    internal ImmutableArray<TypeParameterConstraintKinds> lazyTypeParameterConstraintKinds;
}

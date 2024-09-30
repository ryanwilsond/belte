using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class TemplateParameterInfo {
    internal static readonly TemplateParameterInfo Empty = new TemplateParameterInfo {
        lazyTemplateParameters = [],
        lazyTemplateParameterConstraintTypes = [],
        lazyTemplateParameterConstraintKinds = [],
    };

    internal ImmutableArray<TemplateParameterSymbol> lazyTemplateParameters;

    internal ImmutableArray<ImmutableArray<TypeWithAnnotations>> lazyTemplateParameterConstraintTypes;

    internal ImmutableArray<TypeParameterConstraintKinds> lazyTemplateParameterConstraintKinds;
}

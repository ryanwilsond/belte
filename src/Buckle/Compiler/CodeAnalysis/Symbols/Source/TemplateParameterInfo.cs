using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class TemplateParameterInfo {
    internal static readonly TemplateParameterInfo Empty = new TemplateParameterInfo {
        lazyTemplateParameters = [],
        lazyTypeParameterConstraintTypes = [],
        lazyTypeParameterConstraintKinds = [],
        lazyTemplateConstraints = [],
    };

    internal ImmutableArray<TemplateParameterSymbol> lazyTemplateParameters;

    internal ImmutableArray<ImmutableArray<TypeWithAnnotations>> lazyTypeParameterConstraintTypes;

    internal ImmutableArray<TypeParameterConstraintKinds> lazyTypeParameterConstraintKinds;

    internal ImmutableArray<BoundExpression> lazyTemplateConstraints;
}

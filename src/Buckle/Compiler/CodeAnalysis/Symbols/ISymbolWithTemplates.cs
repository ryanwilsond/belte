using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A symbol that has template parameters.
/// </summary>
internal interface ISymbolWithTemplates : ISymbol {
    ImmutableArray<TemplateParameterSymbol> templateParameters { get; }

    ImmutableArray<BoundExpression> templateConstraints { get; }

    abstract ImmutableArray<TypeOrConstant> templateArguments { get; }

    abstract TemplateMap templateSubstitution { get; }
}

using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A symbol that has template parameters.
/// </summary>
internal interface ISymbolWithTemplates : ISymbol {
    public ImmutableArray<TemplateParameterSymbol> templateParameters { get; }

    public ImmutableArray<BoundExpression> templateConstraints { get; }

    public abstract ImmutableArray<TypeOrConstant> templateArguments { get; }

    public abstract TemplateMap templateSubstitution { get; }

    public string Signature();
}

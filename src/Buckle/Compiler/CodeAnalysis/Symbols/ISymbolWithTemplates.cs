using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A symbol that has template parameters.
/// </summary>
internal interface ISymbolWithTemplates : ISymbol {
    public ImmutableArray<ParameterSymbol> templateParameters { get; protected set; }

    public ImmutableArray<BoundExpression> templateConstraints { get; protected set; }

    public string Signature();
}

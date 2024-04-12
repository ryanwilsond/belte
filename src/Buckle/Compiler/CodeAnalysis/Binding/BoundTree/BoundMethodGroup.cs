using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// An intermediate expression used when resolving overloads.
/// </summary>
internal sealed class BoundMethodGroup : BoundExpression {
    internal BoundMethodGroup(string name, ImmutableArray<MethodSymbol> methods) {
        this.name = name;
        this.methods = methods;
    }

    internal override BoundNodeKind kind => BoundNodeKind.MethodGroup;

    internal override BoundType type => BoundType.MethodGroup;

    internal string name { get; }

    internal ImmutableArray<MethodSymbol> methods { get; }
}

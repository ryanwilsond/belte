using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// An intermediate expression used when resolving overloads.
/// </summary>
internal sealed class BoundMethodGroup : BoundExpression {
    internal BoundMethodGroup(
        string name,
        ImmutableArray<MethodSymbol> methods,
        ImmutableArray<TypeOrConstant> templateArguments) {
        this.name = name;
        this.methods = methods;
        this.templateArguments = templateArguments;
    }

    internal override BoundNodeKind kind => BoundNodeKind.MethodGroup;

    internal override TypeSymbol type => TypeSymbol.Func;

    internal string name { get; }

    internal ImmutableArray<MethodSymbol> methods { get; }

    internal ImmutableArray<TypeOrConstant> templateArguments { get; }
}

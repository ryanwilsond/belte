using System;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class MissingModuleSymbolWithName : MissingModuleSymbol {
    internal MissingModuleSymbolWithName(AssemblySymbol assembly, string name) : base(assembly, ordinal: -1) {
        this.name = name;
    }

    public override string name { get; }

    public override int GetHashCode() {
        return Hash.Combine(containingAssembly.GetHashCode(), StringComparer.OrdinalIgnoreCase.GetHashCode(name));
    }

    internal override bool Equals(Symbol obj, TypeCompareKind compareKind) {
        if (ReferenceEquals(this, obj))
            return true;

        return obj is MissingModuleSymbolWithName other &&
            containingAssembly.Equals(other.containingAssembly, compareKind) &&
            string.Equals(name, other.name, StringComparison.OrdinalIgnoreCase);
    }
}

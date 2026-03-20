using System;
using System.Collections.Immutable;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class UnboundArgumentErrorTypeSymbol : ErrorTypeSymbol {
    internal static readonly ErrorTypeSymbol Instance
        = new UnboundArgumentErrorTypeSymbol("", null /* TODO error */);

    private UnboundArgumentErrorTypeSymbol(string name, BelteDiagnostic error) {
        this.name = name;
        this.error = error;
    }

    internal static ImmutableArray<TypeOrConstant> CreateTemplateArguments(
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        int n,
        BelteDiagnostic error) {
        var result = ArrayBuilder<TypeOrConstant>.GetInstance();

        for (var i = 0; i < n; i++) {
            var name = (i < templateParameters.Length) ? templateParameters[i].name : "";
            result.Add(new TypeOrConstant(new UnboundArgumentErrorTypeSymbol(name, error)));
        }

        return result.ToImmutableAndFree();
    }

    public override string name { get; }

    internal override bool mangleName => false;

    internal override BelteDiagnostic error { get; }

    internal override bool Equals(TypeSymbol t2, TypeCompareKind comparison) {
        if ((object)t2 == this)
            return true;

        return t2 is UnboundArgumentErrorTypeSymbol other &&
            string.Equals(other.name, name, StringComparison.Ordinal) &&
            Equals(other.error, error);
    }

    public override int GetHashCode() {
        return error is null
            ? name.GetHashCode()
            : Hash.Combine(name, error.info.code.Value);
    }
}

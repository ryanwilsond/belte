using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class UnmanagedCallersOnlyAttributeData {
    internal static readonly UnmanagedCallersOnlyAttributeData Uninitialized = new UnmanagedCallersOnlyAttributeData(callingConventionTypes: []);
    internal static readonly UnmanagedCallersOnlyAttributeData AttributePresentDataNotBound = new UnmanagedCallersOnlyAttributeData(callingConventionTypes: []);
    private static readonly UnmanagedCallersOnlyAttributeData PlatformDefault = new UnmanagedCallersOnlyAttributeData(callingConventionTypes: []);

    internal const string CallConvsPropertyName = "CallConvs";

    internal static UnmanagedCallersOnlyAttributeData Create(ImmutableHashSet<NamedTypeSymbol>? callingConventionTypes)
        => callingConventionTypes switch {
            null or { IsEmpty: true } => PlatformDefault,
            _ => new UnmanagedCallersOnlyAttributeData(callingConventionTypes)
        };

    internal readonly ImmutableHashSet<NamedTypeSymbol> callingConventionTypes;

    private UnmanagedCallersOnlyAttributeData(ImmutableHashSet<NamedTypeSymbol> callingConventionTypes) {
        this.callingConventionTypes = callingConventionTypes;
    }

    internal static bool IsCallConvsTypedConstant(string key, bool isField, in TypedConstant value) {
        return isField
               && key == CallConvsPropertyName
               && value.kind == TypedConstantKind.Array
               && (value.values.IsDefaultOrEmpty || value.values.All(v => v.kind == TypedConstantKind.Type));
    }
}

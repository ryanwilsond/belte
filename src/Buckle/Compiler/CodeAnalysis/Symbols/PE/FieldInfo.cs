using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal readonly struct FieldInfo<TypeSymbol>
    where TypeSymbol : class {
    internal readonly bool isByRef;
    internal readonly ImmutableArray<ModifierInfo<TypeSymbol>> refCustomModifiers;
    internal readonly TypeSymbol type;
    internal readonly ImmutableArray<ModifierInfo<TypeSymbol>> customModifiers;

    internal FieldInfo(
        bool isByRef,
        ImmutableArray<ModifierInfo<TypeSymbol>> refCustomModifiers,
        TypeSymbol type,
        ImmutableArray<ModifierInfo<TypeSymbol>> customModifiers) {
        this.isByRef = isByRef;
        this.refCustomModifiers = refCustomModifiers;
        this.type = type;
        this.customModifiers = customModifiers;
    }

    internal FieldInfo(TypeSymbol type)
        : this(isByRef: false, refCustomModifiers: default, type, customModifiers: default) { }
}

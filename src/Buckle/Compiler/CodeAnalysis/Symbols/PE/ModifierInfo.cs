using System.Runtime.InteropServices;

namespace Buckle.CodeAnalysis.Symbols;

[StructLayout(LayoutKind.Auto)]
internal readonly struct ModifierInfo<TypeSymbol> where TypeSymbol : class {
    internal readonly bool isOptional;
    internal readonly TypeSymbol modifier;

    internal ModifierInfo(bool isOptional, TypeSymbol modifier) {
        this.isOptional = isOptional;
        this.modifier = modifier;
    }
}

using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;

namespace Buckle.CodeAnalysis.Symbols;

[StructLayout(LayoutKind.Auto)]
internal struct ParamInfo<TypeSymbol>
    where TypeSymbol : class {
    internal bool isByRef;
    internal TypeSymbol type;
    internal ParameterHandle handle;
    internal ImmutableArray<ModifierInfo<TypeSymbol>> refCustomModifiers;
    internal ImmutableArray<ModifierInfo<TypeSymbol>> customModifiers;
}

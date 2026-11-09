using System.Collections.Immutable;
using System.Linq;

namespace Buckle.CodeAnalysis.Symbols;

internal static class ModifierInfoExtensions {
    internal static bool AnyRequired<TypeSymbol>(this ImmutableArray<ModifierInfo<TypeSymbol>> modifiers)
        where TypeSymbol : class {
        return !modifiers.IsDefaultOrEmpty && modifiers.Any(static m => !m.isOptional);
    }
}

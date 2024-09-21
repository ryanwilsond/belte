using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// An array type symbol.
/// </summary>
internal sealed class TemplateMap {
    private readonly Dictionary<TemplateParameterSymbol, TypeOrConstant> _mapping;

    internal TemplateMap(ImmutableArray<TemplateParameterSymbol> from, ImmutableArray<TypeOrConstant> to) {
        _mapping = new Dictionary<TemplateParameterSymbol, TypeOrConstant>(ReferenceEqualityComparer.Instance);

        for (var i = 0; i < from.Length; i++) {
            var tp = from[i];
            var ta = to[i];
            _mapping.Add(tp, ta);
        }
    }

    internal TypeOrConstant SubstituteTemplate(TemplateParameterSymbol templateParameter) {
        if (_mapping.TryGetValue(templateParameter, out var result))
            return result;

        return null;
    }
}

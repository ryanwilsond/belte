using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal class CommonMethodEarlyWellKnownAttributeData : EarlyWellKnownAttributeData {
    #region ConditionalAttribute

    private ImmutableArray<string> _lazyConditionalSymbols = [];

    internal void AddConditionalSymbol(string name) {
        _lazyConditionalSymbols = _lazyConditionalSymbols.Add(name);
    }

    internal ImmutableArray<string> conditionalSymbols => _lazyConditionalSymbols;

    #endregion
}

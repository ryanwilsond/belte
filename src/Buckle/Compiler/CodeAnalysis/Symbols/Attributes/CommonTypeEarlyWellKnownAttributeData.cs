using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class CommonTypeEarlyWellKnownAttributeData : EarlyWellKnownAttributeData {
    private AttributeUsageInfo _attributeUsageInfo = AttributeUsageInfo.Null;

    public AttributeUsageInfo attributeUsageInfo {
        get {
            return _attributeUsageInfo;
        }
        set {
            _attributeUsageInfo = value;
        }
    }

    #region ConditionalAttribute

    private ImmutableArray<string> _lazyConditionalSymbols = [];

    internal void AddConditionalSymbol(string name) {
        _lazyConditionalSymbols = _lazyConditionalSymbols.Add(name);
    }

    internal ImmutableArray<string> conditionalSymbols => _lazyConditionalSymbols;

    #endregion
}

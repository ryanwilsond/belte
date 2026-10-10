using System.Collections.Immutable;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class CommonTypeEarlyWellKnownAttributeData : EarlyWellKnownAttributeData {
    private AttributeUsageInfo _attributeUsageInfo = AttributeUsageInfo.Null;

    internal AttributeUsageInfo attributeUsageInfo {
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

    #region EntryTypeAttribute

    private ThreeState _hasEntryTypeAttribute = ThreeState.Unknown;

    internal ThreeState hasEntryTypeAttribute {
        get {
            return _hasEntryTypeAttribute;
        }
        set {
            _hasEntryTypeAttribute = value;
        }
    }

    #endregion
}

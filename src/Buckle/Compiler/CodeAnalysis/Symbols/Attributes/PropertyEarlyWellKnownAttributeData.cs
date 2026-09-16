using System.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class PropertyEarlyWellKnownAttributeData : CommonPropertyEarlyWellKnownAttributeData {
    private string _indexerName;

    internal string indexerName {
        get {
            return _indexerName;
        }
        set {
            Debug.Assert(value is not null);
            _indexerName ??= value;
        }
    }
}

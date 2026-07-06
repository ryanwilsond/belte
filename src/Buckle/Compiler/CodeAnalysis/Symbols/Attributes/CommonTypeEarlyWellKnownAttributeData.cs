
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
}


namespace Buckle.CodeAnalysis.Symbols;

internal class CommonPropertyWellKnownAttributeData : WellKnownAttributeData {
    private bool _hasSpecialNameAttribute;

    internal bool hasSpecialNameAttribute {
        get {
            return _hasSpecialNameAttribute;
        }
        set {
            _hasSpecialNameAttribute = value;
        }
    }
}

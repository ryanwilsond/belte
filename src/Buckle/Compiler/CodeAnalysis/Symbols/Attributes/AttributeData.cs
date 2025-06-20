
namespace Buckle.CodeAnalysis.Symbols;

internal abstract class AttributeData {
    internal virtual bool hasErrors => false;

    internal bool IsTargetAttribute(AttributeDescription description) {
        return GetTargetAttributeSignatureIndex(description) != -1;
    }

    internal abstract int GetTargetAttributeSignatureIndex(AttributeDescription description);
}

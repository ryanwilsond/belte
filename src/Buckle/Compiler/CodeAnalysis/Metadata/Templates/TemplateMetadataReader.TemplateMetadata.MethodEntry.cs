
namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataReader {
    internal sealed partial class TemplateMetadata {
        internal struct MethodEntry {
            internal string name;
            internal ushort arity;
            internal uint containingTypeIndex;
            internal uint methodIndex;

            internal MethodEntry(string name, ushort arity, uint containingTypeIndex, uint methodIndex) {
                this.name = name;
                this.arity = arity;
                this.containingTypeIndex = containingTypeIndex;
                this.methodIndex = methodIndex;
            }
        }
    }
}

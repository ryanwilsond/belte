
namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataReader {
    internal sealed partial class TemplateMetadata {
        internal struct TypeEntry {
            internal string name;
            internal ushort arity;
            internal TemplateMetadataWriter.TypeFlags flags;
            internal string namespaceName;
            internal uint assemblyIndex;
            internal bool isNested;
            internal uint containingTypeIndex;

            internal TypeEntry(
                string name,
                ushort arity,
                TemplateMetadataWriter.TypeFlags flags,
                string namespaceName,
                uint assemblyIndex,
                bool isNested,
                uint containingTypeIndex) {
                this.name = name;
                this.arity = arity;
                this.flags = flags;
                this.namespaceName = namespaceName;
                this.assemblyIndex = assemblyIndex;
                this.isNested = isNested;
                this.containingTypeIndex = containingTypeIndex;
            }
        }
    }
}

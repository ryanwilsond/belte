using System.Reflection;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataReader {
    internal sealed partial class TemplateMetadata {
        internal struct PropertyInfo {
            internal string name;
            internal PropertyAttributes attributes;
            internal TemplateMetadataWriter.PropertyFlags flags;
            internal TypeSymbol type;
            internal uint getMethodIndex;
            internal uint setMethodIndex;
            internal AttributeData[] customAttributes;

            internal PropertyInfo(
                string name,
                PropertyAttributes attributes,
                TemplateMetadataWriter.PropertyFlags flags,
                TypeSymbol type,
                uint getMethodIndex,
                uint setMethodIndex,
                AttributeData[] customAttributes) {
                this.name = name;
                this.attributes = attributes;
                this.flags = flags;
                this.type = type;
                this.getMethodIndex = getMethodIndex;
                this.setMethodIndex = setMethodIndex;
                this.customAttributes = customAttributes;
            }
        }
    }
}

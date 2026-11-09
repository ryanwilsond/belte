using System.Reflection;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataReader {
    internal sealed partial class TemplateMetadata {
        internal struct FieldInfo {
            internal string name;
            internal FieldAttributes attributes;
            internal TemplateMetadataWriter.FieldFlags flags;
            internal TypeSymbol type;
            internal AttributeData[] customAttributes;
            internal ConstantValue defaultValue;

            internal FieldInfo(
                string name,
                FieldAttributes attributes,
                TemplateMetadataWriter.FieldFlags flags,
                TypeSymbol type,
                AttributeData[] customAttributes,
                ConstantValue defaultValue) {
                this.name = name;
                this.attributes = attributes;
                this.flags = flags;
                this.type = type;
                this.customAttributes = customAttributes;
                this.defaultValue = defaultValue;
            }
        }
    }
}

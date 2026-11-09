using System.Reflection;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataReader {
    internal sealed partial class TemplateMetadata {
        internal struct ParameterInfo {
            internal string name;
            internal ParameterAttributes attributes;
            internal TemplateMetadataWriter.ParameterFlags flags;
            internal TypeSymbol type;
            internal ConstantValue defaultValue;
            internal AttributeData[] customAttributes;

            internal ParameterInfo(
                string name,
                ParameterAttributes attributes,
                TemplateMetadataWriter.ParameterFlags flags,
                TypeSymbol type,
                ConstantValue defaultValue,
                AttributeData[] customAttributes) {
                this.name = name;
                this.attributes = attributes;
                this.flags = flags;
                this.type = type;
                this.defaultValue = defaultValue;
                this.customAttributes = customAttributes;
            }
        }
    }
}

using System.Reflection;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataReader {
    internal sealed partial class TemplateMetadata {
        internal struct TemplateParameterInfo {
            internal string name;
            internal GenericParameterAttributes attributes;
            internal TemplateMetadataWriter.TemplateParameterFlags flags;
            internal TypeSymbol type;
            internal TypeOrConstant defaultValue;
            internal AttributeData[] customAttributes;
            internal TypeSymbol[] constraintTypes;

            internal TemplateParameterInfo(
                string name,
                GenericParameterAttributes attributes,
                TemplateMetadataWriter.TemplateParameterFlags flags,
                TypeSymbol type,
                TypeOrConstant defaultValue,
                AttributeData[] customAttributes,
                TypeSymbol[] constraintTypes) {
                this.name = name;
                this.attributes = attributes;
                this.flags = flags;
                this.type = type;
                this.defaultValue = defaultValue;
                this.customAttributes = customAttributes;
                this.constraintTypes = constraintTypes;
            }
        }
    }
}

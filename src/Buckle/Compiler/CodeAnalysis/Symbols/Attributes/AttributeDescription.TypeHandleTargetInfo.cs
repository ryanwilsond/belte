using System.Reflection.Metadata;

namespace Buckle.CodeAnalysis.Symbols;

internal partial struct AttributeDescription {
    internal readonly struct TypeHandleTargetInfo {
        internal readonly string @namespace;
        internal readonly string name;
        internal readonly SerializationTypeCode underlying;

        internal TypeHandleTargetInfo(string @namespace, string name, SerializationTypeCode underlying) {
            this.@namespace = @namespace;
            this.name = name;
            this.underlying = underlying;
        }
    }
}

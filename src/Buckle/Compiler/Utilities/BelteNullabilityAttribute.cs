// A copy of Belte.Core's Belte.NullabilityAttribute
// This is used to mark reference types as non-nullable in the compiler's APIs used by Belte code

using System;

namespace Belte {
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.ReturnValue | AttributeTargets.Parameter)]
    public sealed class NullabilityAttribute : Attribute {
        public byte[] encoding;

        public NullabilityAttribute(byte[] encoding) {
            this.encoding = encoding;
        }
    }
}

using System.Reflection.Metadata;

namespace Buckle.CodeAnalysis;

internal sealed partial class PEModule {
    internal readonly struct AttributeInfo {
        internal readonly CustomAttributeHandle handle;
        internal readonly byte signatureIndex;

        internal AttributeInfo(CustomAttributeHandle handle, int signatureIndex) {
            this.handle = handle;
            this.signatureIndex = (byte)signatureIndex;
        }

        internal bool hasValue => !handle.IsNil;
    }
}

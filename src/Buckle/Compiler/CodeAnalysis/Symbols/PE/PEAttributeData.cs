using System.Reflection.Metadata;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class PEAttributeData : AttributeData {
    private readonly MetadataDecoder _decoder;
    private readonly CustomAttributeHandle _handle;

    internal PEAttributeData(PEModuleSymbol moduleSymbol, CustomAttributeHandle handle) {
        _decoder = new MetadataDecoder(moduleSymbol);
        _handle = handle;
    }

    internal override int GetTargetAttributeSignatureIndex(AttributeDescription description) {
        return _decoder.GetTargetAttributeSignatureIndex(_handle, description);
    }
}

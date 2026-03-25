using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class PEAttributeData : AttributeData {
    private readonly MetadataDecoder _decoder;
    private readonly CustomAttributeHandle _handle;

    internal PEAttributeData(PEModuleSymbol moduleSymbol, CustomAttributeHandle handle) {
        _decoder = new MetadataDecoder(moduleSymbol);
        _handle = handle;
    }

    protected internal override ImmutableArray<KeyValuePair<string, TypedConstant>> _commonNamedArguments {
        get {
            throw new NotImplementedException();
            // EnsureAttributeArgumentsAreLoaded();
            // return _lazyNamedArguments;
        }
    }

    protected internal override ImmutableArray<TypedConstant> _commonConstructorArguments {
        get {
            throw new NotImplementedException();
            // EnsureAttributeArgumentsAreLoaded();
            // return _lazyConstructorArguments;
        }
    }

    internal override int GetTargetAttributeSignatureIndex(AttributeDescription description) {
        return _decoder.GetTargetAttributeSignatureIndex(_handle, description);
    }

    internal override TextLocation GetAttributeArgumentLocation(int parameterIndex) {
        return new MetadataLocation(_decoder.moduleSymbol);
    }

    internal override bool IsTargetAttribute(string namespaceName, string typeName) {
        return _decoder.IsTargetAttribute(_handle, namespaceName, typeName);
    }
}

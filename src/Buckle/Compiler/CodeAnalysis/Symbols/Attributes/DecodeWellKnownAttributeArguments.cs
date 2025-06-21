using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal struct DecodeWellKnownAttributeArguments<TAttributeSyntax, TAttributeData, TAttributeLocation>
    where TAttributeSyntax : SyntaxNode
    where TAttributeData : AttributeData {
    private WellKnownAttributeData? _lazyDecodeData;

    internal T GetOrCreateData<T>() where T : WellKnownAttributeData, new() {
        _lazyDecodeData ??= new T();
        return (T)_lazyDecodeData;
    }

    internal readonly bool hasDecodedData => _lazyDecodeData is not null;

    internal readonly WellKnownAttributeData decodedData => _lazyDecodeData;

    internal TAttributeSyntax attributeSyntax { get; set; }

    internal TAttributeData attribute { get; set; }

    internal int index { get; set; }

    internal int attributesCount { get; set; }

    internal BelteDiagnosticQueue diagnostics { get; set; }

    internal TAttributeLocation symbolPart { get; set; }
}

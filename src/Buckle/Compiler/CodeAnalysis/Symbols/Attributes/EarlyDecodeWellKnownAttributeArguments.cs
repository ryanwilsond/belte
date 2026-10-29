using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal struct EarlyDecodeWellKnownAttributeArguments<TEarlyBinder, TNamedTypeSymbol, TAttributeSyntax, TAttributeLocation>
    where TNamedTypeSymbol : INamedTypeSymbol
    where TAttributeSyntax : SyntaxNode {
    private EarlyWellKnownAttributeData _lazyDecodeData;

    internal T GetOrCreateData<T>() where T : EarlyWellKnownAttributeData, new() {
        _lazyDecodeData ??= new T();
        return (T)_lazyDecodeData;
    }

    internal bool hasDecodedData {
        get {
            if (_lazyDecodeData is not null) {
                // _lazyDecodeData.VerifyDataStored(expected: true);
                return true;
            }

            return false;
        }
    }

    internal EarlyWellKnownAttributeData decodedData => _lazyDecodeData;

    internal TEarlyBinder binder { get; set; }

    internal TNamedTypeSymbol attributeType { get; set; }

    internal TAttributeSyntax attributeSyntax { get; set; }

    internal TAttributeLocation symbolPart { get; set; }
}

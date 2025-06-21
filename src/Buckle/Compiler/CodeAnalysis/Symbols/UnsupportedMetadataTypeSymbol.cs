using System;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class UnsupportedMetadataTypeSymbol : ErrorTypeSymbol {
    private readonly BadImageFormatException _mrEx;

    internal UnsupportedMetadataTypeSymbol(BadImageFormatException mrEx = null) {
        _mrEx = mrEx;
    }

    internal override BelteDiagnostic error {
        get {
            // TODO error
            // return new CSDiagnosticInfo(ErrorCode.ERR_BogusType, string.Empty);
            return null;
        }
    }

    internal override bool mangleName => false;
}

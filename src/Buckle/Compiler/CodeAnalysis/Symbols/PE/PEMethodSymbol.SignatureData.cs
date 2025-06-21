using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed partial class PEMethodSymbol {
    internal class SignatureData {
        internal readonly SignatureHeader header;
        internal readonly ImmutableArray<ParameterSymbol> parameters;
        internal readonly PEParameterSymbol returnParam;

        internal SignatureData(
            SignatureHeader header,
            ImmutableArray<ParameterSymbol> parameters,
            PEParameterSymbol returnParam) {
            this.header = header;
            this.parameters = parameters;
            this.returnParam = returnParam;
        }
    }
}

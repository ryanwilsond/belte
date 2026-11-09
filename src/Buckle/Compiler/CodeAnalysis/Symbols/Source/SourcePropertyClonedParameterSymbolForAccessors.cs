
namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourcePropertyClonedParameterSymbolForAccessors : SourceClonedParameterSymbol {
    internal SourcePropertyClonedParameterSymbolForAccessors(SourceParameterSymbol originalParam, Symbol newOwner)
        : base(originalParam, newOwner, originalParam.ordinal, suppressOptional: false) {
    }
}

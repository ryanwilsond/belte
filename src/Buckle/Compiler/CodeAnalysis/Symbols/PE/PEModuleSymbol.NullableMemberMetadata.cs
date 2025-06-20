
namespace Buckle.CodeAnalysis.Symbols;

internal sealed partial class PEModuleSymbol {
    private enum NullableMemberMetadata : byte {
        Unknown = 0,
        Public,
        Internal,
        All,
    }
}

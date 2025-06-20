
namespace Buckle.CodeAnalysis.Symbols;

internal sealed partial class PEModuleSymbol {
    internal enum RefSafetyRulesAttributeVersion : byte {
        Uninitialized = 0,
        NoAttribute,
        Version11,
        UnrecognizedAttribute,
    }
}


namespace Buckle.CodeAnalysis.Binding;

internal sealed class ImportChain {
    internal readonly Imports imports;
    internal readonly ImportChain parentOpt;

    internal ImportChain(Imports imports, ImportChain parentOpt) {
        this.imports = imports;
        this.parentOpt = parentOpt;
    }
}

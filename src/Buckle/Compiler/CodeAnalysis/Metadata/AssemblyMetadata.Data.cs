using System.Collections.Immutable;

namespace Buckle.CodeAnalysis;

internal sealed partial class AssemblyMetadata {
    private sealed class Data {
        internal static readonly Data Disposed = new Data();

        internal readonly ImmutableArray<ModuleMetadata> modules;
        internal readonly PEAssembly assembly;

        private Data() { }

        internal Data(ImmutableArray<ModuleMetadata> modules, PEAssembly assembly) {
            this.modules = modules;
            this.assembly = assembly;
        }

        internal bool isDisposed => assembly is null;
    }
}

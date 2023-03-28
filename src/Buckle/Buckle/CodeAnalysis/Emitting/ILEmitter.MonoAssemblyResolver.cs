using Mono.Cecil;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed partial class ILEmitter {
    private class MonoAssemblyResolver : IAssemblyResolver {
        public AssemblyDefinition assemblyDefinition;

        public AssemblyDefinition Resolve(AssemblyNameReference name) {
            return assemblyDefinition;
        }

        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters) {
            return assemblyDefinition;
        }

        public void Dispose() {
            assemblyDefinition = null;
        }
    }
}

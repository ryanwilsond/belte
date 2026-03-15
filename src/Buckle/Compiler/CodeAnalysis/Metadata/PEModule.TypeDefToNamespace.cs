using System.Reflection.Metadata;

namespace Buckle.CodeAnalysis;

internal sealed partial class PEModule {
    private readonly struct TypeDefToNamespace {
        internal readonly TypeDefinitionHandle typeDef;
        internal readonly NamespaceDefinitionHandle namespaceHandle;

        internal TypeDefToNamespace(TypeDefinitionHandle typeDef, NamespaceDefinitionHandle namespaceHandle) {
            this.typeDef = typeDef;
            this.namespaceHandle = namespaceHandle;
        }
    }
}

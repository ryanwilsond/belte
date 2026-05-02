using System.Reflection.Metadata;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class PENamedTypeSymbol {
    private sealed class PENamedTypeSymbolNonGeneric : PENamedTypeSymbol {
        internal PENamedTypeSymbolNonGeneric(
            PEModuleSymbol moduleSymbol,
            NamespaceOrTypeSymbol container,
            TypeDefinitionHandle handle,
            string emittedNamespaceName)
            : base(moduleSymbol, container, handle, emittedNamespaceName, 0, out _) { }

        public override int arity => 0;

        internal override bool mangleName => false;

        internal override int metadataArity
            => _container is not PENamedTypeSymbol containingType ? 0 : containingType.metadataArity;
    }
}

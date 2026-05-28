using System.Reflection.Metadata;
using Buckle.Utilities;

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

        private protected override NamedTypeSymbol WithTupleDataCore(TupleExtraData newData) {
            throw ExceptionUtilities.Unreachable();
        }
    }
}

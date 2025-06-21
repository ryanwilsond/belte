using System.Collections.Immutable;
using System.Reflection.Metadata;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class PENamedTypeSymbol {
    private sealed class PENamedTypeSymbolGeneric : PENamedTypeSymbol {
        private readonly GenericParameterHandleCollection _genericParameterHandles;
        private readonly ushort _arity;
        private readonly bool _mangleName;
        private ImmutableArray<TemplateParameterSymbol> _lazyTemplateParameters;

        internal PENamedTypeSymbolGeneric(
            PEModuleSymbol moduleSymbol,
            NamespaceOrTypeSymbol container,
            TypeDefinitionHandle handle,
            string emittedNamespaceName,
            GenericParameterHandleCollection genericParameterHandles,
            ushort arity)
            : base(moduleSymbol,
                container,
                handle,
                emittedNamespaceName,
                arity,
                out var mangleName) {
            _arity = arity;

            if (_arity == 0)
                _lazyTemplateParameters = [];

            _genericParameterHandles = genericParameterHandles;
            _mangleName = mangleName;
        }

        public override int arity => _arity;

        public override ImmutableArray<TypeOrConstant> templateArguments => GetTemplateParametersAsTemplateArguments();

        public override ImmutableArray<TemplateParameterSymbol> templateParameters {
            get {
                EnsureTemplateParametersAreLoaded();
                return _lazyTemplateParameters;
            }
        }

        internal override bool mangleName => _mangleName;

        internal override int metadataArity => _genericParameterHandles.Count;

        private void EnsureTemplateParametersAreLoaded() {
            if (_lazyTemplateParameters.IsDefault) {
                var moduleSymbol = containingPEModule;
                var firstIndex = _genericParameterHandles.Count - _arity;

                var ownedParams = ArrayBuilder<TemplateParameterSymbol>.GetInstance(_arity);
                ownedParams.Count = _arity;

                for (var i = 0; i < ownedParams.Count; i++) {
                    ownedParams[i] = new PETemplateParameterSymbol(
                        moduleSymbol,
                        this,
                        (ushort)i, _genericParameterHandles[firstIndex + i]
                    );
                }

                ImmutableInterlocked.InterlockedInitialize(
                    ref _lazyTemplateParameters,
                    ownedParams.ToImmutableAndFree()
                );
            }
        }

        private bool MatchesContainingTypeParameters() {
            var container = containingType;

            if (container is null)
                return true;

            var containingTypeParameters = container.GetAllTypeParameters();
            var n = containingTypeParameters.Length;

            if (n == 0)
                return true;

            var nestedType = Create(containingPEModule, (PENamespaceSymbol)containingNamespace, _handle, null);
            var nestedTypeParameters = nestedType.templateParameters;
            var containingTypeMap = new TemplateMap(containingTypeParameters, IndexedTemplateParameterSymbol.Take(n));
            var nestedTypeMap = new TemplateMap(
                nestedTypeParameters,
                IndexedTemplateParameterSymbol.Take(nestedTypeParameters.Length)
            );

            for (var i = 0; i < n; i++) {
                var containingTypeParameter = containingTypeParameters[i];
                var nestedTypeParameter = nestedTypeParameters[i];

                if (!MemberSignatureComparer.HaveSameConstraints(
                    containingTypeParameter,
                    containingTypeMap,
                    nestedTypeParameter,
                    nestedTypeMap)) {
                    return false;
                }
            }

            return true;
        }
    }
}

using System;
using System.Linq;
using System.Threading;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class MissingMetadataTypeSymbol {
    internal sealed class TopLevel : MissingMetadataTypeSymbol {
        private readonly string _namespaceName;
        private readonly ModuleSymbol _containingModule;
        private BelteDiagnostic _lazyErrorInfo;
        private NamespaceSymbol _lazyContainingNamespace;
        private int _lazyTypeId;

        internal TopLevel(ModuleSymbol module, string @namespace, string name, int arity, bool mangleName)
            : this(
                module,
                @namespace,
                name,
                arity,
                mangleName,
                errorInfo: null,
                containingNamespace: null,
                typeId: -1
            ) {
        }

        internal TopLevel(ModuleSymbol module, ref MetadataTypeName fullName, BelteDiagnostic errorInfo = null)
            : this(module, ref fullName, -1, errorInfo) {
        }

        private TopLevel(ModuleSymbol module, ref MetadataTypeName fullName, int typeId, BelteDiagnostic errorInfo)
            : this(
                module,
                ref fullName,
                fullName.forcedArity == -1 || fullName.forcedArity == fullName.inferredArity,
                errorInfo,
                typeId
            ) {
        }

        private TopLevel(
            ModuleSymbol module,
            ref MetadataTypeName fullName,
            bool mangleName,
            BelteDiagnostic errorInfo,
            int typeId)
            : this(module, fullName.namespaceName,
                mangleName ? fullName.unmangledTypeName : fullName.typeName,
                mangleName ? fullName.inferredArity : fullName.forcedArity,
                mangleName,
                errorInfo,
                containingNamespace: null,
                typeId
            ) {
        }

        private TopLevel(
            ModuleSymbol module,
            string @namespace,
            string name,
            int arity,
            bool mangleName,
            BelteDiagnostic errorInfo,
            NamespaceSymbol containingNamespace,
            int typeId)
            : base(name, arity, mangleName) {
            _namespaceName = @namespace;
            _containingModule = module;
            _lazyErrorInfo = errorInfo;
            _lazyContainingNamespace = containingNamespace;
            _lazyTypeId = typeId;
        }

        internal string namespaceName => _namespaceName;

        internal override AssemblySymbol containingAssembly => _containingModule.containingAssembly;

        internal override Symbol containingSymbol {
            get {
                if (_lazyContainingNamespace is null) {
                    var container = _containingModule.globalNamespace;

                    if (_namespaceName.Length > 0) {
                        var namespaces = MetadataHelpers.SplitQualifiedName(_namespaceName);
                        int i;

                        for (i = 0; i < namespaces.Length; i++) {
                            NamespaceSymbol newContainer = null;

                            foreach (var symbol in container.GetMembers(namespaces[i]).Cast<NamespaceOrTypeSymbol>()) {
                                if (symbol.kind == SymbolKind.Namespace) {
                                    newContainer = (NamespaceSymbol)symbol;
                                    break;
                                }
                            }

                            if (newContainer is null)
                                break;

                            container = newContainer;
                        }

                        for (; i < namespaces.Length; i++)
                            container = new MissingNamespaceSymbol(container, namespaces[i]);
                    }

                    Interlocked.CompareExchange(ref _lazyContainingNamespace, container, null);
                }

                return _lazyContainingNamespace;
            }
        }

        private int _typeId {
            get {
                if (_lazyTypeId == -1) {
                    SpecialType typeId = default;
                    var containingAssembly = _containingModule.containingAssembly;

                    if ((arity == 0 || mangleName) &&
                        containingAssembly is not null &&
                        _containingModule.ordinal == 0) {
                        var emittedName = MetadataHelpers.BuildQualifiedName(_namespaceName, metadataName);
                        typeId = SpecialTypes.GetTypeFromMetadataName(emittedName);
                    }

                    Interlocked.CompareExchange(ref _lazyTypeId, (int)typeId, -1);
                }

                return _lazyTypeId;
            }
        }

        internal override BelteDiagnostic error {
            get {
                if (_lazyErrorInfo is null) {
                    // TODO error
                    // var errorInfo = _typeId != (int)SpecialType.None
                    //     ? new CSDiagnosticInfo(ErrorCode.ERR_PredefinedTypeNotFound, MetadataHelpers.BuildQualifiedName(_namespaceName, MetadataName))
                    //     : base.ErrorInfo;
                    BelteDiagnostic errorInfo = null;
                    Interlocked.CompareExchange(ref _lazyErrorInfo, errorInfo, null);
                }

                return _lazyErrorInfo;
            }
        }

        public override int GetHashCode() {
            if (specialType == SpecialType.Object)
                return (int)SpecialType.Object;

            return Hash.Combine(metadataName, Hash.Combine(_containingModule, Hash.Combine(_namespaceName, _arity)));
        }

        internal override bool Equals(TypeSymbol t2, TypeCompareKind comparison) {
            if (ReferenceEquals(this, t2))
                return true;

            if (t2 is not TopLevel other)
                return false;

            return string.Equals(metadataName, other.metadataName, StringComparison.Ordinal) &&
                _arity == other._arity &&
                string.Equals(_namespaceName, other.namespaceName, StringComparison.Ordinal) &&
                _containingModule.Equals(other._containingModule);
        }
    }
}

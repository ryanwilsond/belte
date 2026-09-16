using System.Collections.Immutable;
using System.Diagnostics;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceOrdinaryMethodSymbol {
    private sealed class SourceComplexOrdinaryMethodSymbol : SourceOrdinaryMethodSymbol {
        private TemplateParameterInfo _templateParameterInfo;
        private readonly TypeSymbol _fieldExplicitInterfaceType;

        internal SourceComplexOrdinaryMethodSymbol(
            NamedTypeSymbol containingType,
            TypeSymbol explicitInterfaceType,
            string name,
            MethodDeclarationSyntax syntax,
            MethodKind methodKind,
            BelteDiagnosticQueue diagnostics)
            : base(containingType, name, syntax, methodKind, diagnostics) {
            var templateParameters = MakeTemplateParameters(syntax, diagnostics);
            _fieldExplicitInterfaceType = explicitInterfaceType;
            Debug.Assert(_templateParameterInfo is null);
            _templateParameterInfo = templateParameters.IsEmpty
                ? TemplateParameterInfo.Empty
                : new TemplateParameterInfo { lazyTemplateParameters = templateParameters };
        }

        public sealed override ImmutableArray<TemplateParameterSymbol> templateParameters
            // TODO This is only null when displaying this symbol for a diagnostic produced by the base constructor
            // Perhaps there is a way to fix this so the template parameters aren't just gone in some error messages
            => _templateParameterInfo?.lazyTemplateParameters ?? [];

        public override ImmutableArray<BoundExpression> templateConstraints
            => _templateParameterInfo?.lazyTemplateConstraints ?? [];

        private protected sealed override TypeSymbol _explicitInterfaceType => _fieldExplicitInterfaceType;

        internal sealed override ImmutableArray<ImmutableArray<TypeWithAnnotations>> GetTypeParameterConstraintTypes() {
            return GetTypeParameterConstraintTypesCore(ref _templateParameterInfo, GetSyntax());
        }

        internal sealed override ImmutableArray<TypeParameterConstraintKinds> GetTypeParameterConstraintKinds() {
            return GetTypeParameterConstraintKindsCore(ref _templateParameterInfo, GetSyntax());
        }

        internal sealed override ImmutableArray<BoundExpression> GetTemplateConstraints() {
            return GetTemplateConstraintsCore(ref _templateParameterInfo, GetSyntax());
        }

        private protected sealed override MethodSymbol FindExplicitlyImplementedMethod(
            BelteDiagnosticQueue diagnostics) {
            var syntax = GetSyntax();
            return this.FindExplicitlyImplementedMethod(
                isOperator: false,
                _explicitInterfaceType,
                syntax.identifier.valueText,
                syntax.explicitInterfaceSpecifier,
                diagnostics
            );
        }
    }
}

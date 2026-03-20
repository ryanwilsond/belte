using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceOrdinaryMethodSymbol {
    private sealed class SourceSimpleOrdinaryMethodSymbol : SourceOrdinaryMethodSymbol {
        internal SourceSimpleOrdinaryMethodSymbol(
            NamedTypeSymbol containingType,
            string name,
            MethodDeclarationSyntax syntax,
            MethodKind methodKind,
            BelteDiagnosticQueue diagnosticQueue)
            : base(containingType, name, syntax, methodKind, diagnosticQueue) { }

        public sealed override ImmutableArray<TemplateParameterSymbol> templateParameters => [];

        public sealed override ImmutableArray<BoundExpression> templateConstraints => [];

        internal sealed override ImmutableArray<ImmutableArray<TypeWithAnnotations>> GetTypeParameterConstraintTypes() {
            return [];
        }

        internal sealed override ImmutableArray<TypeParameterConstraintKinds> GetTypeParameterConstraintKinds() {
            return [];
        }
    }
}

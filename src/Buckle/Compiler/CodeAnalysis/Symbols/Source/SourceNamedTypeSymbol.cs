using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceNamedTypeSymbol : SourceMemberContainerTypeSymbol {
    private readonly TemplateParameterInfo _templateParameterInfo;

    internal SourceNamedTypeSymbol(
        NamespaceOrTypeSymbol containingSymbol,
        TypeDeclarationSyntax declaration,
        BelteDiagnosticQueue diagnostics,
        Compilation declaringCompilation)
        : base(containingSymbol, declaration, diagnostics, declaringCompilation) {
        _templateParameterInfo = arity == 0 ? TemplateParameterInfo.Empty : new TemplateParameterInfo();
    }

    internal ImmutableArray<TypeWithAnnotations> GetTypeParameterConstraintTypes(
        int ordinal,
        BelteDiagnosticQueue diagnostics) {
        var constraintTypes = GetTypeParameterConstraintTypes(diagnostics);
        return (constraintTypes.Length > 0) ? constraintTypes[ordinal] : [];
    }

    internal TypeParameterConstraintKinds GetTypeParameterConstraintKinds(int ordinal) {
        var constraintKinds = GetTypeParameterConstraintKinds();
        return (constraintKinds.Length > 0) ? constraintKinds[ordinal] : TypeParameterConstraintKinds.None;
    }

    private ImmutableArray<ImmutableArray<TypeWithAnnotations>> GetTypeParameterConstraintTypes(
        BelteDiagnosticQueue diagnostics) {
        if (_templateParameterInfo.lazyTypeParameterConstraintTypes.IsDefault) {
            GetTypeParameterConstraintKinds();

            if (ImmutableInterlocked.InterlockedInitialize(
                ref _templateParameterInfo.lazyTypeParameterConstraintTypes,
                MakeTypeParameterConstraintTypes(diagnostics))) {
                AddDeclarationDiagnostics(diagnostics);
            }
        }

        return _templateParameterInfo.lazyTypeParameterConstraintTypes;
    }

    private ImmutableArray<TypeParameterConstraintKinds> GetTypeParameterConstraintKinds() {
        if (_templateParameterInfo.lazyTypeParameterConstraintKinds.IsDefault) {
            ImmutableInterlocked.InterlockedInitialize(
                ref _templateParameterInfo.lazyTypeParameterConstraintKinds,
                MakeTypeParameterConstraintKinds());
        }

        return _templateParameterInfo.lazyTypeParameterConstraintKinds;
    }
}

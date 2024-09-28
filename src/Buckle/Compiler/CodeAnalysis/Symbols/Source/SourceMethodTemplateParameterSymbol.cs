using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceMethodTemplateParameterSymbol : SourceTemplateParameterSymbolBase {
    private readonly SourceMethodSymbol _owner;

    internal SourceMethodTemplateParameterSymbol(
        SourceMethodSymbol owner,
        string name,
        int ordinal,
        TypeWithAnnotations underlyingType,
        ConstantValue defaultValue,
        SyntaxReference syntaxReference) : base(name, ordinal, underlyingType, defaultValue, syntaxReference) {
        _owner = owner;
    }

    internal override void AddDeclarationDiagnostics(BelteDiagnosticQueue diagnostics)
        => _owner.AddDeclarationDiagnostics(diagnostics);

    internal override TemplateParameterKind templateParameterKind => TemplateParameterKind.Method;

    internal override Symbol containingSymbol => _owner;

    protected override ImmutableArray<TemplateParameterSymbol> _containerTemplateParameters
        => _owner.templateParameters;

    protected override TypeParameterBounds ResolveBounds(
        List<TemplateParameterSymbol> inProgress,
        BelteDiagnosticQueue diagnostics) {
        var constraintTypes = _owner.GetTemplateParameterConstraintTypes(ordinal);

        if (constraintTypes.IsEmpty && GetConstraintKinds() == TypeParameterConstraintKinds.None)
            return null;

        return ResolveBounds(inProgress.Prepend(this), constraintTypes, declaringCompilation, diagnostics);
    }

    private TypeParameterConstraintKinds GetConstraintKinds() {
        return _owner.GetTypeParameterConstraintKinds(ordinal);
    }
}

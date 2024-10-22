using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceMethodTemplateParameterSymbol : SourceTemplateParameterSymbolBase {
    private readonly SourceMethodSymbol _owner;

    internal SourceMethodTemplateParameterSymbol(
        SourceMethodSymbol owner,
        string name,
        int ordinal,
        SyntaxReference syntaxReference)
        : base(name, ordinal, syntaxReference) {
        _owner = owner;
    }

    internal override void AddDeclarationDiagnostics(BelteDiagnosticQueue diagnostics)
        => _owner.AddDeclarationDiagnostics(diagnostics);

    internal override TemplateParameterKind templateParameterKind => TemplateParameterKind.Method;

    internal override Symbol containingSymbol => _owner;

    internal override bool hasPrimitiveTypeConstraint {
        get {
            var constraints = GetConstraintKinds();
            return (constraints & TypeParameterConstraintKinds.Primitive) != 0;
        }
    }

    internal override bool hasObjectTypeConstraint {
        get {
            var constraints = GetConstraintKinds();
            return (constraints & TypeParameterConstraintKinds.Object) != 0;
        }
    }

    internal override bool isPrimitiveTypeFromConstraintTypes {
        get {
            var constraints = GetConstraintKinds();
            return (constraints & TypeParameterConstraintKinds.Primitive) != 0;
        }
    }

    internal override bool isObjectTypeFromConstraintTypes {
        get {
            var constraints = GetConstraintKinds();
            return (constraints & TypeParameterConstraintKinds.Object) != 0;
        }
    }

    private protected override ImmutableArray<TemplateParameterSymbol> _containerTemplateParameters
        => _owner.templateParameters;

    private protected override TypeParameterBounds ResolveBounds(
        ConsList<TemplateParameterSymbol> inProgress,
        BelteDiagnosticQueue diagnostics) {
        var constraintTypes = _owner.GetTemplateParameterConstraintTypes(ordinal);

        if (constraintTypes.IsEmpty && GetConstraintKinds() == TypeParameterConstraintKinds.None)
            return null;

        return ConstraintsHelpers.ResolveBounds(
            this,
            inProgress.Prepend(this),
            constraintTypes,
            false,
            declaringCompilation,
            diagnostics
        );
    }

    private TypeParameterConstraintKinds GetConstraintKinds() {
        return _owner.GetTypeParameterConstraintKinds(ordinal);
    }
}

using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceTemplateParameterSymbol : SourceTemplateParameterSymbolBase {
    private readonly SourceNamedTypeSymbol _owner;

    internal SourceTemplateParameterSymbol(
        SourceNamedTypeSymbol owner,
        string name,
        int ordinal,
        SyntaxReference syntaxReference)
        : base(name, ordinal, syntaxReference) {
        _owner = owner;
    }

    internal override TemplateParameterKind templateParameterKind => TemplateParameterKind.Type;

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
        var constraintTypes = _owner.GetTypeParameterConstraintTypes(ordinal, diagnostics);

        if (constraintTypes.IsEmpty && GetConstraintKinds() == TypeParameterConstraintKinds.None)
            return null;

        return this.ResolveBounds(
            inProgress.Prepend(this),
            constraintTypes,
            false,
            declaringCompilation,
            diagnostics,
            syntaxReference.location
        );
    }

    private TypeParameterConstraintKinds GetConstraintKinds() {
        return _owner.GetTypeParameterConstraintKinds(ordinal);
    }
}

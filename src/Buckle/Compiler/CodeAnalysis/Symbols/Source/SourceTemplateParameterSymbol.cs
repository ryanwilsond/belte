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

    internal override bool allowsRefLikeType {
        get {
            var constraints = GetConstraintKinds();
            return (constraints & TypeParameterConstraintKinds.AllowByRefLike) != 0;
        }
    }

    internal override bool hasNotNullConstraint {
        get {
            var constraints = GetConstraintKinds();
            return (constraints & TypeParameterConstraintKinds.NotNull) != 0;
        }
    }

    internal override bool hasValueTypeConstraint {
        get {
            var constraints = GetConstraintKinds();
            return (constraints & TypeParameterConstraintKinds.ValueType) != 0;
        }
    }

    internal override bool hasReferenceTypeConstraint {
        get {
            var constraints = GetConstraintKinds();
            return (constraints & TypeParameterConstraintKinds.ReferenceType) != 0;
        }
    }

    internal override bool hasDefaultConstraint {
        get {
            var constraints = GetConstraintKinds();
            return (constraints & TypeParameterConstraintKinds.Default) != 0;
        }
    }

    internal override bool hasConstructorConstraint {
        get {
            var constraints = GetConstraintKinds();
            return (constraints & TypeParameterConstraintKinds.Constructor) != 0;
        }
    }

    internal override bool isValueTypeFromConstraintTypes {
        get {
            var constraints = GetConstraintKinds();
            return (constraints & TypeParameterConstraintKinds.ValueType) != 0;
        }
    }

    internal override bool isReferenceTypeFromConstraintTypes {
        get {
            var constraints = GetConstraintKinds();
            return (constraints & TypeParameterConstraintKinds.ReferenceType) != 0;
        }
    }

    internal override bool hasDefaultFromConstraintTypes {
        get {
            var constraints = GetConstraintKinds();
            return (constraints & TypeParameterConstraintKinds.Default) != 0;
        }
    }

    internal override bool hasConstructorFromConstraintTypes {
        get {
            var constraints = GetConstraintKinds();
            return (constraints & TypeParameterConstraintKinds.Constructor) != 0;
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

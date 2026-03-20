using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceOrdinaryMethodOrUserDefinedOperatorSymbol : SourceMemberMethodSymbol {
    private ImmutableArray<ParameterSymbol> _lazyParameters;
    private TypeWithAnnotations _lazyReturnType;

    private protected SourceOrdinaryMethodOrUserDefinedOperatorSymbol(
        NamedTypeSymbol containingType,
        SyntaxReference syntaxReference,
        (DeclarationModifiers modifiers, Flags flags) modifiersAndFlags)
        : base(containingType, syntaxReference, modifiersAndFlags) { }

    public sealed override bool returnsVoid {
        get {
            LazyMethodChecks();
            return base.returnsVoid;
        }
    }

    internal sealed override int parameterCount {
        get {
            if (_lazyParameters.IsDefault)
                return GetParameterCountFromSyntax();

            return _lazyParameters.Length;
        }
    }

    internal sealed override ImmutableArray<ParameterSymbol> parameters {
        get {
            LazyMethodChecks();
            return _lazyParameters;
        }
    }

    internal sealed override TypeWithAnnotations returnTypeWithAnnotations {
        get {
            LazyMethodChecks();
            return _lazyReturnType;
        }
    }

    private protected abstract TextLocation _returnTypeLocation { get; }

    internal override void AfterAddingTypeMembersChecks(BelteDiagnosticQueue diagnostics) {
        base.AfterAddingTypeMembersChecks(diagnostics);

        returnType.CheckAllConstraints(syntaxReference.location, diagnostics);

        foreach (var parameter in parameters)
            parameter.type.CheckAllConstraints(parameter.syntaxReference.location, diagnostics);
    }

    private protected abstract int GetParameterCountFromSyntax();

    private protected MethodSymbol MethodChecks(
        TypeWithAnnotations returnType,
        ImmutableArray<ParameterSymbol> parameters,
        BelteDiagnosticQueue diagnostics) {
        _lazyReturnType = returnType;
        _lazyParameters = parameters;

        SetReturnsVoid(_lazyReturnType.IsVoidType());

        CheckEffectiveAccessibility(_lazyReturnType, _lazyParameters, diagnostics);

        if (isOverride)
            return overriddenMethod;

        return null;
    }
}

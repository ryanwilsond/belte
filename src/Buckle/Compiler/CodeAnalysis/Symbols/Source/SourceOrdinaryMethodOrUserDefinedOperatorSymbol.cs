using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceOrdinaryMethodOrUserDefinedOperatorSymbol : SourceMemberMethodSymbol {
    private ImmutableArray<MethodSymbol> _lazyExplicitInterfaceImplementations;
    private ImmutableArray<ParameterSymbol> _lazyParameters;
    private TypeWithAnnotations _lazyReturnType;

    private protected SourceOrdinaryMethodOrUserDefinedOperatorSymbol(
        NamedTypeSymbol containingType,
        SyntaxReference syntaxReference,
        TextLocation location,
        (DeclarationModifiers modifiers, Flags flags) modifiersAndFlags)
        : base(containingType, syntaxReference, location, modifiersAndFlags) { }

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

    internal sealed override bool isExplicitInterfaceImplementation
        => methodKind == MethodKind.ExplicitInterfaceImplementation;

    internal sealed override ImmutableArray<MethodSymbol> explicitInterfaceImplementations {
        get {
            LazyMethodChecks();
            return _lazyExplicitInterfaceImplementations;
        }
    }

    private protected abstract TypeSymbol _explicitInterfaceType { get; }

    private protected abstract TextLocation _returnTypeLocation { get; }

    private protected abstract MethodSymbol FindExplicitlyImplementedMethod(BelteDiagnosticQueue diagnostics);

    internal override void AfterAddingTypeMembersChecks(ConversionsBase conversions, BelteDiagnosticQueue diagnostics) {
        base.AfterAddingTypeMembersChecks(conversions, diagnostics);

        returnType.CheckAllConstraints(conversions, syntaxReference.location, diagnostics);

        foreach (var parameter in parameters)
            parameter.type.CheckAllConstraints(conversions, parameter.syntaxReference.location, diagnostics);
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

        // TODO Warn if explicitly defining a destructor or finalizer signature?

        MethodSymbol overriddenOrExplicitlyImplementedMethod = null;

        if (methodKind != MethodKind.ExplicitInterfaceImplementation) {
            _lazyExplicitInterfaceImplementations = [];

            if (isOverride)
                overriddenOrExplicitlyImplementedMethod = overriddenMethod;
            // TODO Some runtime in attribute thing might need to go here
        } else if (_explicitInterfaceType is not null) {
            overriddenOrExplicitlyImplementedMethod = FindExplicitlyImplementedMethod(diagnostics);

            if (overriddenOrExplicitlyImplementedMethod is not null) {
                _lazyExplicitInterfaceImplementations = [overriddenOrExplicitlyImplementedMethod];

                this.FindExplicitlyImplementedMemberVerification(overriddenOrExplicitlyImplementedMethod, diagnostics);

                TypeSymbol.CheckModifierMismatchOnImplementingMember(
                    containingType,
                    this,
                    overriddenOrExplicitlyImplementedMethod,
                    isExplicit: true,
                    diagnostics
                );
            } else {
                _lazyExplicitInterfaceImplementations = [];
            }
        }

        return overriddenOrExplicitlyImplementedMethod;
    }
}

using System.Collections.Immutable;
using System.Diagnostics;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceOrdinaryMethodOrUserDefinedOperatorSymbol : SourceMemberMethodSymbol {
    private ImmutableArray<ExpressionSyntax> _unboundConstraints;
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

        var impliedConstraints = GetEnclosingTemplateConstraints();

        returnType.CheckAllConstraints(conversions, syntaxReference.location, impliedConstraints, diagnostics);

        foreach (var parameter in parameters) {
            parameter.type.CheckAllConstraints(
                conversions,
                parameter.syntaxReference.location,
                impliedConstraints,
                diagnostics
            );
        }
    }

    private protected abstract int GetParameterCountFromSyntax();

    private protected MethodSymbol MethodChecks(
        TypeWithAnnotations returnType,
        ImmutableArray<ParameterSymbol> parameters,
        BelteDiagnosticQueue diagnostics) {
        _lazyReturnType = returnType;
        _lazyParameters = parameters;
        Debug.Assert(!_lazyParameters.IsDefault);
        Debug.Assert(_lazyReturnType is not null);

        SetReturnsVoid(_lazyReturnType.IsVoidType());

        CheckEffectiveAccessibility(_lazyReturnType, _lazyParameters, diagnostics);

        CheckSpecifiers(diagnostics);

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

    private void CheckSpecifiers(BelteDiagnosticQueue diagnostics) {
        if (isPure && isStatic && shouldMemoizeIfPure) {
            if (returnType.ContainsPointerType()) {
                diagnostics.Push(Error.MemoizeDisallowsPointers(location));
            } else if (returnsByRef) {
                diagnostics.Push(Error.MemoizeDisallowsRef(location));
            } else {
                foreach (var parameter in parameters) {
                    if (parameter.type.ContainsPointerType()) {
                        diagnostics.Push(Error.MemoizeDisallowsPointers(location));
                        break;
                    } else if (parameter.refKind != RefKind.None) {
                        diagnostics.Push(Error.MemoizeDisallowsRef(location));
                        break;
                    }
                }
            }
        }
    }

    private protected ImmutableArray<TemplateParameterSymbol> MakeTemplateParameters(
        BaseMethodDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics) {
        Debug.Assert(syntax is MethodDeclarationSyntax or ConversionDeclarationSyntax or OperatorDeclarationSyntax);

        var templateParameterList = syntax is MethodDeclarationSyntax m
            ? m.templateParameterList
            : syntax is ConversionDeclarationSyntax c
                ? c.templateParameterList
                : ((OperatorDeclarationSyntax)syntax).templateParameterList;

        if (templateParameterList is null)
            return [];

        OverriddenMethodTemplateParameterMapBase templateMap = null;

        if (isOverride)
            templateMap = new OverriddenMethodTemplateParameterMap(this);

        var templateParameters = templateParameterList.parameters;
        var result = ArrayBuilder<TemplateParameterSymbol>.GetInstance();

        for (var ordinal = 0; ordinal < templateParameters.Count; ordinal++) {
            var parameter = templateParameters[ordinal];
            var identifier = parameter.identifier;
            var location = identifier.location;
            var name = identifier.valueText;

            for (var i = 0; i < result.Count; i++) {
                if (name == result[i].name) {
                    diagnostics.Push(Error.DuplicateTemplateParameter(location, name));
                    break;
                }
            }

            var enclosingTemplateParameter = containingType.FindEnclosingTemplateParameter(name);

            if (enclosingTemplateParameter is not null) {
                // TODO Perhaps an error?
                // Type parameter '{0}' has the same name as the type parameter from outer type '{1}'
                // diagnostics.Add(ErrorCode.WRN_TypeParameterSameAsOuterTypeParameter, location, name, tpEnclosing.ContainingType);
            }

            var templateParameter = templateMap is null
                ? new SourceMethodTemplateParameterSymbol(
                    this,
                    name,
                    ordinal,
                    new SyntaxReference(parameter)
                  )
                : (TemplateParameterSymbol)new SourceOverridingMethodTemplateParameterSymbol(
                    templateMap,
                    name,
                    ordinal,
                    new SyntaxReference(parameter)
                  );

            result.Add(templateParameter);
        }

        return result.ToImmutableAndFree();
    }

    private protected ImmutableArray<ImmutableArray<TypeWithAnnotations>> GetTypeParameterConstraintTypesCore(
        ref TemplateParameterInfo templateParameterInfo,
        BaseMethodDeclarationSyntax syntax) {
        if (templateParameterInfo.lazyTypeParameterConstraintTypes.IsDefault) {
            Debug.Assert(syntax is MethodDeclarationSyntax or ConversionDeclarationSyntax or OperatorDeclarationSyntax);

            TypeSyntax returnTypeSyntax;
            TemplateParameterListSyntax templateParameterListSyntax;
            TemplateConstraintClauseListSyntax constraintClauseListSyntax;

            if (syntax is MethodDeclarationSyntax m) {
                returnTypeSyntax = m.returnType;
                templateParameterListSyntax = m.templateParameterList;
                constraintClauseListSyntax = m.constraintClauseList;
            } else if (syntax is ConversionDeclarationSyntax c) {
                returnTypeSyntax = c.type;
                templateParameterListSyntax = c.templateParameterList;
                constraintClauseListSyntax = c.constraintClauseList;
            } else if (syntax is OperatorDeclarationSyntax o) {
                returnTypeSyntax = o.returnType;
                templateParameterListSyntax = o.templateParameterList;
                constraintClauseListSyntax = o.constraintClauseList;
            } else {
                throw ExceptionUtilities.UnexpectedValue(syntax.kind);
            }

            GetTypeParameterConstraintKinds();

            var diagnostics = BelteDiagnosticQueue.GetInstance();
            var withTemplateParametersBinder = declaringCompilation
                .GetBinderFactory(syntax.syntaxTree)
                .GetBinder(returnTypeSyntax, syntax, this);

            var allConstraints = this.MakeTypeParameterConstraintTypes(
                withTemplateParametersBinder,
                templateParameters,
                templateParameterListSyntax,
                constraintClauseListSyntax?.constraintClauses,
                diagnostics
            );

            var typeConstraints = allConstraints.SelectAsArray(clause => clause.constraintTypes);

            if (ImmutableInterlocked.InterlockedInitialize(
                ref templateParameterInfo.lazyTypeParameterConstraintTypes,
                typeConstraints)) {
                AddDeclarationDiagnostics(diagnostics);
            }

            diagnostics.Free();

            var constraintsBuilder = ArrayBuilder<ExpressionSyntax>.GetInstance();

            foreach (var constraint in allConstraints) {
                if ((constraint.constraints & TypeParameterConstraintKinds.Expression) != 0)
                    constraintsBuilder.Add(constraint.expression);
            }

            ImmutableInterlocked.InterlockedInitialize(
                ref _unboundConstraints,
                constraintsBuilder.ToImmutableAndFree()
            );
        }

        return templateParameterInfo.lazyTypeParameterConstraintTypes;
    }

    private protected ImmutableArray<TypeParameterConstraintKinds> GetTypeParameterConstraintKindsCore(
        ref TemplateParameterInfo templateParameterInfo,
        BaseMethodDeclarationSyntax syntax) {
        if (templateParameterInfo.lazyTypeParameterConstraintKinds.IsDefault) {
            Debug.Assert(syntax is MethodDeclarationSyntax or ConversionDeclarationSyntax or OperatorDeclarationSyntax);

            TypeSyntax returnTypeSyntax;
            TemplateParameterListSyntax templateParameterListSyntax;
            TemplateConstraintClauseListSyntax constraintClauseListSyntax;

            if (syntax is MethodDeclarationSyntax m) {
                returnTypeSyntax = m.returnType;
                templateParameterListSyntax = m.templateParameterList;
                constraintClauseListSyntax = m.constraintClauseList;
            } else if (syntax is ConversionDeclarationSyntax c) {
                returnTypeSyntax = c.type;
                templateParameterListSyntax = c.templateParameterList;
                constraintClauseListSyntax = c.constraintClauseList;
            } else if (syntax is OperatorDeclarationSyntax o) {
                returnTypeSyntax = o.returnType;
                templateParameterListSyntax = o.templateParameterList;
                constraintClauseListSyntax = o.constraintClauseList;
            } else {
                throw ExceptionUtilities.UnexpectedValue(syntax.kind);
            }

            var withTemplateParametersBinder = declaringCompilation
                .GetBinderFactory(syntax.syntaxTree)
                .GetBinder(returnTypeSyntax, syntax, this);

            var constraints = this.MakeTypeParameterConstraintKinds(
                withTemplateParametersBinder,
                templateParameters,
                templateParameterListSyntax,
                constraintClauseListSyntax?.constraintClauses
            );

            ImmutableInterlocked.InterlockedInitialize(
                ref templateParameterInfo.lazyTypeParameterConstraintKinds,
                constraints
            );
        }

        return templateParameterInfo.lazyTypeParameterConstraintKinds;
    }

    private protected ImmutableArray<BoundExpression> GetTemplateConstraintsCore(
        ref TemplateParameterInfo templateParameterInfo,
        BaseMethodDeclarationSyntax syntax) {
        if (templateParameterInfo.lazyTemplateConstraints.IsDefault) {
            Debug.Assert(syntax is MethodDeclarationSyntax or ConversionDeclarationSyntax or OperatorDeclarationSyntax);

            _ = GetTypeParameterConstraintTypes();

            if (_unboundConstraints.IsDefault || _unboundConstraints.Length == 0) {
                ImmutableInterlocked.InterlockedInitialize(
                    ref templateParameterInfo.lazyTemplateConstraints,
                    []
                );
            } else {
                var returnTypeSyntax = syntax is MethodDeclarationSyntax m
                    ? m.returnType
                    : syntax is ConversionDeclarationSyntax c
                        ? c.type
                        : ((OperatorDeclarationSyntax)syntax).returnType;

                var withTemplateParametersBinder = declaringCompilation
                    .GetBinderFactory(syntax.syntaxTree)
                    .GetBinder(returnTypeSyntax, syntax, this);

                var signatureFlags = BinderFlags.TemplateConstraintsClause | BinderFlags.SuppressConstraintChecks;

                if (isLowLevel)
                    signatureFlags |= BinderFlags.LowLevelContext;

                var signatureBinder = withTemplateParametersBinder.WithAdditionalFlagsAndContainingMember(
                    signatureFlags,
                    this
                );

                var diagnostics = BelteDiagnosticQueue.GetInstance();
                var constraints = signatureBinder.BindExpressionConstraints(
                    _unboundConstraints,
                    templateParameters,
                    diagnostics
                );

                if (ImmutableInterlocked.InterlockedInitialize(
                    ref templateParameterInfo.lazyTemplateConstraints,
                    constraints)) {
                    AddDeclarationDiagnostics(diagnostics);
                }

                diagnostics.Free();
            }
        }

        return templateParameterInfo.lazyTemplateConstraints;
    }
}

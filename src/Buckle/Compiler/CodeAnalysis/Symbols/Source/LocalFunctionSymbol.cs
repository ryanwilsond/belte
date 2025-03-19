using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class LocalFunctionSymbol : SourceMethodSymbol {
    private readonly RefKind _refKind;
    private readonly DeclarationModifiers _modifiers;
    private readonly BelteDiagnosticQueue _declarationDiagnostics;
    private readonly ImmutableArray<SourceMethodTemplateParameterSymbol> _templateParameters;

    private ImmutableArray<ImmutableArray<TypeWithAnnotations>> _lazyTypeParameterConstraintTypes;
    private ImmutableArray<TypeParameterConstraintKinds> _lazyTypeParameterConstraintKinds;
    private ImmutableArray<ParameterSymbol> _lazyParameters;
    private TypeWithAnnotations _lazyReturnType;

    internal LocalFunctionSymbol(Binder binder, Symbol containingSymbol, LocalFunctionStatementSyntax syntax)
        : base(new SyntaxReference(syntax)) {
        this.containingSymbol = containingSymbol;
        _declarationDiagnostics = new BelteDiagnosticQueue();
        _modifiers = DeclarationModifiers.Private |
            ModifierHelpers.CreateModifiers(syntax.modifiers, _declarationDiagnostics, out _);

        scopeBinder = binder;
        location = syntax.identifier.location;

        if (syntax.templateParameterList is not null) {
            _templateParameters = MakeTemplateParameters(_declarationDiagnostics);
        } else {
            _templateParameters = [];
            ReportErrorIfHasConstraints(syntax.constraintClauseList, _declarationDiagnostics);
        }

        syntax.returnType.SkipRef(out _refKind);
    }

    public override string name => syntax.identifier.text ?? "";

    public override RefKind refKind => _refKind;

    public override MethodKind methodKind => MethodKind.LocalFunction;

    public override bool returnsVoid => returnType.IsVoidType();

    public override int arity => templateParameters.Length;

    public override ImmutableArray<TemplateParameterSymbol> templateParameters
        => _templateParameters.Cast<SourceMethodTemplateParameterSymbol, TemplateParameterSymbol>();

    public override ImmutableArray<TypeOrConstant> templateArguments => GetTemplateParametersAsTemplateArguments();

    // TODO this should be something
    public override ImmutableArray<BoundExpression> templateConstraints => [];

    internal override ImmutableArray<ParameterSymbol> parameters {
        get {
            ComputeParameters();
            return _lazyParameters;
        }
    }

    internal override TypeWithAnnotations returnTypeWithAnnotations {
        get {
            ComputeReturnType();
            return _lazyReturnType;
        }
    }

    internal Binder scopeBinder { get; }

    internal override Binder outerBinder => scopeBinder;

    internal override Binder withTemplateParametersBinder
        => _templateParameters.IsEmpty ? scopeBinder : new WithMethodTemplateParametersBinder(this, scopeBinder);

    internal override Symbol containingSymbol { get; }

    internal LocalFunctionStatementSyntax syntax => (LocalFunctionStatementSyntax)syntaxReference.node;

    internal override TextLocation location { get; }

    internal SyntaxToken identifier => syntax.identifier;

    internal override Accessibility declaredAccessibility => ModifierHelpers.EffectiveAccessibility(_modifiers);

    internal override bool isStatic => (_modifiers & DeclarationModifiers.Static) != 0;

    internal override bool isVirtual => (_modifiers & DeclarationModifiers.Virtual) != 0;

    internal override bool isOverride => (_modifiers & DeclarationModifiers.Override) != 0;

    internal override bool isAbstract => (_modifiers & DeclarationModifiers.Abstract) != 0;

    internal override bool isSealed => (_modifiers & DeclarationModifiers.Sealed) != 0;

    internal override bool isDeclaredConst => false;

    internal override CallingConvention callingConvention => CallingConvention.Default;

    internal override void AddDeclarationDiagnostics(BelteDiagnosticQueue diagnostics) {
        _declarationDiagnostics.PushRange(diagnostics);
    }

    internal override ImmutableArray<ImmutableArray<TypeWithAnnotations>> GetTypeParameterConstraintTypes() {
        if (_lazyTypeParameterConstraintTypes.IsDefault) {
            GetTypeParameterConstraintKinds();

            var syntax = this.syntax;
            var diagnostics = BelteDiagnosticQueue.GetInstance();

            var constraints = this.MakeTypeParameterConstraintTypes(
                withTemplateParametersBinder,
                templateParameters,
                syntax.templateParameterList,
                syntax.constraintClauseList.constraintClauses,
                diagnostics
            );

            lock (_declarationDiagnostics) {
                if (_lazyTypeParameterConstraintTypes.IsDefault) {
                    _declarationDiagnostics.PushRange(diagnostics);
                    _lazyTypeParameterConstraintTypes = constraints;
                }
            }

            diagnostics.Free();
        }

        return _lazyTypeParameterConstraintTypes;
    }

    internal override ImmutableArray<TypeParameterConstraintKinds> GetTypeParameterConstraintKinds() {
        if (_lazyTypeParameterConstraintKinds.IsDefault) {
            var syntax = this.syntax;
            var constraints = this.MakeTypeParameterConstraintKinds(
                withTemplateParametersBinder,
                templateParameters,
                syntax.templateParameterList,
                syntax.constraintClauseList.constraintClauses
            );

            ImmutableInterlocked.InterlockedInitialize(ref _lazyTypeParameterConstraintKinds, constraints);
        }

        return _lazyTypeParameterConstraintKinds;
    }

    internal void GetDeclarationDiagnostics(BelteDiagnosticQueue addTo) {
        foreach (var templateParameter in _templateParameters)
            templateParameter.ForceComplete(null);

        ComputeParameters();

        foreach (var p in _lazyParameters)
            p.ForceComplete(null);

        ComputeReturnType();

        addTo.PushRange(_declarationDiagnostics);
    }

    internal void ComputeReturnType() {
        if (_lazyReturnType is not null)
            return;

        var diagnostics = BelteDiagnosticQueue.GetInstance();

        var returnTypeSyntax = syntax.returnType;
        var returnType = withTemplateParametersBinder.BindType(returnTypeSyntax.SkipRef(out _), diagnostics);

        lock (_declarationDiagnostics) {
            if (_lazyReturnType is not null) {
                diagnostics.Free();
                return;
            }

            _declarationDiagnostics.PushRange(diagnostics);
            diagnostics.Free();
            Interlocked.CompareExchange(ref _lazyReturnType, returnType, null);
        }
    }

    internal override bool TryGetThisParameter(out ParameterSymbol thisParameter) {
        thisParameter = null;
        return true;
    }

    private void ComputeParameters() {
        if (!_lazyParameters.IsDefault)
            return;

        var diagnostics = BelteDiagnosticQueue.GetInstance();

        var parameters = ParameterHelpers.MakeParameters(
            withTemplateParametersBinder,
            this,
            syntax.parameterList.parameters,
            diagnostics,
            true,
            false
        ).Cast<SourceParameterSymbol, ParameterSymbol>();

        lock (_declarationDiagnostics) {
            if (!_lazyParameters.IsDefault) {
                diagnostics.Free();
                return;
            }

            _declarationDiagnostics.PushRange(diagnostics);
            diagnostics.Free();
            _lazyParameters = parameters;
        }
    }

    private ImmutableArray<SourceMethodTemplateParameterSymbol> MakeTemplateParameters(
        BelteDiagnosticQueue diagnostics) {
        var result = ArrayBuilder<SourceMethodTemplateParameterSymbol>.GetInstance();
        var parameters = syntax.templateParameterList?.parameters ?? default;

        for (var ordinal = 0; ordinal < parameters.Count; ordinal++) {
            var parameter = parameters[ordinal];
            var identifier = parameter.identifier;
            var location = identifier.location;
            var name = identifier.text ?? "";

            foreach (var @param in result) {
                if (name == @param.name) {
                    // TODO
                    // diagnostics.Add(ErrorCode.ERR_DuplicateTypeParameter, location, name);
                    break;
                }
            }

            var enclosingTemplateParameter = containingSymbol.FindEnclosingTemplateParameter(name);

            if (enclosingTemplateParameter is not null) {
                // TODO
                if (enclosingTemplateParameter.containingSymbol.kind == SymbolKind.Method) {
                    // Type parameter '{0}' has the same name as the type parameter from outer method '{1}'
                    // typeError = ErrorCode.WRN_TypeParameterSameAsOuterMethodTypeParameter;
                } else {
                    // Type parameter '{0}' has the same name as the type parameter from outer type '{1}'
                    // typeError = ErrorCode.WRN_TypeParameterSameAsOuterTypeParameter;
                }
            }

            var templateParameter = new SourceMethodTemplateParameterSymbol(
                this,
                name,
                ordinal,
                new SyntaxReference(parameter)
            );

            result.Add(templateParameter);
        }

        return result.ToImmutableAndFree();
    }
}

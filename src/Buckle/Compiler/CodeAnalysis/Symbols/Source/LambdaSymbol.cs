using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class LambdaSymbol : SourceMethodSymbol {
    private readonly Binder _binder;
    private readonly Symbol _containingSymbol;
    private readonly SyntaxNode _syntax;
    // private readonly ImmutableArray<ParameterSymbol> _parameters;
    private RefKind _refKind;
    private TypeWithAnnotations _returnType;
    // private readonly bool _isSynthesized;
    // private readonly bool _isAsync;
    // private readonly bool _isStatic;
    // private readonly BelteDiagnosticQueue _declarationDiagnostics;

    internal static readonly TypeSymbol ReturnTypeIsBeingInferred = new UnsupportedMetadataTypeSymbol();

    internal static readonly TypeSymbol InferenceFailureReturnType = new UnsupportedMetadataTypeSymbol();

    public LambdaSymbol(
        Binder binder,
        Compilation compilation,
        Symbol containingSymbol,
        UnboundLambda unboundLambda,
        ImmutableArray<TypeWithAnnotations> parameterTypes,
        ImmutableArray<RefKind> parameterRefKinds,
        RefKind refKind,
        TypeWithAnnotations returnType)
        : base(new SyntaxReference(unboundLambda.syntax)) {
        _binder = binder;
        _containingSymbol = containingSymbol;
        _syntax = unboundLambda.syntax;

        // if (!unboundLambda.HasExplicitReturnType(out _refKind, out _returnType)) {
        //     _refKind = refKind;
        //     _returnType = !returnType.HasType ? TypeWithAnnotations.Create(ReturnTypeIsBeingInferred) : returnType;
        // }
        // _isStatic = unboundLambda.isStatic;
        // _parameters = MakeParameters(unboundLambda, parameterTypes, parameterRefKinds);
        // _declarationDiagnostics = new BelteDiagnosticQueue();
    }

    public override MethodKind methodKind => MethodKind.AnonymousFunction;

    internal override bool isExtern => false;

    internal override bool isSealed => false;

    internal override bool isAbstract => false;

    internal override bool isVirtual => false;

    internal override bool isOverride => false;

    // internal override bool isStatic => _isStatic;
    internal override bool isStatic => false;

    internal override bool IsMetadataVirtual(bool forceComplete = false) {
        return false;
    }

    internal override bool isMetadataFinal => false;

    internal override bool hasSpecialName => false;

    public override bool returnsVoid => returnTypeWithAnnotations.hasType && returnType.IsVoidType();

    public override RefKind refKind => _refKind;

    internal override TypeWithAnnotations returnTypeWithAnnotations => _returnType;

    internal void SetInferredReturnType(RefKind refKind, TypeWithAnnotations inferredReturnType) {
        _refKind = refKind;
        _returnType = inferredReturnType;
    }

    public override ImmutableArray<TypeOrConstant> templateArguments => [];

    public override ImmutableArray<TemplateParameterSymbol> templateParameters => [];

    public override ImmutableArray<BoundExpression> templateConstraints => [];

    public override int arity => 0;

    // internal override ImmutableArray<ParameterSymbol> parameters => _parameters;
    internal override ImmutableArray<ParameterSymbol> parameters => [];

    internal override bool TryGetThisParameter(out ParameterSymbol thisParameter) {
        thisParameter = null;
        return true;
    }

    internal override Accessibility declaredAccessibility => Accessibility.Private;

    internal override ImmutableArray<TextLocation> locations => [_syntax.location];

    internal override TextLocation location => _syntax.location;

    internal TextLocation diagnosticLocation => _syntax switch {
        LambdaExpressionSyntax syntax => syntax.arrowToken.location,
        _ => location
    };

    private bool _hasExplicitReturnType => _syntax is ParenthesizedLambdaExpressionSyntax { returnType: not null };

    internal override Symbol containingSymbol => _containingSymbol;

    internal override CallingConvention callingConvention => CallingConvention.Default;

    internal override Binder outerBinder => _binder;

    internal override Binder withTemplateParametersBinder => _binder;

    // internal override bool isImplicitlyDeclared => _isSynthesized;
    internal override bool isImplicitlyDeclared => false;

    internal override bool isDeclaredConst => false;

    internal override OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations() {
        return _syntax is LambdaExpressionSyntax lambdaSyntax
            ? OneOrMany.Create(lambdaSyntax.attributeLists)
            : default;
    }

    internal void GetDeclarationDiagnostics(BelteDiagnosticQueue addTo) {
        // foreach (var parameter in _parameters)
        //     parameter.ForceComplete(null);

        GetAttributes();
        GetReturnTypeAttributes(); ;

        // addTo.PushRange(_declarationDiagnostics);
    }

    internal override void AddDeclarationDiagnostics(BelteDiagnosticQueue diagnostics) {
        // _declarationDiagnostics.PushRange(diagnostics);
    }

    private ImmutableArray<ParameterSymbol> MakeParameters(
        Compilation compilation,
        UnboundLambda unboundLambda,
        ImmutableArray<TypeWithAnnotations> parameterTypes,
        ImmutableArray<RefKind> parameterRefKinds) {
        // if (!unboundLambda.hasSignature || unboundLambda.ParameterCount == 0) {
        //     // The parameters may be omitted in source, but they are still present on the symbol.
        //     return parameterTypes.SelectAsArray((type, ordinal, arg) =>
        //                                             SynthesizedParameterSymbol.Create(
        //                                                 arg.owner,
        //                                                 type,
        //                                                 ordinal,
        //                                                 arg.refKinds[ordinal],
        //                                                 GeneratedNames.LambdaCopyParameterName(ordinal)), // Make sure nothing binds to this.
        //                                          (owner: this, refKinds: parameterRefKinds));
        // }

        // var builder = ArrayBuilder<ParameterSymbol>.GetInstance(unboundLambda.ParameterCount);
        // var hasExplicitlyTypedParameterList = unboundLambda.HasExplicitlyTypedParameterList;
        // var numDelegateParameters = parameterTypes.Length;

        // for (int p = 0; p < unboundLambda.ParameterCount; ++p) {
        //     // If there are no types given in the lambda then use the delegate type.
        //     // If the lambda is typed then the types probably match the delegate types;
        //     // if they do not, use the lambda types for binding. Either way, if we
        //     // can, then we use the lambda types. (Whatever you do, do not use the names
        //     // in the delegate parameters; they are not in scope!)

        //     TypeWithAnnotations type;
        //     RefKind refKind;
        //     ScopedKind scope;
        //     ParameterSyntax? paramSyntax = null;
        //     if (hasExplicitlyTypedParameterList) {
        //         type = unboundLambda.ParameterTypeWithAnnotations(p);
        //         refKind = unboundLambda.RefKind(p);
        //         scope = unboundLambda.DeclaredScope(p);
        //         paramSyntax = unboundLambda.ParameterSyntax(p);
        //     } else if (p < numDelegateParameters) {
        //         type = parameterTypes[p];
        //         refKind = RefKind.None;
        //         scope = ScopedKind.None;
        //     } else {
        //         type = TypeWithAnnotations.Create(new ExtendedErrorTypeSymbol(compilation, name: string.Empty, arity: 0, errorInfo: null));
        //         refKind = RefKind.None;
        //         scope = ScopedKind.None;
        //     }

        //     var attributeLists = unboundLambda.ParameterAttributes(p);
        //     var name = unboundLambda.ParameterName(p);
        //     var location = unboundLambda.ParameterLocation(p);
        //     var isParams = paramSyntax?.Modifiers.Any(static m => m.IsKind(SyntaxKind.ParamsKeyword)) ?? false;

        //     var parameter = new LambdaParameterSymbol(owner: this, paramSyntax?.GetReference(), attributeLists, type, ordinal: p, refKind, scope, name, unboundLambda.ParameterIsDiscard(p), isParams, location);
        //     builder.Add(parameter);
        // }

        // var result = builder.ToImmutableAndFree();

        // return result;
        // TODO
        return [];
    }

    internal sealed override bool Equals(Symbol symbol, TypeCompareKind compareKind) {
        if ((object)this == symbol)
            return true;

        return symbol is LambdaSymbol lambda
            && lambda._syntax == _syntax
            && lambda._refKind == _refKind
            && TypeSymbol.Equals(lambda.returnType, returnType, compareKind)
            && parameterTypesWithAnnotations.SequenceEqual(
                lambda.parameterTypesWithAnnotations,
                compareKind,
                (p1, p2, compareKind) => p1.Equals(p2, compareKind))
            && lambda.containingSymbol.Equals(containingSymbol, compareKind);
    }

    public override int GetHashCode() {
        return _syntax.GetHashCode();
    }

    internal override ImmutableArray<ImmutableArray<TypeWithAnnotations>> GetTypeParameterConstraintTypes() {
        return [];
    }

    internal override ImmutableArray<TypeParameterConstraintKinds> GetTypeParameterConstraintKinds() {
        return [];
    }

    internal override int CalculateLocalSyntaxOffset(int localPosition, SyntaxTree localTree) {
        throw ExceptionUtilities.Unreachable();
    }

    private protected override void NoteAttributesComplete(bool forReturnType) {
    }
}

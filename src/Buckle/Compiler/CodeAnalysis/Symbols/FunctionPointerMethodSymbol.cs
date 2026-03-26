using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class FunctionPointerMethodSymbol : MethodSymbol {
    private readonly ImmutableArray<FunctionPointerParameterSymbol> _parameters;

    private FunctionPointerMethodSymbol(
        CallingConvention callingConvention,
        RefKind refKind,
        TypeWithAnnotations returnType,
        ImmutableArray<ParameterSymbol> originalParameters,
        ImmutableArray<TypeOrConstant> substitutedParameterTypes) {
        this.callingConvention = callingConvention;
        this.refKind = refKind;
        returnTypeWithAnnotations = returnType;

        if (originalParameters.Length > 0) {
            var paramsBuilder = ArrayBuilder<FunctionPointerParameterSymbol>.GetInstance(originalParameters.Length);

            for (var i = 0; i < originalParameters.Length; i++) {
                var originalParam = originalParameters[i];
                var substitutedType = substitutedParameterTypes[i];

                paramsBuilder.Add(new FunctionPointerParameterSymbol(
                    substitutedType.type,
                    originalParam.refKind,
                    originalParam.ordinal,
                    containingSymbol: this
                ));
            }

            _parameters = paramsBuilder.ToImmutableAndFree();
        } else {
            _parameters = [];
        }
    }

    private FunctionPointerMethodSymbol(
        CallingConvention callingConvention,
        RefKind refKind,
        TypeWithAnnotations returnType,
        FunctionPointerSyntax syntax,
        Binder typeBinder,
        BelteDiagnosticQueue diagnostics) {
        this.callingConvention = callingConvention;
        this.refKind = refKind;
        returnTypeWithAnnotations = returnType;

        _parameters = syntax.parameterList.parameters.Count > 0
            ? ParameterHelpers.MakeFunctionPointerParameters(
                typeBinder,
                this,
                syntax.parameterList.parameters,
                diagnostics)
            : [];
    }

    private FunctionPointerMethodSymbol(
        CallingConvention callingConvention,
        RefKind refKind,
        TypeWithAnnotations returnTypeWithAnnotations,
        ImmutableArray<TypeWithAnnotations> parameterTypes,
        ImmutableArray<RefKind> parameterRefKinds) {
        this.refKind = refKind;
        this.callingConvention = callingConvention;
        this.returnTypeWithAnnotations = returnTypeWithAnnotations;

        _parameters = parameterTypes.ZipAsArray(parameterRefKinds, this,
            (type, refKind, i, arg) => {
                return new FunctionPointerParameterSymbol(type, refKind, i, arg);
            }
        );
    }

    internal static FunctionPointerMethodSymbol CreateFromParts(
        CallingConvention callingConvention,
        TypeWithAnnotations returnTypeWithAnnotations,
        RefKind returnRefKind,
        ImmutableArray<TypeWithAnnotations> parameterTypes,
        ImmutableArray<RefKind> parameterRefKinds) {
        return new FunctionPointerMethodSymbol(
            callingConvention,
            returnRefKind,
            returnTypeWithAnnotations,
            parameterTypes,
            parameterRefKinds
        );
    }

    internal static FunctionPointerMethodSymbol CreateFromSource(
        CallingConvention callingConvention,
        FunctionPointerSyntax syntax,
        Binder typeBinder,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved) {

        var refKind = RefKind.None;
        TypeWithAnnotations returnType;

        returnType = typeBinder.BindType(syntax.returnType, diagnostics, basesBeingResolved);

        // TODO Error checks
        // if (returnType.IsVoidType() && refKind != RefKind.None) {
        //     diagnostics.Add(ErrorCode.ERR_NoVoidHere, returnTypeParameter.Location);
        // } else if (returnType.IsStatic) {
        //     diagnostics.Add(ErrorFacts.GetStaticClassReturnCode(useWarning: false), returnTypeParameter.Location, returnType);
        // } else if (returnType.IsRestrictedType(ignoreSpanLikeTypes: true)) {
        //     diagnostics.Add(ErrorCode.ERR_MethodReturnCantBeRefAny, returnTypeParameter.Location, returnType);
        // }

        return new FunctionPointerMethodSymbol(
            callingConvention,
            refKind,
            returnType,
            syntax,
            typeBinder,
            diagnostics
        );
    }

    internal FunctionPointerMethodSymbol SubstituteParameterSymbols(
        TypeWithAnnotations substitutedReturnType,
        ImmutableArray<TypeOrConstant> substitutedParameterTypes) {
        return new FunctionPointerMethodSymbol(
            callingConvention,
            refKind,
            substitutedReturnType,
            parameters,
            substitutedParameterTypes
        );
    }

    internal override bool Equals(Symbol other, TypeCompareKind compareKind) {
        if (!(other is FunctionPointerMethodSymbol method)) {
            return false;
        }

        return Equals(method, compareKind);
    }

    internal bool Equals(FunctionPointerMethodSymbol other, TypeCompareKind compareKind) {
        if (ReferenceEquals(this, other))
            return true;

        if (!EqualsNoParameters(other, compareKind))
            return false;

        return _parameters.SequenceEqual(other._parameters, compareKind,
             (param1, param2, compareKind) => param1.MethodEqualityChecks(param2, compareKind));
    }

    private bool EqualsNoParameters(FunctionPointerMethodSymbol other, TypeCompareKind compareKind) {
        if (callingConvention != other.callingConvention
            || !FunctionPointerTypeSymbol.RefKindEquals(compareKind, refKind, other.refKind)
            || !returnTypeWithAnnotations.Equals(other.returnTypeWithAnnotations, compareKind)) {
            return false;
        }

        // if ((compareKind & TypeCompareKind.IgnoreArraySizesAndLowerBounds) != 0) {
        //     if (CallingConvention.IsCallingConvention(CallingConvention.Unmanaged)
        //         && !GetCallingConventionModifiers().SetEqualsWithoutIntermediateHashSet(other.GetCallingConventionModifiers())) {
        //         return false;
        //     }
        // }

        return true;
    }

    public override int GetHashCode() {
        var currentHash = GetHashCodeNoParameters();

        foreach (var param in _parameters)
            currentHash = Hash.Combine(param.MethodHashCode(), currentHash);

        return currentHash;
    }

    internal int GetHashCodeNoParameters() {
        return Hash.Combine(returnType,
            Hash.Combine(((int)callingConvention).GetHashCode(),
                ((int)FunctionPointerTypeSymbol.GetRefKindForHashCode(refKind)).GetHashCode()));
    }

    internal override CallingConvention callingConvention { get; }

    internal bool isManaged => callingConvention != CallingConvention.Unmanaged;

    public override bool returnsVoid => returnTypeWithAnnotations.IsVoidType();

    public override RefKind refKind { get; }

    internal override TypeWithAnnotations returnTypeWithAnnotations { get; }

    internal override ImmutableArray<ParameterSymbol> parameters
        => _parameters.Cast<FunctionPointerParameterSymbol, ParameterSymbol>();

    public override MethodKind methodKind => MethodKind.FunctionPointerSignature;

    internal override Symbol containingSymbol => null;

    public override int arity => 0;

    public override ImmutableArray<TemplateParameterSymbol> templateParameters => [];

    public override ImmutableArray<BoundExpression> templateConstraints => [];

    internal override bool hidesBaseMethodsByName => false;

    internal override ImmutableArray<TextLocation> locations => [];

    internal override TextLocation location => null;

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

    internal override SyntaxReference syntaxReference => null;

    internal override Accessibility declaredAccessibility => Accessibility.NotApplicable;

    internal override bool isStatic => false;

    internal override bool isVirtual => false;

    internal override bool isOverride => false;

    internal override bool isAbstract => false;

    internal override bool isSealed => false;

    internal override bool isExtern => false;

    internal override bool isImplicitlyDeclared => true;

    internal override bool isDeclaredConst => false;

    internal override bool hasUnscopedRefAttribute => false;

    public override ImmutableArray<TypeOrConstant> templateArguments => [];

    internal override bool hasSpecialName => false;

    internal override DllImportData GetDllImportData() {
        throw ExceptionUtilities.Unreachable();
    }

    internal override int CalculateLocalSyntaxOffset(int localPosition, SyntaxTree localTree) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override bool IsMetadataVirtual(bool forceComplete = false) {
        return false;
    }
}

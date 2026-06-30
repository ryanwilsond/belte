using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class SynthesizedTemplateTypeMethod : WrappedMethodSymbol {
    private readonly SynthesizedTemplateType _containingType;

    private int _hashCode;

    internal SynthesizedTemplateTypeMethod(SynthesizedTemplateType newOwner, MethodSymbol method)
        : base(method) {
        _containingType = newOwner;
    }

    internal override Symbol containingSymbol => _containingType;

    public override ImmutableArray<TemplateParameterSymbol> templateParameters => underlyingMethod.templateParameters;

    public override ImmutableArray<BoundExpression> templateConstraints => underlyingMethod.templateConstraints;

    public override ImmutableArray<TypeOrConstant> templateArguments => underlyingMethod.templateArguments;

    internal override TypeWithAnnotations returnTypeWithAnnotations => underlyingMethod.returnTypeWithAnnotations;

    internal override ImmutableArray<ParameterSymbol> parameters => underlyingMethod.parameters;

    internal override int parameterCount => underlyingMethod.parameterCount;

    internal override bool isExplicitInterfaceImplementation => underlyingMethod.isExplicitInterfaceImplementation;

    internal override ImmutableArray<MethodSymbol> explicitInterfaceImplementations => [];

    internal override int CalculateLocalSyntaxOffset(int localPosition, SyntaxTree localTree) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override DllImportData GetDllImportData() {
        return underlyingMethod.GetDllImportData();
    }

    internal override UnmanagedCallersOnlyAttributeData GetUnmanagedCallersOnlyAttributeData(bool forceComplete) {
        return underlyingMethod.GetUnmanagedCallersOnlyAttributeData(forceComplete);
    }

    public override int GetHashCode() {
        if (_hashCode == 0)
            _hashCode = ComputeHashCode();

        return _hashCode;
    }

    private int ComputeHashCode() {
        var code = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        var containingHashCode = containingType.GetHashCode();
        code = Hash.Combine(containingHashCode, code);

        if (code == 0)
            code++;

        return code;
    }
}

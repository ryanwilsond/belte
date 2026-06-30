using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class SynthesizedTemplateTypeMethod : WrappedMethodSymbol {
    private readonly SynthesizedTemplateType _containingType;
    private readonly TypeWithAnnotations _returnType;
    private readonly ImmutableArray<ParameterSymbol> _parameters;

    private int _hashCode;

    internal SynthesizedTemplateTypeMethod(SynthesizedTemplateType newOwner, MethodSymbol method)
        : base(method) {
        _containingType = newOwner;
        _returnType = TemplateTypeReplacer<TemplateParameterSymbol, TemplateParameterSymbol, TemplateParameterSymbol>
            .Replace(method.returnTypeWithAnnotations, newOwner.replacementTemplateParameters);

        var builder = ArrayBuilder<ParameterSymbol>.GetInstance(method.parameterCount);

        foreach (var parameter in method.parameters) {
            var newType = TemplateTypeReplacer<TemplateParameterSymbol, TemplateParameterSymbol, TemplateParameterSymbol>
                .Replace(parameter.typeWithAnnotations, newOwner.replacementTemplateParameters);

            builder.Add(new TypeSubstitutedParameterSymbol(parameter, newType));
        }

        _parameters = builder.ToImmutableAndFree();
    }

    internal override Symbol containingSymbol => _containingType;

    public override ImmutableArray<TemplateParameterSymbol> templateParameters => underlyingMethod.templateParameters;

    public override ImmutableArray<BoundExpression> templateConstraints => underlyingMethod.templateConstraints;

    public override ImmutableArray<TypeOrConstant> templateArguments => underlyingMethod.templateArguments;

    internal override TypeWithAnnotations returnTypeWithAnnotations => _returnType;

    internal override ImmutableArray<ParameterSymbol> parameters => _parameters;

    internal override int parameterCount => _parameters.Length;

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

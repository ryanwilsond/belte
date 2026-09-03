using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class TypeSubstitutedMethodSymbol : WrappedMethodSymbol {
    private readonly TypeWithAnnotations _returnType;
    private readonly ImmutableArray<ParameterSymbol> _parameters;

    internal TypeSubstitutedMethodSymbol(
        MethodSymbol originalMethod,
        TypeWithAnnotations newReturnType,
        ImmutableArray<ParameterSymbol> newParameters)
        : base(originalMethod) {
        _returnType = newReturnType;
        _parameters = newParameters;
    }

    public override ImmutableArray<TemplateParameterSymbol> templateParameters => underlyingMethod.templateParameters;

    public override ImmutableArray<BoundExpression> templateConstraints => underlyingMethod.templateConstraints;

    public override ImmutableArray<TypeOrConstant> templateArguments => underlyingMethod.templateArguments;

    internal override Symbol containingSymbol => underlyingMethod.containingSymbol;

    internal override TypeWithAnnotations returnTypeWithAnnotations => _returnType;

    internal override ImmutableArray<ParameterSymbol> parameters => _parameters;

    internal override int parameterCount => _parameters.Length;

    internal override bool isExplicitInterfaceImplementation => underlyingMethod.isExplicitInterfaceImplementation;

    internal override ImmutableArray<MethodSymbol> explicitInterfaceImplementations => [];

    internal override int CalculateLocalSyntaxOffset(int localPosition, SyntaxTree localTree) {
        return underlyingMethod.CalculateLocalSyntaxOffset(localPosition, localTree);
    }

    internal override DllImportData GetDllImportData() {
        return underlyingMethod.GetDllImportData();
    }

    internal override UnmanagedCallersOnlyAttributeData GetUnmanagedCallersOnlyAttributeData(bool forceComplete) {
        return underlyingMethod.GetUnmanagedCallersOnlyAttributeData(forceComplete);
    }
}

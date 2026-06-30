using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class SynthesizedTemplateMethod : WrappedMethodSymbol, ISynthesizedTemplate<MethodSymbol> {
    private readonly ConstructedMethodSymbol _originalMethod;
    private readonly TypeWithAnnotations _returnType;
    private readonly ImmutableArray<ParameterSymbol> _parameters;
    private readonly Dictionary<TemplateParameterSymbol, TemplateParameterSymbol> _replacementTemplateParameters;

    private int _hashCode;

    internal SynthesizedTemplateMethod(
        Symbol containingSymbol,
        ConstructedMethodSymbol originalMethod)
        : base(originalMethod.constructedFrom) {
        _originalMethod = originalMethod;
        name = GeneratedNames.MakeTemplateTypeOrMethodName(originalMethod);

        var i = 0;
        templateParameters = originalMethod.templateParameters
            .Where(t => t.underlyingType.specialType == SpecialType.Type)
            .Select(t => new SynthesizedTemplateTypeParameter(this, t, i++))
            .ToImmutableArray<TemplateParameterSymbol>();

        i = 0;
        templateSubstitution = new TemplateMap(
            originalMethod.constructedFrom.containingType,
            originalMethod.templateParameters,
            originalMethod.templateArguments.ZipAsArray(
                originalMethod.constructedFrom.templateParameters,
                i,
                (typeOrConstant, templateParameter, i, arg) => {
                    if (templateParameter.underlyingType.specialType == SpecialType.Type) {
                        return new TypeOrConstant(templateParameters[templateParameter.ordinal - i]);
                    } else {
                        i++;
                        return typeOrConstant;
                    }
                }
            )
        );

        _replacementTemplateParameters = [];

        i = 0;
        foreach (var templateParameter in originalMethod.constructedFrom.templateParameters) {
            if (templateParameter.underlyingType.specialType == SpecialType.Type)
                _replacementTemplateParameters.Add(templateParameter, templateParameters[i++]);
        }

        this.containingSymbol = containingSymbol;

        _returnType = TemplateTypeReplacer<TemplateParameterSymbol, TemplateParameterSymbol, TemplateParameterSymbol>
            .Replace(originalMethod.returnTypeWithAnnotations, _replacementTemplateParameters);

        var builder = ArrayBuilder<ParameterSymbol>.GetInstance(originalMethod.parameterCount);

        foreach (var parameter in originalMethod.parameters) {
            var newType = TemplateTypeReplacer<TemplateParameterSymbol, TemplateParameterSymbol, TemplateParameterSymbol>
                .Replace(parameter.typeWithAnnotations, _replacementTemplateParameters);

            builder.Add(new TypeSubstitutedParameterSymbol(parameter, newType));
        }

        _parameters = builder.ToImmutableAndFree();
    }

    public override string name { get; }

    public override ImmutableArray<TemplateParameterSymbol> templateParameters { get; }

    public override ImmutableArray<TypeOrConstant> templateArguments => GetTemplateParametersAsTemplateArguments();

    public override ImmutableArray<BoundExpression> templateConstraints => underlyingMethod.templateConstraints;

    public override int arity => templateParameters.Length;

    public override TemplateMap templateSubstitution { get; }

    internal override TypeWithAnnotations returnTypeWithAnnotations => _returnType;

    internal override ImmutableArray<ParameterSymbol> parameters => _parameters;

    internal override int parameterCount => _parameters.Length;

    internal override MethodSymbol originalDefinition => this;

    internal override MethodSymbol constructedFrom => this;

    internal override Symbol containingSymbol { get; }

    internal override bool isExplicitInterfaceImplementation => underlyingMethod.isExplicitInterfaceImplementation;

    internal override ImmutableArray<MethodSymbol> explicitInterfaceImplementations => [];

    internal MethodSymbol unexpandedSymbol => _originalMethod;

    internal Dictionary<TemplateParameterSymbol, TemplateParameterSymbol> replacementTemplateParameters
        => _replacementTemplateParameters;

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
        var baseHashCode = base.GetHashCode();
        var newHashCode = baseHashCode;

        foreach (var templateArgument in _originalMethod.templateArguments) {
            if (templateArgument.isConstant)
                newHashCode = Hash.Combine(templateArgument.constant, newHashCode);
        }

        Debug.Assert(baseHashCode != newHashCode);
        return newHashCode;
    }

    MethodSymbol ISynthesizedTemplate<MethodSymbol>.unexpandedSymbol => _originalMethod;

    Dictionary<TemplateParameterSymbol, TemplateParameterSymbol> ISynthesizedTemplate<MethodSymbol>.replacementTemplateParameters
        => _replacementTemplateParameters;
}

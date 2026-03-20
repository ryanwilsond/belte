using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

namespace Buckle.Libraries;

internal sealed class SynthesizedFinishedMethodSymbol : WrappedMethodSymbol {
    internal SynthesizedFinishedMethodSymbol(
        MethodSymbol underlyingMethod,
        Symbol containingSymbol,
        ImmutableArray<ParameterSymbol>? parameters = null)
        : base(underlyingMethod) {
        this.containingSymbol = containingSymbol;
        this.parameters = parameters ?? underlyingMethod.parameters;
    }

    public override ImmutableArray<TemplateParameterSymbol> templateParameters => underlyingMethod.templateParameters;

    public override ImmutableArray<BoundExpression> templateConstraints => underlyingMethod.templateConstraints;

    public override ImmutableArray<TypeOrConstant> templateArguments => underlyingMethod.templateArguments;

    internal override TypeWithAnnotations returnTypeWithAnnotations => underlyingMethod.returnTypeWithAnnotations;

    internal override ImmutableArray<ParameterSymbol> parameters { get; }

    internal override int parameterCount => parameters.Length;

    internal override Symbol containingSymbol { get; }

    internal override int CalculateLocalSyntaxOffset(int localPosition, SyntaxTree localTree) {
        throw ExceptionUtilities.Unreachable();
    }
}

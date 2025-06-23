using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SynthesizedMethodSymbolBase : SourceMemberMethodSymbol {
    internal readonly MethodSymbol baseMethod;

    private readonly string _name;
    private ImmutableArray<TemplateParameterSymbol> _templateParameters;
    private ImmutableArray<ParameterSymbol> _parameters;

    private protected SynthesizedMethodSymbolBase(
        NamedTypeSymbol containingType,
        MethodSymbol baseMethod,
        SyntaxReference syntaxReference,
        TextLocation location,
        string name,
        DeclarationModifiers declarationModifiers)
        : base(
            containingType,
            syntaxReference,
            (declarationModifiers, MakeFlags(
                methodKind: MethodKind.Ordinary,
                refKind: baseMethod.refKind,
                declarationModifiers,
                returnsVoid: baseMethod.returnsVoid,
                returnsVoidIsSet: true,
                hasAnyBody: true,
                hasThisInitializer: false)
            )
        ) {
        this.baseMethod = baseMethod;
        _name = name;
        this.location = location;
    }

    private protected void AssignTemplateMapAndTemplateParameters(
        TemplateMap templateMap,
        ImmutableArray<TemplateParameterSymbol> templateParameters) {
        this.templateMap = templateMap;
        _templateParameters = templateParameters;
    }

    private protected override void MethodChecks(BelteDiagnosticQueue diagnostics) {
        // TODO Forwarded from Roslyn
        // TODO: move more functionality into here, making these symbols more lazy
    }

    public sealed override ImmutableArray<TemplateParameterSymbol> templateParameters => _templateParameters;

    // TODO This should be something
    public sealed override ImmutableArray<BoundExpression> templateConstraints => [];

    public sealed override string name => _name;

    internal sealed override ImmutableArray<ImmutableArray<TypeWithAnnotations>> GetTypeParameterConstraintTypes() => [];

    internal sealed override ImmutableArray<TypeParameterConstraintKinds> GetTypeParameterConstraintKinds() => [];

    internal override int parameterCount => parameters.Length;

    internal override TextLocation location { get; }

    internal sealed override ImmutableArray<ParameterSymbol> parameters {
        get {
            if (_parameters.IsDefault)
                ImmutableInterlocked.InterlockedInitialize(ref _parameters, MakeParameters());

            return _parameters;
        }
    }

    internal virtual bool inheritsBaseMethodAttributes => false;

    internal sealed override bool hasSpecialName => inheritsBaseMethodAttributes && baseMethod.hasSpecialName;

    internal sealed override TypeWithAnnotations returnTypeWithAnnotations
        => templateMap.SubstituteType(baseMethod.originalDefinition.returnTypeWithAnnotations).type;

    internal sealed override bool isImplicitlyDeclared => true;

    internal TemplateMap templateMap { get; private set; }

    private protected virtual ImmutableArray<ParameterSymbol> _baseMethodParameters => baseMethod.parameters;

    private protected virtual ImmutableArray<NamedTypeSymbol> _extraSynthesizedRefParameters => default;

    private ImmutableArray<ParameterSymbol> MakeParameters() {
        var ordinal = 0;
        var builder = ArrayBuilder<ParameterSymbol>.GetInstance();
        var parameters = _baseMethodParameters;
        var inheritAttributes = inheritsBaseMethodAttributes;

        foreach (var p in parameters) {
            builder.Add(SynthesizedParameterSymbol.Create(
                this,
                templateMap.SubstituteType(p.originalDefinition.typeWithAnnotations).type,
                ordinal++,
                p.refKind,
                p.name,
                p.effectiveScope,
                p.explicitDefaultConstantValue,
                inheritAttributes ? p as SourceComplexParameterSymbolBase : null
            ));
        }

        var extraSynthed = _extraSynthesizedRefParameters;

        if (!extraSynthed.IsDefaultOrEmpty) {
            foreach (var extra in extraSynthed) {
                var paramType = templateMap.SubstituteType(extra).type;

                builder.Add(SynthesizedParameterSymbol.Create(
                    this,
                    paramType,
                    ordinal++,
                    RefKind.Ref,
                    GeneratedNames.MakeSynthedParameterName(ordinal, paramType)
                ));
            }
        }

        return builder.ToImmutableAndFree();
    }
}

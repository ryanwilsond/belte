using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Lowering;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SynthesizedClosureMethod : SynthesizedMethodSymbolBase {
    private readonly ImmutableArray<NamedTypeSymbol> _structEnvironments;

    internal readonly int methodOrdinal;

    internal SynthesizedClosureMethod(
        NamedTypeSymbol containingType,
        ImmutableArray<SynthesizedClosureEnvironment> structEnvironments,
        ClosureKind closureKind,
        MethodSymbol topLevelMethod,
        int topLevelMethodOrdinal,
        MethodSymbol originalMethod,
        SyntaxReference blockSyntax,
        TextLocation location,
        int methodOrdinal,
        TypeCompilationState compilationState)
            : base(
                containingType,
                originalMethod,
                blockSyntax,
                location,
                GeneratedNames.MakeClosureName(
                    topLevelMethod.name,
                    originalMethod.name,
                    topLevelMethodOrdinal,
                    closureKind,
                    methodOrdinal
                ),
                MakeDeclarationModifiers(closureKind, originalMethod)) {
        this.topLevelMethod = topLevelMethod;
        this.methodOrdinal = methodOrdinal;
        this.closureKind = closureKind;

        var lambdaFrame = containingType as SynthesizedClosureEnvironment;

        TemplateMap templateMap;
        ImmutableArray<TemplateParameterSymbol> templateParameters;

        switch (closureKind) {
            case ClosureKind.Singleton:
            case ClosureKind.General:
                templateMap = lambdaFrame.templateMap.WithConcatAlphaRename(
                    originalMethod,
                    this,
                    out templateParameters,
                    out _,
                    lambdaFrame.originalContainingMethod
                );

                break;
            case ClosureKind.ThisOnly:
            case ClosureKind.Static:
                templateMap = TemplateMap.Empty.WithConcatAlphaRename(
                    originalMethod,
                    this,
                    out templateParameters,
                    out _,
                    stopAt: null
                );

                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(closureKind);
        }

        if (!structEnvironments.IsDefaultOrEmpty && templateParameters.Length != 0) {
            var constructedStructClosures = ArrayBuilder<NamedTypeSymbol>.GetInstance();

            foreach (var environment in structEnvironments) {
                NamedTypeSymbol constructed;

                if (environment.arity == 0) {
                    constructed = environment;
                } else {
                    var originals = environment.constructedFromTemplateParameters;
                    var newArgs = templateMap.SubstituteTemplateParameters(originals);
                    constructed = environment.Construct(newArgs.Select(s => new TypeOrConstant(s)).ToImmutableArray());
                }

                constructedStructClosures.Add(constructed);
            }

            _structEnvironments = constructedStructClosures.ToImmutableAndFree();
        } else {
            _structEnvironments = ImmutableArray<NamedTypeSymbol>.CastUp(structEnvironments);
        }

        AssignTemplateMapAndTemplateParameters(templateMap, templateParameters);
    }

    private static DeclarationModifiers MakeDeclarationModifiers(ClosureKind closureKind, MethodSymbol originalMethod) {
        var mods = closureKind == ClosureKind.ThisOnly ? DeclarationModifiers.Private : DeclarationModifiers.Public;

        if (closureKind == ClosureKind.Static)
            mods |= DeclarationModifiers.Static;

        if (originalMethod.isExtern)
            mods |= DeclarationModifiers.Extern;

        return mods;
    }

    internal MethodSymbol topLevelMethod { get; }

    internal ClosureKind closureKind { get; }

    private protected override ImmutableArray<ParameterSymbol> _baseMethodParameters => baseMethod.parameters;

    private protected override ImmutableArray<NamedTypeSymbol> _extraSynthesizedRefParameters
        => ImmutableArray<NamedTypeSymbol>.CastUp(_structEnvironments);

    internal int extraSynthesizedParameterCount => _structEnvironments.IsDefault ? 0 : _structEnvironments.Length;

    internal override bool inheritsBaseMethodAttributes => true;

    internal override ExecutableCodeBinder TryGetBodyBinder(
        BinderFactory binderFactoryOpt = null,
        bool ignoreAccessibility = false) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override int CalculateLocalSyntaxOffset(int localPosition, SyntaxTree localTree) {
        return topLevelMethod.CalculateLocalSyntaxOffset(localPosition, localTree);
    }
}

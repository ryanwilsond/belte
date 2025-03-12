using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SynthesizedClosureMethod : SynthesizedMethodSymbolBase {
    internal readonly int methodOrdinal;

    internal SynthesizedClosureMethod(
        NamedTypeSymbol containingType,
        ImmutableArray<NamedTypeSymbol> extraParameters,
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
                MakeName(topLevelMethod.name, originalMethod.name, topLevelMethodOrdinal, methodOrdinal),
                DeclarationModifiers.Private) {
        this.topLevelMethod = topLevelMethod;
        this.methodOrdinal = methodOrdinal;

        var templateMap = TemplateMap.Empty.WithConcatAlphaRename(
            originalMethod,
            this,
            out var templateParameters,
            out _
        );

        _extraSynthesizedRefParameters = extraParameters;

        // TODO Do we need to replace this for template parameters?
        // if (!structEnvironments.IsDefaultOrEmpty && typeParameters.Length != 0) {
        //     var constructedStructClosures = ArrayBuilder<NamedTypeSymbol>.GetInstance();
        //     foreach (var env in structEnvironments) {
        //         NamedTypeSymbol constructed;
        //         if (env.Arity == 0) {
        //             constructed = env;
        //         } else {
        //             var originals = env.ConstructedFromTypeParameters;
        //             var newArgs = typeMap.SubstituteTypeParameters(originals);
        //             constructed = env.Construct(newArgs);
        //         }
        //         constructedStructClosures.Add(constructed);
        //     }
        //     _structEnvironments = constructedStructClosures.ToImmutableAndFree();
        // } else {
        //     _structEnvironments = ImmutableArray<NamedTypeSymbol>.CastUp(structEnvironments);
        // }

        AssignTemplateMapAndTemplateParameters(templateMap, templateParameters);
    }

    private static string MakeName(
        string topLevelMethodName,
        string localFunctionName,
        int topLevelMethodOrdinal,
        int methodOrdinal) {
        var result = PooledStringBuilder.GetInstance();
        var builder = result.Builder;
        builder.Append('<');

        if (topLevelMethodName is not null)
            builder.Append(topLevelMethodName);

        // 'g' represents 'general'
        // We only support general local functions currently
        builder.Append(">g");

        if (localFunctionName is not null || topLevelMethodOrdinal >= 0 || methodOrdinal >= 0) {
            // '__' represents the suffix separator
            builder.Append("__");
            builder.Append(localFunctionName);
            // '|' repesents the local function suffix terminator
            builder.Append('|');

            if (topLevelMethodOrdinal >= 0)
                builder.Append(topLevelMethodOrdinal);

            if (methodOrdinal >= 0) {
                if (methodOrdinal >= 0) {
                    // '_' represents the ID separator
                    builder.Append('_');
                }

                builder.Append(methodOrdinal);
            }
        }

        return result.ToStringAndFree();
    }

    internal MethodSymbol topLevelMethod { get; }

    private protected override ImmutableArray<ParameterSymbol> _baseMethodParameters => _baseMethod.parameters;

    private protected override ImmutableArray<NamedTypeSymbol> _extraSynthesizedRefParameters { get; }

    internal int extraSynthesizedParameterCount => _extraSynthesizedRefParameters.Length;

    internal override bool inheritsBaseMethodAttributes => true;

    internal override ExecutableCodeBinder TryGetBodyBinder(
        BinderFactory binderFactoryOpt = null,
        bool ignoreAccessibility = false) {
        throw ExceptionUtilities.Unreachable();
    }
}

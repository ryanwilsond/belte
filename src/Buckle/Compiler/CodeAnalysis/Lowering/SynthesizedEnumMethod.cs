using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class SynthesizedEnumMethod : SynthesizedMethodSymbolBase {
    internal SynthesizedEnumMethod(NamedTypeSymbol containingType, MethodSymbol originalMethod)
        : base(
            containingType,
            originalMethod,
            originalMethod.syntaxReference,
            originalMethod.location,
            originalMethod.name,
            MakeDeclarationModifiers(originalMethod)) {
        if (originalMethod.isStatic) {
            _baseMethodParameters = originalMethod.parameters;
        } else {
            var builder = ArrayBuilder<ParameterSymbol>.GetInstance();
            var enumType = originalMethod.containingType;

            builder.Add(SynthesizedParameterSymbol.Create(
                this,
                new TypeWithAnnotations(enumType),
                0,
                RefKind.None,
                "this" // Name is just for debugging visibility
            ));

            builder.AddRange(originalMethod.parameters);
            _baseMethodParameters = builder.ToImmutableAndFree();
        }

        AssignTemplateMapAndTemplateParameters(
            originalMethod.templateSubstitution ?? TemplateMap.Empty,
            originalMethod.templateParameters
        );
    }

    private protected override ImmutableArray<ParameterSymbol> _baseMethodParameters { get; }

    internal override ExecutableCodeBinder TryGetBodyBinder(
        BinderFactory binderFactoryOpt = null,
        bool ignoreAccessibility = false) {
        throw ExceptionUtilities.Unreachable();
    }

    private static DeclarationModifiers MakeDeclarationModifiers(MethodSymbol originalMethod) {
        var mods = DeclarationModifiers.Public | DeclarationModifiers.Static;

        if (originalMethod.isExtern)
            mods |= DeclarationModifiers.Extern;

        return mods;
    }
}

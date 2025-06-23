using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Lowering;

internal abstract partial class MethodToClassRewriter {
    private sealed partial class BaseMethodWrapperSymbol : SynthesizedMethodSymbolBase {
        internal BaseMethodWrapperSymbol(NamedTypeSymbol containingType, MethodSymbol methodBeingWrapped, SyntaxNode syntax, string name)
            : base(containingType,
                methodBeingWrapped,
                new SyntaxReference(syntax),
                syntax.location,
                name,
                DeclarationModifiers.Private) {
            var templateMap = methodBeingWrapped.containingType is SubstitutedNamedTypeSymbol substitutedType
                ? substitutedType.templateSubstitution
                : TemplateMap.Empty;

            ImmutableArray<TemplateParameterSymbol> typeParameters;

            if (!methodBeingWrapped.isTemplateMethod)
                typeParameters = [];
            else
                templateMap = templateMap.WithAlphaRename(methodBeingWrapped, this, out typeParameters);

            AssignTemplateMapAndTemplateParameters(templateMap, typeParameters);
        }

        internal override bool synthesizesLoweredBoundBody => true;

        // internal override void AddSynthesizedAttributes(PEModuleBuilder moduleBuilder, ref ArrayBuilder<SynthesizedAttributeData> attributes) {
        //     base.AddSynthesizedAttributes(moduleBuilder, ref attributes);

        //     AddSynthesizedAttribute(ref attributes, this.DeclaringCompilation.TrySynthesizeAttribute(WellKnownMember.System_Diagnostics_DebuggerHiddenAttribute__ctor));
        // }

        internal override ExecutableCodeBinder TryGetBodyBinder(
            BinderFactory binderFactoryOpt = null,
            bool ignoreAccessibility = false)
                => throw ExceptionUtilities.Unreachable();

        internal override void GenerateMethodBody(
            TypeCompilationState compilationState,
            BelteDiagnosticQueue diagnostics) {
            // SyntheticBoundNodeFactory F = new SyntheticBoundNodeFactory(this, this.GetNonNullSyntaxNode(), compilationState, diagnostics);
            // F.CurrentFunction = this.OriginalDefinition;

            // try {
            //     MethodSymbol methodBeingWrapped = this.BaseMethod;

            //     if (this.Arity > 0) {
            //         Debug.Assert(this.Arity == methodBeingWrapped.Arity);
            //         methodBeingWrapped = methodBeingWrapped.ConstructedFrom.Construct(StaticCast<TypeSymbol>.From(this.TypeParameters));
            //     }

            //     BoundBlock body = MethodBodySynthesizer.ConstructSingleInvocationMethodBody(F, methodBeingWrapped, useBaseReference: true);
            //     if (body.Kind != BoundKind.Block) body = F.Block(body);
            //     F.CompilationState.AddMethodWrapper(methodBeingWrapped, this, body);
            // } catch (SyntheticBoundNodeFactory.MissingPredefinedMember ex) {
            //     diagnostics.Add(ex.Diagnostic);
            // }
        }
    }
}

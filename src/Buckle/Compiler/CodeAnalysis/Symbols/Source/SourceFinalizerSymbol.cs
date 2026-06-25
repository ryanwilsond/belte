using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceFinalizerSymbol : SourceMemberMethodSymbol {
    private TypeWithAnnotations _lazyReturnType;

    internal SourceFinalizerSymbol(
        SourceMemberContainerTypeSymbol containingType,
        FinalizerDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics)
        : base(
            containingType,
            new SyntaxReference(syntax),
            syntax.finalizerKeyword.location,
            MakeModifiersAndFlags(containingType, syntax, diagnostics, out _)) {
        if (containingType.isStatic)
            diagnostics.Push(Error.FinalizerInStaticClass(location));
        else if (!containingType.isReferenceType)
            diagnostics.Push(Error.OnlyClassesCanContainFinalizers(location));
    }

    public override string name => WellKnownMemberNames.FinalizerName;

    internal override bool isMetadataFinal => false;

    internal override int parameterCount => 0;

    internal override ImmutableArray<ParameterSymbol> parameters => [];

    public override ImmutableArray<TemplateParameterSymbol> templateParameters => [];

    public override ImmutableArray<BoundExpression> templateConstraints => [];

    internal override TypeWithAnnotations returnTypeWithAnnotations {
        get {
            LazyMethodChecks();
            return _lazyReturnType;
        }
    }

    internal override ImmutableArray<ImmutableArray<TypeWithAnnotations>> GetTypeParameterConstraintTypes() {
        return [];
    }

    internal override ImmutableArray<TypeParameterConstraintKinds> GetTypeParameterConstraintKinds() {
        return [];
    }

    private protected override void MethodChecks(BelteDiagnosticQueue diagnostics) {
        _lazyReturnType = new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Void));
    }

    private static (DeclarationModifiers, Flags) MakeModifiersAndFlags(
        NamedTypeSymbol containingType,
        FinalizerDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics,
        out bool modifierErrors) {
        var declarationModifiers = MakeModifiers(
            containingType,
            syntax,
            syntax.modifiers,
            diagnostics,
            out modifierErrors
        );

        var flags = MakeFlags(
            MethodKind.Finalizer,
            RefKind.None,
            declarationModifiers,
            returnsVoid: true,
            returnsVoidIsSet: true,
            hasAnyBody: true,
            hasThisInitializer: false
        );

        return (declarationModifiers, flags);
    }

    internal FinalizerDeclarationSyntax GetSyntax() {
        return (FinalizerDeclarationSyntax)syntaxReference.node;
    }

    internal override ExecutableCodeBinder TryGetBodyBinder(
        BinderFactory binderFactoryOpt = null,
        bool ignoreAccessibility = false) {
        return TryGetBodyBinderFromSyntax(binderFactoryOpt, ignoreAccessibility);
    }

    internal override OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations() {
        return OneOrMany.Create(GetSyntax().attributeLists);
    }

    internal override OneOrMany<SyntaxList<AttributeListSyntax>> GetReturnTypeAttributeDeclarations() {
        return OneOrMany.Create(default(SyntaxList<AttributeListSyntax>));
    }

    internal override bool IsMetadataVirtual(bool forceComplete = false) {
        return true;
    }

    private static DeclarationModifiers MakeModifiers(
        NamedTypeSymbol containingType,
        FinalizerDeclarationSyntax syntax,
        SyntaxTokenList modifiers,
        BelteDiagnosticQueue diagnostics,
        out bool modifierErrors) {
        var mods = ModifierHelpers.CreateAndCheckNonTypeMemberModifiers(
            modifiers,
            containingType.isInterface,
            DeclarationModifiers.None,
            0,
            syntax.finalizerKeyword.location,
            diagnostics,
            out modifierErrors
        );

        mods = (mods & ~DeclarationModifiers.AccessibilityMask) | DeclarationModifiers.Protected;

        return mods;
    }
}

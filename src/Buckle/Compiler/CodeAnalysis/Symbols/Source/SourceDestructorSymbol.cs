using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceDestructorSymbol : SourceMemberMethodSymbol {
    private TypeWithAnnotations _lazyReturnType;

    internal SourceDestructorSymbol(
        SourceMemberContainerTypeSymbol containingType,
        DestructorDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics)
        : base(
            containingType,
            new SyntaxReference(syntax),
            MakeModifiersAndFlags(syntax, diagnostics, out _)) {
        location = syntax.destructorKeyword.location;

        if (containingType.isStatic)
            diagnostics.Push(Error.DestructorInStaticClass(location));
    }

    public override string name => WellKnownMemberNames.DestructorName;

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

    internal override TextLocation location { get; }

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
        DestructorDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics,
        out bool modifierErrors) {
        var declarationModifiers = MakeModifiers(syntax, syntax.modifiers, diagnostics, out modifierErrors);
        var flags = MakeFlags(
            MethodKind.Destructor,
            RefKind.None,
            declarationModifiers,
            returnsVoid: true,
            returnsVoidIsSet: true,
            hasAnyBody: true,
            hasThisInitializer: false
        );

        return (declarationModifiers, flags);
    }

    internal DestructorDeclarationSyntax GetSyntax() {
        return (DestructorDeclarationSyntax)syntaxReference.node;
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
        DestructorDeclarationSyntax syntax,
        SyntaxTokenList modifiers,
        BelteDiagnosticQueue diagnostics,
        out bool modifierErrors) {
        var mods = ModifierHelpers.CreateAndCheckNonTypeMemberModifiers(
            modifiers,
            DeclarationModifiers.None,
            0,
            syntax.destructorKeyword.location,
            diagnostics,
            out modifierErrors
        );

        mods = (mods & ~DeclarationModifiers.AccessibilityMask) | DeclarationModifiers.Protected;

        return mods;
    }
}

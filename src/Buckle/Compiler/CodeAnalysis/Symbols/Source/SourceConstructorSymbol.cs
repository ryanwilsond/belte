using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceConstructorSymbol : SourceConstructorSymbolBase {
    private SourceConstructorSymbol(
        SourceMemberContainerTypeSymbol containingType,
        ConstructorDeclarationSyntax syntax,
        MethodKind methodKind,
        BelteDiagnosticQueue diagnostics)
        : base(
            containingType,
            syntax,
            MakeModifiersAndFlags(
                syntax,
                methodKind,
                syntax.constructorInitializer?.thisOrBaseKeyword?.kind == SyntaxKind.ThisKeyword,
                diagnostics,
                out var hasErrors
            )
        ) {
        location = syntax.constructorKeyword.location;

        ModifierHelpers.CheckAccessibility(_modifiers, diagnostics, location);

        if (!hasErrors)
            CheckModifiers(location, diagnostics);
    }

    internal override TextLocation location { get; }

    private protected override bool _allowRef => true;

    internal static SourceConstructorSymbol CreateConstructorSymbol(
        SourceMemberContainerTypeSymbol containingType,
        ConstructorDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics) {
        var methodKind = (syntax.modifiers?.Any(SyntaxKind.StaticKeyword) == true)
            ? MethodKind.StaticConstructor
            : MethodKind.Constructor;

        return new SourceConstructorSymbol(containingType, syntax, methodKind, diagnostics);
    }

    internal ConstructorDeclarationSyntax GetSyntax() {
        return (ConstructorDeclarationSyntax)syntaxReference.node;
    }

    internal override OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations() {
        return OneOrMany.Create(((ConstructorDeclarationSyntax)syntaxNode).attributeLists);
    }

    internal override ExecutableCodeBinder TryGetBodyBinder(
        BinderFactory binderFactory = null,
        bool ignoreAccessibility = false) {
        return TryGetBodyBinderFromSyntax(binderFactory, ignoreAccessibility);
    }

    private protected override ParameterListSyntax GetParameterList() {
        return GetSyntax().parameterList;
    }

    private static (DeclarationModifiers, Flags) MakeModifiersAndFlags(
        ConstructorDeclarationSyntax syntax,
        MethodKind methodKind,
        bool hasThisInitializer,
        BelteDiagnosticQueue diagnostics,
        out bool modifierErrors) {
        var hasAnyBody = syntax.body is not null;

        var declarationModifiers = MakeModifiers(syntax, methodKind, diagnostics, out modifierErrors);

        var flags = new Flags(
            methodKind,
            RefKind.None,
            declarationModifiers,
            true,
            true,
            hasAnyBody,
            hasThisInitializer
        );

        return (declarationModifiers, flags);
    }

    private static DeclarationModifiers MakeModifiers(
        ConstructorDeclarationSyntax syntax,
        MethodKind methodKind,
        BelteDiagnosticQueue diagnostics,
        out bool modifierErrors) {
        var defaultAccess = (methodKind == MethodKind.StaticConstructor)
            ? DeclarationModifiers.None
            : DeclarationModifiers.Private;

        var allowedModifiers = DeclarationModifiers.AccessibilityMask | DeclarationModifiers.Static;

        var mods = ModifierHelpers.CreateAndCheckNonTypeMemberModifiers(
            syntax.modifiers,
            defaultAccess,
            allowedModifiers,
            syntax.constructorKeyword.location,
            diagnostics,
            out modifierErrors
        );

        if (methodKind == MethodKind.StaticConstructor) {
            if ((mods & DeclarationModifiers.AccessibilityMask) != 0) {
                mods &= ~DeclarationModifiers.AccessibilityMask;
                diagnostics.Push(Error.StaticConstructorWithAccessModifier(syntax.constructorKeyword.location));
                modifierErrors = true;
            }

            mods |= DeclarationModifiers.Private;
        }

        return mods;
    }

    private void CheckModifiers(TextLocation location, BelteDiagnosticQueue diagnostics) {
        if (containingType.isSealed && declaredAccessibility == Accessibility.Protected && !isOverride)
            diagnostics.Push(Warning.ProtectedInSealed(location, this));
    }

    private protected override SyntaxNode GetInitializer() {
        return GetSyntax().constructorInitializer;
    }

    private protected override bool IsWithinBody(int position, out int offset) {
        var ctorSyntax = GetSyntax();

        if (ctorSyntax.body.span.Contains(position)) {
            offset = position - ctorSyntax.body.span.start;
            return true;
        }

        offset = -1;
        return false;
    }
}

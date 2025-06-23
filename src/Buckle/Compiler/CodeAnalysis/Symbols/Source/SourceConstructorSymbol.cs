using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceConstructorSymbol : SourceConstructorSymbolBase {
    private SourceConstructorSymbol(
        SourceMemberContainerTypeSymbol containingType,
        ConstructorDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics)
        : base(
            containingType,
            syntax,
            MakeModifiersAndFlags(
                syntax,
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
        // Eventually this will distinguish static and instance constructors
        return new SourceConstructorSymbol(containingType, syntax, diagnostics);
    }

    internal ConstructorDeclarationSyntax GetSyntax() {
        return (ConstructorDeclarationSyntax)syntaxReference.node;
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
        bool hasThisInitializer,
        BelteDiagnosticQueue diagnostics,
        out bool modifierErrors) {
        var hasAnyBody = syntax.body is not null;

        var declarationModifiers = MakeModifiers(syntax, diagnostics, out modifierErrors);

        var flags = new Flags(
            MethodKind.Constructor,
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
        BelteDiagnosticQueue diagnostics,
        out bool modifierErrors) {
        var defaultAccess = DeclarationModifiers.Private;
        var allowedModifiers = DeclarationModifiers.AccessibilityMask;

        var mods = ModifierHelpers.CreateAndCheckNonTypeMemberModifiers(
            syntax.modifiers,
            defaultAccess,
            allowedModifiers,
            syntax.constructorKeyword.location,
            diagnostics,
            out modifierErrors
        );

        return mods;
    }

    private void CheckModifiers(TextLocation location, BelteDiagnosticQueue diagnostics) {
        if (containingType.isSealed && declaredAccessibility == Accessibility.Protected && !isOverride)
            diagnostics.Push(Warning.ProtectedMemberInSealedType(location, containingType, this));
        else if (containingType.isStatic)
            diagnostics.Push(Error.ConstructorInStaticClass(location));
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

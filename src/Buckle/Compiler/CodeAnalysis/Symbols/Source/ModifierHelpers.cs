using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal static class ModifierHelpers {
    internal static DeclarationModifiers CreateAndCheckNonTypeMemberModifiers(
        SyntaxTokenList modifiers,
        DeclarationModifiers defaultAccess,
        DeclarationModifiers allowedModifiers,
        TextLocation errorLocation,
        BelteDiagnosticQueue diagnostics,
        out bool hasErrors) {
        var result = CreateModifiers(modifiers, diagnostics, out var creationErrors);
        result = CheckModifiers(false, result, allowedModifiers, errorLocation, diagnostics, out var checkErrors);
        hasErrors = creationErrors | checkErrors;

        if ((result & DeclarationModifiers.AccessibilityMask) == 0)
            result |= defaultAccess;

        return result;
    }

    internal static DeclarationModifiers CreateModifiers(
        SyntaxTokenList modifierTokens,
        BelteDiagnosticQueue diagnostics,
        out bool hasErrors) {
        hasErrors = false;
        var modifiers = DeclarationModifiers.None;

        if (modifierTokens is not null) {
            for (var i = 0; i < modifierTokens.Count; i++) {
                var modifierToken = modifierTokens[i];
                var nextIsRef = i < modifierTokens.Count - 1 && modifierTokens[i + 1].kind == SyntaxKind.RefKeyword;
                var currentModifier = ToDeclarationModifier(modifierToken.kind, nextIsRef);

                hasErrors |= SeenModifier(
                    modifierToken,
                    currentModifier,
                    modifiers,
                    diagnostics
                );

                modifiers |= currentModifier;
            }
        }

        return modifiers;
    }

    internal static DeclarationModifiers CheckModifiers(
        bool isForTypeDeclaration,
        DeclarationModifiers modifiers,
        DeclarationModifiers allowedModifiers,
        TextLocation errorLocation,
        BelteDiagnosticQueue diagnostics,
        out bool hasErrors) {
        hasErrors = false;

        var reportStaticNotVirtualForModifiers = DeclarationModifiers.None;

        if (!isForTypeDeclaration && ((modifiers & allowedModifiers & DeclarationModifiers.Static) != 0)) {
            reportStaticNotVirtualForModifiers = allowedModifiers &
                (DeclarationModifiers.Abstract | DeclarationModifiers.Override | DeclarationModifiers.Virtual);
            allowedModifiers &= ~reportStaticNotVirtualForModifiers;
        }

        var errorModifiers = modifiers & ~allowedModifiers;
        var result = modifiers & allowedModifiers;

        while (errorModifiers != DeclarationModifiers.None) {
            var oneError = errorModifiers & ~(errorModifiers - 1);
            errorModifiers &= ~oneError;

            switch (oneError) {
                case DeclarationModifiers.Abstract:
                case DeclarationModifiers.Override:
                case DeclarationModifiers.Virtual:
                    if ((reportStaticNotVirtualForModifiers & oneError) == 0)
                        goto default;

                    // TODO Figure out what this error should actually be
                    diagnostics.Push(Error.InvalidModifier(errorLocation, ConvertSingleModifierToSyntaxText(oneError)));
                    break;
                default:
                    diagnostics.Push(Error.InvalidModifier(errorLocation, ConvertSingleModifierToSyntaxText(oneError)));
                    break;
            }

            hasErrors = true;
        }

        return result;
    }

    internal static bool CheckAccessibility(
        DeclarationModifiers modifiers,
        BelteDiagnosticQueue diagnostics,
        TextLocation errorLocation) {
        if (!IsValidAccessibility(modifiers)) {
            diagnostics.Push(Error.MultipleAccessibilities(errorLocation));
            return true;
        }

        return false;
    }

    internal static string ConvertSingleModifierToSyntaxText(DeclarationModifiers modifier) {
        return modifier switch {
            DeclarationModifiers.Abstract => SyntaxFacts.GetText(SyntaxKind.AbstractKeyword),
            DeclarationModifiers.Sealed => SyntaxFacts.GetText(SyntaxKind.SealedKeyword),
            DeclarationModifiers.Static => SyntaxFacts.GetText(SyntaxKind.StaticKeyword),
            DeclarationModifiers.New => SyntaxFacts.GetText(SyntaxKind.NewKeyword),
            DeclarationModifiers.Public => SyntaxFacts.GetText(SyntaxKind.PublicKeyword),
            DeclarationModifiers.Protected => SyntaxFacts.GetText(SyntaxKind.ProtectedKeyword),
            DeclarationModifiers.Private => SyntaxFacts.GetText(SyntaxKind.PrivateKeyword),
            DeclarationModifiers.ConstExpr => SyntaxFacts.GetText(SyntaxKind.ConstexprKeyword),
            DeclarationModifiers.LowLevel => SyntaxFacts.GetText(SyntaxKind.LowlevelKeyword),
            DeclarationModifiers.Const or DeclarationModifiers.ConstRef => SyntaxFacts.GetText(SyntaxKind.ConstKeyword),
            DeclarationModifiers.Virtual => SyntaxFacts.GetText(SyntaxKind.VirtualKeyword),
            DeclarationModifiers.Override => SyntaxFacts.GetText(SyntaxKind.OverrideKeyword),
            DeclarationModifiers.Ref => SyntaxFacts.GetText(SyntaxKind.RefKeyword),
            _ => throw ExceptionUtilities.UnexpectedValue(modifier),
        };
    }

    internal static bool IsValidAccessibility(DeclarationModifiers modifiers) {
        return (modifiers & DeclarationModifiers.AccessibilityMask) switch {
            DeclarationModifiers.None or DeclarationModifiers.Private or
            DeclarationModifiers.Protected or DeclarationModifiers.Public => true,
            _ => false, // This happens when you have multiple accessibilities.
        };
    }

    internal static Accessibility EffectiveAccessibility(DeclarationModifiers modifiers) {
        return (modifiers & DeclarationModifiers.AccessibilityMask) switch {
            DeclarationModifiers.None => Accessibility.NotApplicable,
            DeclarationModifiers.Private => Accessibility.Private,
            DeclarationModifiers.Protected => Accessibility.Protected,
            DeclarationModifiers.Public => Accessibility.Public,
            _ => Accessibility.Public, // This happens when you have multiple accessibilities.
        };
    }

    private static DeclarationModifiers ToDeclarationModifier(SyntaxKind kind, bool nextIsRef) {
        return kind switch {
            SyntaxKind.SealedKeyword => DeclarationModifiers.Sealed,
            SyntaxKind.AbstractKeyword => DeclarationModifiers.Abstract,
            SyntaxKind.StaticKeyword => DeclarationModifiers.Static,
            SyntaxKind.LowlevelKeyword => DeclarationModifiers.LowLevel,
            SyntaxKind.PublicKeyword => DeclarationModifiers.Public,
            SyntaxKind.PrivateKeyword => DeclarationModifiers.Private,
            SyntaxKind.ProtectedKeyword => DeclarationModifiers.Protected,
            SyntaxKind.ConstKeyword when nextIsRef => DeclarationModifiers.ConstRef,
            SyntaxKind.ConstKeyword => DeclarationModifiers.Const,
            SyntaxKind.ConstexprKeyword => DeclarationModifiers.ConstExpr,
            SyntaxKind.VirtualKeyword => DeclarationModifiers.Virtual,
            SyntaxKind.OverrideKeyword => DeclarationModifiers.Override,
            SyntaxKind.NewKeyword => DeclarationModifiers.New,
            SyntaxKind.RefKeyword => DeclarationModifiers.Ref,
            _ => throw ExceptionUtilities.UnexpectedValue(kind),
        };
    }

    private static bool SeenModifier(
        SyntaxToken modifierToken,
        DeclarationModifiers modifier,
        DeclarationModifiers seenModifiers,
        BelteDiagnosticQueue diagnostics) {
        if ((seenModifiers & modifier) != 0) {
            diagnostics.Push(Error.ModifierAlreadyApplied(modifierToken.location, modifierToken));
            return true;
        }

        return false;
    }
}

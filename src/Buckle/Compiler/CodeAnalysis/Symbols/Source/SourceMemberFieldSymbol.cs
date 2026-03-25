using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceMemberFieldSymbol : SourceFieldSymbolWithSyntaxReference {
    internal SourceMemberFieldSymbol(
        NamedTypeSymbol containingType,
        DeclarationModifiers modifiers,
        string name,
        SyntaxReference syntaxReference)
        : base(containingType, name, syntaxReference) {
        _modifiers = modifiers;
    }

    internal abstract bool hasInitializer { get; }

    private protected sealed override DeclarationModifiers _modifiers { get; }

    private protected abstract TypeSyntax _typeSyntax { get; }

    private protected abstract SyntaxTokenList _modifiersTokenList { get; }

    internal override int fixedSize {
        get {
            _state.NotePartComplete(CompletionParts.FixedSize);
            return 0;
        }
    }

    internal static DeclarationModifiers MakeModifiers(
        NamedTypeSymbol containingSymbol,
        SyntaxToken firstIdentifier,
        SyntaxTokenList modifiers,
        BelteDiagnosticQueue diagnostics,
        out bool hasErrors) {
        var allowedModifiers =
            DeclarationModifiers.AccessibilityMask |
            DeclarationModifiers.Const |
            DeclarationModifiers.ConstExpr |
            DeclarationModifiers.New |
            DeclarationModifiers.LowLevel |
            DeclarationModifiers.Static;

        var result = ModifierHelpers.CreateAndCheckNonTypeMemberModifiers(
            modifiers,
            containingSymbol.typeKind == TypeKind.Class ? DeclarationModifiers.Private : DeclarationModifiers.Public,
            allowedModifiers,
            firstIdentifier.location,
            diagnostics,
            out hasErrors
        );

        // TODO Any other error checking needed here?
        if ((result & DeclarationModifiers.ConstExpr) != 0)
            result |= DeclarationModifiers.Static;

        return result;
    }

    internal sealed override void ForceComplete(TextLocation location) {
        while (true) {
            var incompletePart = _state.nextIncompletePart;

            switch (incompletePart) {
                case CompletionParts.Attributes:
                    GetAttributes();
                    break;
                case CompletionParts.Type:
                    GetFieldType(ConsList<FieldSymbol>.Empty);
                    break;
                case CompletionParts.FixedSize:
                    _ = fixedSize;
                    break;
                case CompletionParts.ConstantValue:
                    GetConstantValue(ConstantFieldsInProgress.Empty);
                    break;
                case CompletionParts.None:
                    return;
                default:
                    _state.NotePartComplete(CompletionParts.All & ~CompletionParts.FieldSymbolAll);
                    break;
            }

            _state.SpinWaitComplete(incompletePart);
        }
    }

    private protected void TypeChecks(TypeSymbol type, BelteDiagnosticQueue diagnostics) {
        if (type.isStatic)
            diagnostics.Push(Error.StaticDataContainer(errorLocation));
        else if (type.IsVoidType())
            diagnostics.Push(Error.VoidVariable(errorLocation));

        if (!IsNoMoreVisibleThan(type))
            diagnostics.Push(Error.InconsistentAccessibilityField(errorLocation, type, this));
    }
}

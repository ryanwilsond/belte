using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceMemberFieldSymbol : SourceFieldSymbolWithSyntaxReference {
    internal SourceMemberFieldSymbol(
        SourceMemberContainerTypeSymbol containingType,
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

    internal static DeclarationModifiers MakeModifiers(
        SyntaxToken firstIdentifier,
        SyntaxTokenList modifiers,
        BelteDiagnosticQueue diagnostics,
        out bool hasErrors) {
        var allowedModifiers =
            DeclarationModifiers.AccessibilityMask |
            DeclarationModifiers.Const |
            DeclarationModifiers.ConstExpr |
            DeclarationModifiers.New |
            DeclarationModifiers.Static |
            DeclarationModifiers.LowLevel;

        var result = ModifierHelpers.CreateAndCheckNonTypeMemberModifiers(
            modifiers,
            DeclarationModifiers.Private,
            allowedModifiers,
            firstIdentifier.location,
            diagnostics,
            out hasErrors
        );

        // TODO Any other error checking needed here?

        return result;
    }

    internal sealed override void ForceComplete(TextLocation location) {
        while (true) {
            var incompletePart = _state.nextIncompletePart;

            switch (incompletePart) {
                case CompletionParts.Type:
                    GetFieldType(ConsList<FieldSymbol>.Empty);
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
            diagnostics.Push(Error.StaticVariable(errorLocation));
        else if (type.IsVoidType())
            diagnostics.Push(Error.VoidVariable(errorLocation));

        if (IsNoMoreVisibleThan(type))
            diagnostics.Push(Error.InconsistentAccessibilityField(errorLocation, type, this));
    }
}

using Buckle.CodeAnalysis.Binding;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceFieldSymbol : FieldSymbolWithModifiers {
    private protected SourceFieldSymbol(NamedTypeSymbol containingType) {
        this.containingType = containingType;
    }

    public abstract override string name { get; }

    internal sealed override Symbol containingSymbol => containingType;

    internal override NamedTypeSymbol containingType { get; }

    internal sealed override bool requiresCompletion => true;

    internal bool isNew => (_modifiers & DeclarationModifiers.New) != 0;

    private protected override IAttributeTargetSymbol _attributeOwner => this;

    private protected void CheckAccessibility(BelteDiagnosticQueue diagnostics) {
        ModifierHelpers.CheckAccessibility(_modifiers, diagnostics, errorLocation);
    }

    private protected void ReportModifiersDiagnostics(BelteDiagnosticQueue diagnostics) {
        if (containingType.isSealed && declaredAccessibility == Accessibility.Protected)
            diagnostics.Push(AccessCheck.GetProtectedMemberInSealedTypeError(containingType, errorLocation));
        else if (containingType.isStatic && !isStatic)
            diagnostics.Push(Error.InstanceMemberInStatic(errorLocation, this));
        else if (refKind != RefKind.None && isConstExpr)
            diagnostics.Push(Error.CannotBeRefAndConstexpr(location));
        else if (isConst && isConstExpr)
            diagnostics.Push(Error.ConflictingModifiers(location, "const", "constexpr"));
    }
}

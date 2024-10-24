using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceFieldSymbol : FieldSymbolWithModifiers {
    private readonly SourceMemberContainerTypeSymbol _containingType;

    private protected SourceFieldSymbol(SourceMemberContainerTypeSymbol containingType) {
        _containingType = containingType;
    }

    public abstract override string name { get; }

    internal sealed override Symbol containingSymbol => _containingType;

    internal override NamedTypeSymbol containingType => _containingType;

    internal sealed override bool requiresCompletion => true;

    internal bool isNew => (_modifiers & DeclarationModifiers.New) != 0;

    private protected void CheckAccessibility(BelteDiagnosticQueue diagnostics) {
        ModifierHelpers.CheckAccessibility(_modifiers, diagnostics, errorLocation);
    }

    private protected void ReportModifiersDiagnostics(BelteDiagnosticQueue diagnostics) {
        // if (containingType.isSealed && declaredAccessibility == Accessibility.Protected) {
        //     diagnostics.Add(AccessCheck.GetProtectedMemberInSealedTypeError(containingType), ErrorLocation, this);
        // } else if (IsVolatile && IsReadOnly) {
        //     diagnostics.Add(ErrorCode.ERR_VolatileAndReadonly, ErrorLocation, this);
        // } else if (containingType.IsStatic && !IsStatic) {
        //     diagnostics.Add(ErrorCode.ERR_InstanceMemberInStaticClass, ErrorLocation, this);
        // } else if (!IsStatic && !IsReadOnly && containingType.IsReadOnly) {
        //     diagnostics.Add(ErrorCode.ERR_FieldsInRoStruct, ErrorLocation);
        // }

        // TODO
    }
}

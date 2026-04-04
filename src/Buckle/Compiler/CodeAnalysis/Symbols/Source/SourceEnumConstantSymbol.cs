using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceEnumConstantSymbol : SourceFieldSymbolWithSyntaxReference {
    public static SourceEnumConstantSymbol CreateExplicitValuedConstant(
        SourceMemberContainerTypeSymbol containingEnum,
        EnumMemberDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics) {
        return new ExplicitValuedEnumConstantSymbol(containingEnum, syntax, diagnostics);
    }

    public static SourceEnumConstantSymbol CreateImplicitValuedConstant(
        SourceMemberContainerTypeSymbol containingEnum,
        EnumMemberDeclarationSyntax syntax,
        SourceEnumConstantSymbol otherConstant,
        int otherConstantOffset,
        BelteDiagnosticQueue diagnostics) {
        if (otherConstant is null) {
            return new ZeroValuedEnumConstantSymbol(containingEnum, syntax, diagnostics);
        } else {
            return new ImplicitValuedEnumConstantSymbol(
                containingEnum,
                syntax,
                otherConstant,
                (uint)otherConstantOffset,
                diagnostics
            );
        }
    }

    protected SourceEnumConstantSymbol(
        SourceMemberContainerTypeSymbol containingEnum,
        EnumMemberDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics)
        : base(containingEnum, syntax.identifier.text, new SyntaxReference(syntax)) { }

    public sealed override RefKind refKind => RefKind.None;

    internal override TypeWithAnnotations GetFieldType(ConsList<FieldSymbol> fieldsBeingBound) {
        return new TypeWithAnnotations(containingType);
    }

    private protected sealed override DeclarationModifiers _modifiers
        => DeclarationModifiers.Const | DeclarationModifiers.Static | DeclarationModifiers.Public;

    public new EnumMemberDeclarationSyntax syntaxNode => (EnumMemberDeclarationSyntax)base.syntaxNode;

    private protected override OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations() {
        // TODO Attributes
        // if (containingType.AnyMemberHasAttributes) {
        //     return OneOrMany.Create(this.syntaxNode.attributeLists);
        // }

        return OneOrMany<SyntaxList<AttributeListSyntax>>.Empty;
    }

    internal sealed override void ForceComplete(TextLocation? location) {
        while (true) {
            var incompletePart = _state.nextIncompletePart;

            switch (incompletePart) {
                case CompletionParts.Attributes:
                    GetAttributes();
                    break;
                case CompletionParts.Type:
                    _state.NotePartComplete(CompletionParts.Type);
                    break;
                case CompletionParts.FixedSize:
                    _state.NotePartComplete(CompletionParts.FixedSize);
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
}

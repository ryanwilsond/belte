using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class FieldSymbolWithModifiers : FieldSymbol {
    private CustomAttributesBag<AttributeData> _lazyAttributeBag;
    private protected SymbolCompletionState _state;

    public sealed override bool isConst => (_modifiers & DeclarationModifiers.Const) != 0;

    public sealed override bool isConstExpr => (_modifiers & DeclarationModifiers.ConstExpr) != 0;

    internal abstract TextLocation errorLocation { get; }

    internal sealed override bool HasComplete(CompletionParts part) => _state.HasComplete(part);

    internal sealed override bool isStatic => (_modifiers & DeclarationModifiers.Static) != 0;

    internal sealed override Accessibility declaredAccessibility => ModifierHelpers.EffectiveAccessibility(_modifiers);

    private protected abstract DeclarationModifiers _modifiers { get; }

    internal sealed override ImmutableArray<AttributeData> GetAttributes() {
        return GetAttributesBag().attributes;
    }

    private CustomAttributesBag<AttributeData> GetAttributesBag() {
        var bag = _lazyAttributeBag;

        if (bag is not null && bag.isSealed)
            return bag;

        if (LoadAndValidateAttributes(GetAttributeDeclarations(), ref _lazyAttributeBag))
            _state.NotePartComplete(CompletionParts.Attributes);

        return _lazyAttributeBag;
    }

    private protected abstract OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations();
}

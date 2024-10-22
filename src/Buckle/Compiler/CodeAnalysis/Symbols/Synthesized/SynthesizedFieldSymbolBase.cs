using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SynthesizedFieldSymbolBase : FieldSymbol {
    private readonly DeclarationModifiers _modifiers;

    internal SynthesizedFieldSymbolBase(
        NamedTypeSymbol containingType,
        string name,
        bool isPublic,
        bool isConst,
        bool isStatic) {
        this.containingType = containingType;
        this.name = name;
        _modifiers = (isPublic ? DeclarationModifiers.Public : DeclarationModifiers.Private) |
            (isConst ? DeclarationModifiers.Const : DeclarationModifiers.None) |
            (isStatic ? DeclarationModifiers.Static : DeclarationModifiers.None);
    }

    public override string name { get; }

    internal override Symbol containingSymbol => containingType;

    internal override NamedTypeSymbol containingType { get; }

    internal override bool isConst => (_modifiers & DeclarationModifiers.Const) != 0;

    internal override bool isConstExpr => false;

    internal override SyntaxReference syntaxReference => null;

    internal override Accessibility declaredAccessibility => ModifierHelpers.EffectiveAccessibility(_modifiers);

    internal override bool isStatic => (_modifiers & DeclarationModifiers.Static) != 0;

    internal override bool isImplicitlyDeclared => true;

    internal abstract override TypeWithAnnotations GetFieldType(ConsList<FieldSymbol> fieldsBeingBound);

    internal override ConstantValue GetConstantValue(ConstantFieldsInProgress inProgress) {
        return null;
    }
}

using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class WrappedFieldSymbol : FieldSymbol {
    internal WrappedFieldSymbol(FieldSymbol underlyingField) {
        this.underlyingField = underlyingField;
    }

    public override string name => underlyingField.name;

    internal FieldSymbol underlyingField { get; }

    internal override Accessibility declaredAccessibility => underlyingField.declaredAccessibility;

    internal override bool isImplicitlyDeclared => underlyingField.isImplicitlyDeclared;

    internal override bool isConst => underlyingField.isConst;

    internal override bool isConstExpr => underlyingField.isConstExpr;

    internal override bool isRef => underlyingField.isRef;

    internal override object constantValue => underlyingField.constantValue;

    internal override SyntaxReference syntaxReference => underlyingField.syntaxReference;

    internal override bool isStatic => underlyingField.isStatic;

    internal override ConstantValue GetConstantValue(ConstantFieldsInProgress inProgress) {
        return underlyingField.GetConstantValue(inProgress);
    }
}

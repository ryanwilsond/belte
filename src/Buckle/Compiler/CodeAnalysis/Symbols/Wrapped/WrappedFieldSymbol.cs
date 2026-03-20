using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class WrappedFieldSymbol : FieldSymbol {
    internal WrappedFieldSymbol(FieldSymbol underlyingField) {
        this.underlyingField = underlyingField;
    }

    public override string name => underlyingField.name;

    public override bool isConst => underlyingField.isConst;

    public override bool isConstExpr => underlyingField.isConstExpr;

    public override RefKind refKind => underlyingField.refKind;

    public override object constantValue => underlyingField.constantValue;

    internal FieldSymbol underlyingField { get; }

    internal override Accessibility declaredAccessibility => underlyingField.declaredAccessibility;

    internal override bool isImplicitlyDeclared => underlyingField.isImplicitlyDeclared;

    internal override SyntaxReference syntaxReference => underlyingField.syntaxReference;

    internal override TextLocation location => underlyingField.location;

    internal override bool isStatic => underlyingField.isStatic;

    internal override ConstantValue GetConstantValue(ConstantFieldsInProgress inProgress) {
        return underlyingField.GetConstantValue(inProgress);
    }
}

using System.Collections.Immutable;
using System.Diagnostics;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SynthesizedBackingFieldSymbolBase : FieldSymbolWithModifiers {
    private readonly string _name;

    internal SynthesizedBackingFieldSymbolBase(
        string name,
        bool isConst,
        bool isStatic) {
        Debug.Assert(!string.IsNullOrEmpty(name));

        _name = name;

        _modifiers = DeclarationModifiers.Private |
            (isConst ? DeclarationModifiers.Const : DeclarationModifiers.None) |
            (isStatic ? DeclarationModifiers.Static : DeclarationModifiers.None);
    }

    internal abstract bool hasInitializer { get; }

    private protected override DeclarationModifiers _modifiers { get; }

    public override string name => _name;

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

    internal override SyntaxReference syntaxReference => null;

    internal override bool isImplicitlyDeclared => true;

    internal override ConstantValue GetConstantValue(ConstantFieldsInProgress inProgress) {
        return null;
    }
}

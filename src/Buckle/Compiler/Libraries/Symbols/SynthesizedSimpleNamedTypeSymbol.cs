using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.Libraries;

internal sealed class SynthesizedSimpleNamedTypeSymbol : NamedTypeSymbol {
    private readonly DeclarationModifiers _modifiers;

    internal SynthesizedSimpleNamedTypeSymbol(
        string name,
        TypeKind typeKind,
        NamedTypeSymbol baseType,
        DeclarationModifiers modifiers) {
        this.name = name;
        this.typeKind = typeKind;
        this.baseType = baseType;
        _modifiers = modifiers;
    }

    public override string name { get; }

    public override ImmutableArray<TemplateParameterSymbol> templateParameters => [];

    public override ImmutableArray<BoundExpression> templateConstraints => [];

    public override ImmutableArray<TypeOrConstant> templateArguments => [];

    public override int arity => 0;

    public override TypeKind typeKind { get; }

    internal override bool mangleName => false;

    internal override IEnumerable<string> memberNames => throw new InvalidOperationException();

    internal override NamedTypeSymbol constructedFrom => this;

    internal override Accessibility declaredAccessibility => ModifierHelpers.EffectiveAccessibility(_modifiers);

    internal override NamedTypeSymbol baseType { get; }

    internal override Symbol containingSymbol => null;

    internal override bool isStatic => (_modifiers & DeclarationModifiers.Static) != 0;

    internal override bool isAbstract => (_modifiers & DeclarationModifiers.Abstract) != 0;

    internal override bool isSealed => (_modifiers & DeclarationModifiers.Sealed) != 0;

    internal override SyntaxReference syntaxReference => null;

    internal override TextLocation location => null;

    internal override bool isRefLikeType => false;

    internal override NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved) {
        return baseType;
    }

    internal override ImmutableArray<Symbol> GetMembers() {
        throw new InvalidOperationException();
    }

    internal override ImmutableArray<Symbol> GetMembers(string name) {
        throw new InvalidOperationException();
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers() {
        throw new InvalidOperationException();
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name) {
        throw new InvalidOperationException();
    }
}

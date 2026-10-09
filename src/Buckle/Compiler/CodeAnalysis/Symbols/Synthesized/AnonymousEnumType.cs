using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class AnonymousEnumType : SynthesizedContainer {
    private readonly ImmutableArray<Symbol> _members;

    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>> _lazyMembersDictionary;

    internal AnonymousEnumType(
        Symbol parent,
        AnonymousEnumTypeSyntax syntax,
        Binder binder,
        BelteDiagnosticQueue diagnostics)
        : base(
            GeneratedNames.MakeAnonymousEnumName(parent.name),
            templateParameters: [],
            templateMap: TemplateMap.Empty) {
        var compilation = binder.compilation;
        containingSymbol = parent;
        baseType = compilation.GetSpecialType(SpecialType.Enum);

        if (syntax.baseType is { } baseSyntax) {
            enumUnderlyingType = SourceNamedTypeSymbol.BindAndVerifyEnumUnderlyingType(
                compilation,
                baseSyntax,
                diagnostics
            );
        } else {
            enumUnderlyingType = compilation.GetSpecialType(SpecialType.Int);
        }

        enumValueField = new SynthesizedEnumValueFieldSymbol(this);

        var builder = ArrayBuilder<Symbol>.GetInstance(syntax.members.Count + 1);
        SourceEnumConstantSymbol otherSymbol = null;
        var otherSymbolOffset = 0;

        foreach (var member in syntax.members) {
            var enumMember = (EnumMemberDeclarationSyntax)member;

            SourceEnumConstantSymbol symbol;
            var valueOpt = enumMember.equalsValue;

            if (valueOpt is not null) {
                symbol = SourceEnumConstantSymbol.CreateExplicitValuedConstant(this, enumMember, diagnostics);
            } else {
                symbol = SourceEnumConstantSymbol.CreateImplicitValuedConstant(
                    this,
                    enumMember,
                    otherSymbol,
                    otherSymbolOffset,
                    enumFlagsAttribute,
                    diagnostics
                );
            }

            builder.Add(symbol);

            if (valueOpt is not null || otherSymbol is null) {
                otherSymbol = symbol;
                otherSymbolOffset = 1;
            } else {
                otherSymbolOffset++;
            }
        }

        builder.Add(new SynthesizedInstanceConstructorSymbol(this));

        _members = builder.ToImmutableAndFree();
    }

    internal override Symbol containingSymbol { get; }

    public override TypeKind typeKind => TypeKind.Enum;

    internal override IEnumerable<string> memberNames => GetMembers().Select(m => m.name);

    internal override Accessibility declaredAccessibility => Accessibility.Public;

    internal override NamedTypeSymbol baseType { get; }

    internal override NamedTypeSymbol enumUnderlyingType { get; }

    internal FieldSymbol enumValueField { get; }

    internal override ImmutableArray<Symbol> GetMembers() {
        return _members;
    }

    internal override ImmutableArray<Symbol> GetMembers(string name) {
        if (GetMembersByName().TryGetValue(name.AsMemory(), out var members))
            return members;

        return [];
    }

    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>> GetMembersByName() {
        if (_lazyMembersDictionary is null) {
            var membersDictionary = SourceMemberContainerTypeSymbol.ToNameKeyedDictionary(_members);
            Interlocked.CompareExchange(ref _lazyMembersDictionary, membersDictionary, null);
        }

        return _lazyMembersDictionary;
    }
}

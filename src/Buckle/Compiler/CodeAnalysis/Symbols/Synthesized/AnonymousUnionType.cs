using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class AnonymousUnionType : SynthesizedContainer {
    private readonly MethodSymbol _constructor;
    private readonly ImmutableArray<SourceMemberFieldSymbol> _fields;

    internal AnonymousUnionType(NamedTypeSymbol parent, ImmutableArray<SourceMemberFieldSymbol> fields)
        : base(
            GeneratedNames.MakeAnonymousUnionName(parent.name, fields),
            templateParameters: [],
            templateMap: TemplateMap.Empty) {
        containingSymbol = parent;
        _constructor = new SynthesizedInstanceConstructorSymbol(this);
        _fields = fields;
    }

    internal override Symbol containingSymbol { get; }

    public override TypeKind typeKind => TypeKind.Struct;

    internal override MethodSymbol constructor => _constructor;

    internal override IEnumerable<string> memberNames => GetMembers().Select(m => m.name);

    internal override Accessibility declaredAccessibility => Accessibility.Public;

    internal override NamedTypeSymbol baseType => null;

    internal override bool isUnionStruct => true;

    internal override ImmutableArray<Symbol> GetMembers() {
        return [_constructor, .. _fields];
    }

    internal override ImmutableArray<Symbol> GetMembers(string name) {
        // ? Shouldn't need to cache this lookup since lookups are rare
        return GetMembers().WhereAsArray(m => m.name == name);
    }
}

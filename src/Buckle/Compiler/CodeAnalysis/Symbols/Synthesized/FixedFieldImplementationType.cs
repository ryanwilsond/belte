using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class FixedFieldImplementationType : SynthesizedContainer {
    internal const string FixedElementFieldName = "FixedElementField";

    private readonly SourceMemberFieldSymbol _field;
    private readonly MethodSymbol _constructor;
    private readonly FieldSymbol _internalField;

    internal FixedFieldImplementationType(SourceMemberFieldSymbol field)
        : base(
            GeneratedNames.MakeFixedFieldImplementationName(field.name),
            templateParameters: [],
            templateMap: TemplateMap.Empty) {
        _field = field;
        _constructor = new SynthesizedInstanceConstructorSymbol(this);
        _internalField = new SynthesizedFieldSymbol(
            this,
            ((PointerTypeSymbol)field.type).pointedAtType,
            FixedElementFieldName,
            isPublic: true,
            false,
            false,
            false
        );
    }

    internal override Symbol containingSymbol => _field.containingType;

    public override TypeKind typeKind => TypeKind.Struct;

    internal override MethodSymbol constructor => _constructor;

    internal override FieldSymbol fixedElementField => _internalField;

    internal override IEnumerable<string> memberNames
        => SpecializedCollections.SingletonEnumerable(FixedElementFieldName);

    internal override Accessibility declaredAccessibility => Accessibility.Public;

    internal override NamedTypeSymbol baseType => null;

    internal override ImmutableArray<Symbol> GetMembers() {
        return [_constructor, _internalField];
    }

    internal override ImmutableArray<Symbol> GetMembers(string name) {
        return
            (name == _constructor.name) ? [_constructor] :
            (name == FixedElementFieldName) ? [_internalField] :
            [];
    }
}

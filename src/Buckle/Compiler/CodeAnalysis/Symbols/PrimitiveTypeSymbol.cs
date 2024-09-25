
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Builtin primitive types such as Int, Float, etc.
/// </summary>
internal sealed class PrimitiveTypeSymbol : TypeSymbol {
    internal PrimitiveTypeSymbol(string name, SpecialType specialType) : base(name) {
        this.specialType = specialType;
    }

    public override bool isStatic => false;

    public override bool isVirtual => false;

    public override bool isAbstract => false;

    public override bool isSealed => false;

    public override bool isOverride => false;

    internal override NamedTypeSymbol baseType => null;

    internal override TypeKind typeKind => TypeKind.Primitive;

    internal override SpecialType specialType { get; }

    public new TypeSymbol originalDefinition => originalTypeDefinition;

    public override TypeSymbol originalTypeDefinition { get; }

    public override Symbol originalSymbolDefinition => originalTypeDefinition;
}

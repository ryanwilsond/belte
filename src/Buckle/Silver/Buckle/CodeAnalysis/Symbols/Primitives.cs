
namespace Buckle.CodeAnalysis.Symbols;

internal class TypeSymbol : Symbol {
    public static readonly TypeSymbol Error = new TypeSymbol("?");
    public static readonly TypeSymbol Int = new TypeSymbol("int");
    public static readonly TypeSymbol Decimal = new TypeSymbol("decimal");
    public static readonly TypeSymbol Bool = new TypeSymbol("bool");
    public static readonly TypeSymbol String = new TypeSymbol("string");
    public static readonly CollectionTypeSymbol Collection = new CollectionTypeSymbol("collection");
    public static readonly TypeSymbol Any = new TypeSymbol("any");
    public static readonly TypeSymbol Void = new TypeSymbol("void");

    public override SymbolType type => SymbolType.Type;

    internal TypeSymbol(string name) : base(name) { }
}

internal sealed class CollectionTypeSymbol : TypeSymbol {
    public TypeSymbol itemType;

    internal CollectionTypeSymbol(string name) : base(name) { }
}

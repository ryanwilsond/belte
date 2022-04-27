
namespace Buckle.CodeAnalysis.Symbols;

internal sealed class TypeSymbol : Symbol {
    public static readonly TypeSymbol Error = new TypeSymbol("?");
    public static readonly TypeSymbol Int = new TypeSymbol("int");
    public static readonly TypeSymbol Bool = new TypeSymbol("bool");
    public static readonly TypeSymbol String = new TypeSymbol("string");
    public static readonly TypeSymbol Any = new TypeSymbol("any");
    public static readonly TypeSymbol Void = new TypeSymbol("void");

    public override SymbolType type => SymbolType.Type;

    private TypeSymbol(string name) : base(name) { }
}

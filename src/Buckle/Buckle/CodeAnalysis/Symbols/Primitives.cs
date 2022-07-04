
namespace Buckle.CodeAnalysis.Symbols;

internal class TypeSymbol : Symbol {
    internal static readonly TypeSymbol Error = new TypeSymbol("?");
    internal static readonly TypeSymbol Int = new TypeSymbol("int");
    internal static readonly TypeSymbol Decimal = new TypeSymbol("decimal");
    internal static readonly TypeSymbol Bool = new TypeSymbol("bool");
    internal static readonly TypeSymbol String = new TypeSymbol("string");
    internal static readonly TypeSymbol Any = new TypeSymbol("any");
    internal static readonly TypeSymbol Void = new TypeSymbol("void");

    internal override SymbolType type => SymbolType.Type;

    internal TypeSymbol(string name) : base(name) { }
}

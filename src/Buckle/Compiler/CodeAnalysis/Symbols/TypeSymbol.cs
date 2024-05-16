
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A type symbol. This is just the base type name, not a full <see cref="Binding.BoundType" />.
/// </summary>
internal abstract class TypeSymbol : Symbol, ITypeSymbol {
    /// <summary>
    /// Error type (meaning something went wrong, not an actual type).
    /// </summary>
    internal static readonly TypeSymbol Error = new PrimitiveTypeSymbol("?");

    /// <summary>
    /// Integer type (any whole number, signed).
    /// </summary>
    internal static readonly TypeSymbol Int = new PrimitiveTypeSymbol("int");

    /// <summary>
    /// Decimal type (any floating point number, precision TBD).
    /// </summary>
    internal static readonly TypeSymbol Decimal = new PrimitiveTypeSymbol("decimal");

    /// <summary>
    /// Boolean type (true/false).
    /// </summary>
    internal static readonly TypeSymbol Bool = new PrimitiveTypeSymbol("bool");

    /// <summary>
    /// String type.
    /// </summary>
    internal static readonly TypeSymbol String = new PrimitiveTypeSymbol("string");

    /// <summary>
    /// Character type.
    /// </summary>
    internal static readonly TypeSymbol Char = new PrimitiveTypeSymbol("char");

    /// <summary>
    /// Any type (effectively the object type).
    /// </summary>
    internal static readonly TypeSymbol Any = new PrimitiveTypeSymbol("any");

    /// <summary>
    /// Void type (lack of type, exclusively used in method declarations).
    /// </summary>
    internal static readonly TypeSymbol Void = new PrimitiveTypeSymbol("void");

    /// <summary>
    /// Type type (contains a type, e.g. type myVar = typeof(int) ).
    /// </summary>
    internal static readonly TypeSymbol Type = new PrimitiveTypeSymbol("type");

    /// <summary>
    /// Type used to represent function (or method) signatures. Purely an implementation detail, cannot be used
    /// by users.
    /// </summary>
    internal static readonly TypeSymbol Func = new PrimitiveTypeSymbol("Func");

    /// <summary>
    /// Creates a new <see cref="TypeSymbol" />.
    /// Use predefined type symbols if possible.
    /// </summary>
    /// <param name="name">Name of type.</param>
    protected TypeSymbol(string name) : base(name) { }

    protected TypeSymbol(string name, Accessibility accessibility) : base(name, accessibility) { }

    public override SymbolKind kind => SymbolKind.Type;

    /// <summary>
    /// Number of template parameters the type has.
    /// </summary>
    internal virtual int arity => 0;
}

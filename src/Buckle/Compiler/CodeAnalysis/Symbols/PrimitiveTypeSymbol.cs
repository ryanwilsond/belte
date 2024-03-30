
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Builtin primitive types such as Int, Float, etc.
/// </summary>
internal sealed class PrimitiveTypeSymbol : TypeSymbol {
    internal PrimitiveTypeSymbol(string name) : base(name) { }

    public override bool isStatic => false;
}

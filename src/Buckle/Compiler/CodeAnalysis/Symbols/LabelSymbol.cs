
namespace Buckle.CodeAnalysis.Symbols;

internal sealed class LabelSymbol : Symbol {
    private static int Sequence = 1;

    internal LabelSymbol(string name) {
        // TODO Remove this when not debugging
        var sequence = System.Threading.Interlocked.Add(ref Sequence, 1);
        this.name = $"<{name}-{sequence & 0xFFFF}>";
    }

    public override string name { get; }

    public override SymbolKind kind => SymbolKind.Label;

    internal override bool isStatic => false;

    internal override bool isOverride => false;

    internal override bool isAbstract => false;

    internal override bool isSealed => false;

    internal override bool isVirtual => false;

}

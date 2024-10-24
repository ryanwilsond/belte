using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SynthesizedLabelSymbol : LabelSymbol {
    internal SynthesizedLabelSymbol(string name) {
        this.name = name;
    }

    public override string name { get; }

    internal override SyntaxReference syntaxReference => null;
}

using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SynthesizedLabelSymbol : LabelSymbol {
    internal SynthesizedLabelSymbol(string name) {
        this.name = name;
    }

    public override string name { get; }

    internal override SyntaxReference syntaxReference => null;

    internal override TextLocation location => null;
}

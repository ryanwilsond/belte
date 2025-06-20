using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis;

internal sealed class MetadataLocation : TextLocation {
    private readonly ModuleSymbol _module;

    internal MetadataLocation(ModuleSymbol module) {
        _module = module;
    }

    public override SourceText text => null;

    public override TextSpan span => null;

    public override SyntaxTree tree => null;

    public override string fileName => _module.fileName;

    public override bool Equals(TextLocation other) {
        return Equals(other as MetadataLocation);
    }

    public bool Equals(MetadataLocation other) {
        return other is not null && other._module == _module;
    }
}

using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Libraries;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class WithBinder : LocalScopeBinder {
    private readonly StatementSyntax _syntax;

    private SynthesizedDataContainerSymbol _commitLocal;

    internal WithBinder(Binder enclosing, StatementSyntax syntax)
        : base(enclosing) {
        _syntax = syntax;
    }

    internal override SyntaxNode scopeDesignator => _syntax;

    internal override SynthesizedDataContainerSymbol commitLocal => _commitLocal;

    private protected override SynthesizedDataContainerSymbol BuildWithCommit() {
        _commitLocal = new SynthesizedDataContainerSymbol(
            containingMember,
            new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Bool)),
            SynthesizedLocalKind.ExpanderTemp,
            "commit"
        );

        return _commitLocal;
    }
}

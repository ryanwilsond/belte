using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceParameterSymbol : SourceParameterSymbolBase {
    private protected SymbolCompletionState _state;

    private protected SourceParameterSymbol(
        Symbol owner,
        int ordinal,
        RefKind refKind,
        ScopedKind scope,
        string name,
        ParameterSyntax syntax)
        : base(owner, ordinal) {
        this.refKind = refKind;
        effectiveScope = scope;
        this.name = name;
        syntaxReference = new SyntaxReference(syntax);
    }

    public sealed override string name { get; }

    internal sealed override bool requiresCompletion => true;

    internal sealed override RefKind refKind { get; }

    internal override SyntaxReference syntaxReference { get; }

    internal override bool isImplicitlyDeclared => false;

    internal override ScopedKind effectiveScope { get; }

    internal abstract bool hasDefaultArgumentSyntax { get; }

    internal static SourceParameterSymbol Create(
        Symbol owner,
        TypeWithAnnotations parameterType,
        ParameterSyntax syntax,
        RefKind refKind,
        SyntaxToken identifier,
        int ordinal,
        ScopedKind scope) {
        var name = identifier.text;

        if (syntax.defaultValue is null && scope == ScopedKind.None)
            return new SourceSimpleParameterSymbol(owner, parameterType, ordinal, refKind, name, syntax);

        return new SourceComplexParameterSymbol(
            owner,
            ordinal,
            parameterType,
            refKind,
            name,
            syntax,
            scope
        );
    }

    internal override void AddDeclarationDiagnostics(BelteDiagnosticQueue diagnostics) {
        containingSymbol.AddDeclarationDiagnostics(diagnostics);
    }

    internal sealed override bool HasComplete(CompletionParts part) {
        return _state.HasComplete(part);
    }

    internal override void ForceComplete(TextLocation location) {
        _state.DefaultForceComplete();
    }
}

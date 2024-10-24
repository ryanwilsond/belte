using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceParameterSymbol : SourceParameterSymbolBase {
    private protected SymbolCompletionState _state;

    private protected SourceParameterSymbol(
        Symbol owner,
        int ordinal,
        RefKind refKind,
        ScopedKind scope,
        string name,
        TextLocation location)
        : base(owner, ordinal) {
        _refKind = refKind;
        _scope = scope;
        _name = name;
        _location = location;
    }

    internal sealed override bool requiresCompletion => true;

    internal sealed override bool HasComplete(CompletionParts part) {
        return _state.HasComplete(part);
    }

    internal override void ForceComplete(TextLocation location) {
        _state.DefaultForceComplete();
    }
}

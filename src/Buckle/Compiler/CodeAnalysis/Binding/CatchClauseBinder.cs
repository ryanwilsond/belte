using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class CatchClauseBinder : LocalScopeBinder {
    private readonly CatchClauseSyntax _syntax;

    internal CatchClauseBinder(Binder enclosing, CatchClauseSyntax syntax)
        : base(enclosing, (enclosing.flags | BinderFlags.InCatchBlock) & ~BinderFlags.InNestedFinallyBlock) {
        _syntax = syntax;
    }

    internal override SyntaxNode scopeDesignator => _syntax;

    internal override ImmutableArray<DataContainerSymbol> GetDeclaredLocalsForScope(SyntaxNode scopeDesignator) {
        if (_syntax == scopeDesignator)
            return locals;

        throw ExceptionUtilities.Unreachable();
    }

    internal override ImmutableArray<LocalFunctionSymbol> GetDeclaredLocalFunctionsForScope(
        BelteSyntaxNode scopeDesignator) {
        throw ExceptionUtilities.Unreachable();
    }

    private protected override ImmutableArray<DataContainerSymbol> BuildLocals() {
        // TODO Eventually this will create a filter local (e.g. `catch (SomeException as e)`)
        return [];
    }
}

using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class BlockBinder : LocalScopeBinder {
    private readonly BlockStatementSyntax _block;

    internal BlockBinder(Binder enclosing, BlockStatementSyntax block) : this(enclosing, block, enclosing.flags) { }

    internal BlockBinder(Binder enclosing, BlockStatementSyntax block, BinderFlags additionalFlags)
        : base(enclosing, enclosing.flags | additionalFlags) {
        _block = block;
    }

    internal override bool isLocalFunctionsScopeBinder => true;

    internal override bool isLabelsScopeBinder => true;

    internal override SyntaxNode scopeDesignator => _block;

    internal override ImmutableArray<DataContainerSymbol> GetDeclaredLocalsForScope(SyntaxNode scopeDesignator) {
        if (this.scopeDesignator == scopeDesignator)
            return locals;

        throw ExceptionUtilities.Unreachable();
    }

    internal override ImmutableArray<LocalFunctionSymbol> GetDeclaredLocalFunctionsForScope(
        BelteSyntaxNode scopeDesignator) {
        if (this.scopeDesignator == scopeDesignator)
            return localFunctions;

        throw ExceptionUtilities.Unreachable();
    }

    private protected override ImmutableArray<DataContainerSymbol> BuildLocals() {
        return BuildLocals(_block.statements, this);
    }

    private protected override ImmutableArray<LocalFunctionSymbol> BuildLocalFunctions() {
        return BuildLocalFunctions(_block.statements);
    }

    private protected override ImmutableArray<LabelSymbol> BuildLabels() {
        ArrayBuilder<LabelSymbol> labels = null;
        BuildLabels(_block.statements, ref labels);
        return (labels is not null) ? labels.ToImmutableAndFree() : [];
    }
}

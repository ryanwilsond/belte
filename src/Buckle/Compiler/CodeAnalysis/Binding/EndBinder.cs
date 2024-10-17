using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class EndBinder : Binder {
    internal EndBinder(Compilation compilation) : base(compilation) { }

    internal override ConstantFieldsInProgress constantFieldsInProgress => ConstantFieldsInProgress.Empty;

    internal override ConsList<FieldSymbol> fieldsBeingBound => ConsList<FieldSymbol>.Empty;

    internal override LocalVariableSymbol localInProgress => null;

    internal override SynthesizedLabelSymbol breakLabel => null;

    internal override SynthesizedLabelSymbol continueLabel => null;

    internal override BoundExpression conditionalReceiverExpression => null;

    internal override Symbol containingMember => null;

    internal override Binder GetBinder(SyntaxNode node) {
        return null;
    }

    internal override ImmutableArray<LocalVariableSymbol> GetDeclaredLocalsForScope(
        SyntaxNode scopeDesignator) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override ImmutableArray<LocalFunctionSymbol> GetDeclaredLocalFunctionsForScope(
        BelteSyntaxNode scopeDesignator) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override BoundForStatement BindForParts(BelteDiagnosticQueue diagnostics, Binder originalBinder) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override BoundWhileStatement BindWhileParts(BelteDiagnosticQueue diagnostics, Binder originalBinder) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override BoundDoWhileStatement BindDoWhileParts(BelteDiagnosticQueue diagnostics, Binder originalBinder) {
        throw ExceptionUtilities.Unreachable();
    }

    private protected override bool IsUnboundTypeAllowed(TemplateNameSyntax syntax) {
        return false;
    }

    private protected override SourceLocalSymbol LookupLocal(SyntaxToken identifier) {
        return null;
    }

    private protected override LocalFunctionSymbol LookupLocalFunction(SyntaxToken identifier) {
        return null;
    }

}

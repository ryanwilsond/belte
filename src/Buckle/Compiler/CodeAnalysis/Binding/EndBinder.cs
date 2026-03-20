using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class EndBinder : Binder {
    internal EndBinder(Compilation compilation, SourceText associatedText) : base(compilation) {
        this.associatedText = associatedText;
    }

    internal readonly SourceText associatedText;

    internal override ConstantFieldsInProgress constantFieldsInProgress => ConstantFieldsInProgress.Empty;

    internal override ConsList<FieldSymbol> fieldsBeingBound => ConsList<FieldSymbol>.Empty;

    internal override DataContainerSymbol localInProgress => null;

    internal override SynthesizedLabelSymbol breakLabel => null;

    internal override SynthesizedLabelSymbol continueLabel => null;

    internal override BoundExpression conditionalReceiverExpression => null;

    internal override Symbol containingMember => null;

    internal override bool isInsideNameof => false;

    internal override bool isInMethodBody => false;

    internal override QuickAttributeChecker quickAttributeChecker => QuickAttributeChecker.Predefined;

    private protected override SyntaxNode _enclosingNameofArgument => null;

    private protected override bool _inExecutableBinder => false;

    internal override Binder GetBinder(SyntaxNode node) {
        return null;
    }

    internal override ImmutableArray<DataContainerSymbol> GetDeclaredLocalsForScope(
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

    internal override bool IsAccessibleHelper(
        Symbol symbol,
        TypeSymbol accessThroughType,
        out bool failedThroughTypeCheck) {
        failedThroughTypeCheck = false;
        return IsSymbolAccessibleConditional(symbol, compilation.globalNamespaceInternal);
    }

    private protected override bool IsUnboundTypeAllowed(TemplateNameSyntax syntax) {
        return false;
    }

    private protected override SourceDataContainerSymbol LookupLocal(SyntaxToken identifier) {
        return null;
    }

    private protected override LocalFunctionSymbol LookupLocalFunction(SyntaxToken identifier) {
        return null;
    }
}

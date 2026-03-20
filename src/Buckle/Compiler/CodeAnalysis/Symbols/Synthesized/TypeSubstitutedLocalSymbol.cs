using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class TypeSubstitutedLocalSymbol : DataContainerSymbol {
    private readonly DataContainerSymbol _originalVariable;
    private readonly TypeWithAnnotations _type;
    private readonly Symbol _containingSymbol;

    public TypeSubstitutedLocalSymbol(
        DataContainerSymbol originalVariable,
        TypeWithAnnotations type,
        Symbol containingSymbol) {
        _originalVariable = originalVariable;
        _type = type;
        _containingSymbol = containingSymbol;
    }

    internal override DataContainerDeclarationKind declarationKind => _originalVariable.declarationKind;

    internal override SynthesizedLocalKind synthesizedKind => _originalVariable.synthesizedKind;

    internal override SyntaxNode scopeDesignator => _originalVariable.scopeDesignator;

    public override string name => _originalVariable.name;

    internal override Symbol containingSymbol => _containingSymbol;

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences
        => _originalVariable.declaringSyntaxReferences;

    internal override SyntaxReference syntaxReference => _originalVariable.syntaxReference;

    internal override bool hasSourceLocation => _originalVariable.hasSourceLocation;

    internal override ImmutableArray<TextLocation> locations => _originalVariable.locations;

    internal override TextLocation location => _originalVariable.location;

    internal override TypeWithAnnotations typeWithAnnotations => _type;

    internal override SyntaxToken identifierToken => _originalVariable.identifierToken;

    public override RefKind refKind => _originalVariable.refKind;

    // TODO Any way this backfires/isn't necessary?
    private protected override Symbol _originalSymbolDefinition => _originalVariable;

    internal override ScopedKind scope => throw new System.NotImplementedException();

    internal override bool isCompilerGenerated => _originalVariable.isCompilerGenerated;

    internal override ConstantValue GetConstantValue(
        SyntaxNode node,
        DataContainerSymbol inProgress,
        BelteDiagnosticQueue diagnostics) {
        return _originalVariable.GetConstantValue(node, inProgress, diagnostics);
    }

    internal override BelteDiagnosticQueue GetConstantValueDiagnostics(BoundExpression boundInitValue) {
        return _originalVariable.GetConstantValueDiagnostics(boundInitValue);
    }

    internal override SyntaxNode GetDeclarationSyntax() {
        return _originalVariable.GetDeclarationSyntax();
    }
}

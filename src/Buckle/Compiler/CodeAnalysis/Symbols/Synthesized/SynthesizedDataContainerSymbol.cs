using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SynthesizedDataContainerSymbol : DataContainerSymbol {
    private static int _synthCount;

    private readonly SyntaxNode _syntax;

    internal SynthesizedDataContainerSymbol(
        Symbol containingSymbol,
        TypeWithAnnotations type,
        SynthesizedLocalKind kind,
        SyntaxNode syntax = null,
        RefKind refKind = RefKind.None) {
        this.containingSymbol = containingSymbol;
        this.refKind = refKind;
        typeWithAnnotations = type;
        // This is the syntax of just the name
        _syntax = syntax;
        synthesizedKind = kind;
        name = GeneratedNames.MakeSynthedLocalName(type, _synthCount++);
    }

    internal SynthesizedDataContainerSymbol(
        Symbol containingSymbol,
        TypeWithAnnotations type,
        SynthesizedLocalKind kind,
        string name,
        RefKind refKind = RefKind.None) {
        this.containingSymbol = containingSymbol;
        this.refKind = refKind;
        typeWithAnnotations = type;
        this.name = name;
        synthesizedKind = kind;
    }

    public override string name { get; }

    public override RefKind refKind { get; }

    internal override Symbol containingSymbol { get; }

    internal override SyntaxReference syntaxReference => _syntax is null ? null : new SyntaxReference(_syntax);

    internal override TextLocation location => _syntax is null ? null : syntaxReference.location;

    internal override TypeWithAnnotations typeWithAnnotations { get; }

    internal override DataContainerDeclarationKind declarationKind => DataContainerDeclarationKind.None;

    internal override SyntaxNode scopeDesignator => null;

    internal override SyntaxToken identifierToken => null;

    internal override bool isImplicitlyDeclared => true;

    internal override bool isCompilerGenerated => true;

    internal override ScopedKind scope => ScopedKind.None;

    internal override bool hasSourceLocation => _syntax is not null;

    internal override SynthesizedLocalKind synthesizedKind { get; }

    internal override SyntaxNode GetDeclarationSyntax() {
        return _syntax;
    }

    internal override ConstantValue GetConstantValue(SyntaxNode node, DataContainerSymbol inProgress, BelteDiagnosticQueue diagnostics) {
        return null;
    }

    internal override BelteDiagnosticQueue GetConstantValueDiagnostics(BoundExpression boundInitValue) {
        return BelteDiagnosticQueue.Discarded;
    }
}

using System;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal sealed partial class ILOptimizer {
    internal sealed class DummyLocal : DataContainerSymbol {
        internal override DataContainerDeclarationKind declarationKind => DataContainerDeclarationKind.None;

        internal override SynthesizedLocalKind synthesizedKind => SynthesizedLocalKind.OptimizerTemp;

        internal override SyntaxNode scopeDesignator => null;

        internal override SyntaxToken identifierToken => default;

        internal override bool isPinned => false;

        internal override Symbol containingSymbol => throw new InvalidOperationException();

        internal override TypeWithAnnotations typeWithAnnotations => throw new InvalidOperationException();

        internal override ImmutableArray<TextLocation> locations => throw new InvalidOperationException();

        internal override TextLocation location => throw new InvalidOperationException();

        internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences
            => throw new InvalidOperationException();

        internal override SyntaxReference syntaxReference => throw new InvalidOperationException();

        internal override bool isCompilerGenerated => true;

        internal override bool hasSourceLocation => false;

        public override RefKind refKind => RefKind.None;

        internal override ScopedKind scope => ScopedKind.None;

        internal override ConstantValue GetConstantValue(
            SyntaxNode node,
            DataContainerSymbol inProgress,
            BelteDiagnosticQueue diagnostics) {
            throw new InvalidOperationException();
        }

        internal override BelteDiagnosticQueue GetConstantValueDiagnostics(BoundExpression boundInitValue) {
            throw new InvalidOperationException();
        }

        internal override SyntaxNode GetDeclarationSyntax() {
            throw new InvalidOperationException();
        }
    }
}

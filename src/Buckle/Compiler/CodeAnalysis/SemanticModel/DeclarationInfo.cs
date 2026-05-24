using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis;

internal readonly struct DeclarationInfo {
    internal DeclarationInfo(
        SyntaxNode declaredNode,
        ImmutableArray<SyntaxNode> executableCodeBlocks,
        ISymbol declaredSymbol) {
        this.declaredNode = declaredNode;
        this.executableCodeBlocks = executableCodeBlocks;
        this.declaredSymbol = declaredSymbol;
    }

    public SyntaxNode declaredNode { get; }

    public ImmutableArray<SyntaxNode> executableCodeBlocks { get; }

    public ISymbol declaredSymbol { get; }
}

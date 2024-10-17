using System.Threading;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Represents a <see cref="SyntaxNode" /> that is apart of the language syntax.
/// </summary>
public abstract class BelteSyntaxNode : SyntaxNode {
    /// <summary>
    /// Creates a <see cref="BelteSyntaxNode" />.
    /// </summary>
    internal BelteSyntaxNode(SyntaxNode parent, GreenNode green, int position) : base(parent, green, position) { }

    internal BelteSyntaxNode(GreenNode green, int position, SyntaxTree syntaxTree)
        : base(green, position, syntaxTree) { }

    internal override SyntaxTree syntaxTree => _syntaxTree ?? ComputeSyntaxTree(this);

    internal new BelteSyntaxNode parent => (BelteSyntaxNode)base.parent;

    internal abstract TResult Accept<TResult>(SyntaxVisitor<TResult> visitor);

    internal abstract void Accept(SyntaxVisitor visitor);

    private static SyntaxTree ComputeSyntaxTree(BelteSyntaxNode node) {
        ArrayBuilder<BelteSyntaxNode> nodes = null;
        SyntaxTree tree;

        while (true) {
            tree = node._syntaxTree;

            if (tree is not null)
                break;

            var parent = node.parent;

            if (parent is null) {
                Interlocked.CompareExchange(ref node._syntaxTree, SyntaxTree.CreateWithoutClone(node), null);
                tree = node._syntaxTree;
                break;
            }

            tree = parent._syntaxTree;

            if (tree is not null) {
                node._syntaxTree = tree;
                break;
            }

            (nodes ??= ArrayBuilder<BelteSyntaxNode>.GetInstance()).Add(node);
            node = parent;
        }

        if (nodes is not null) {
            foreach (var n in nodes) {
                var existingTree = n._syntaxTree;

                if (existingTree is not null)
                    break;

                n._syntaxTree = tree;
            }

            nodes.Free();
        }

        return tree;
    }
}

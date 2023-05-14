
using System.Threading;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Syntax;

public abstract class BelteSyntaxNode : SyntaxNode {
    internal BelteSyntaxNode(SyntaxNode parent, GreenNode green, int position) : base(parent, green, position) { }

    internal override SyntaxTree syntaxTree => _syntaxTree ?? ComputeSyntaxTree(this);

    internal new BelteSyntaxNode parent => (BelteSyntaxNode)base.parent;

    private static SyntaxTree ComputeSyntaxTree(BelteSyntaxNode node) {
        ArrayBuilder<BelteSyntaxNode> nodes = null;
        SyntaxTree tree = null;

        while (true) {
            tree = node._syntaxTree;

            if (tree != null)
                break;

            var parent = node.parent;

            if (parent == null) {
                Interlocked.CompareExchange(ref node._syntaxTree, SyntaxTree.CreateWithoutClone(node), null);
                tree = node._syntaxTree;
                break;
            }

            tree = parent._syntaxTree;

            if (tree != null) {
                node._syntaxTree = tree;
                break;
            }

            (nodes ?? (nodes = ArrayBuilder<BelteSyntaxNode>.GetInstance())).Add(node);
            node = parent;
        }

        if (nodes != null) {
            foreach (var n in nodes) {
                var existingTree = n._syntaxTree;

                if (existingTree != null)
                    break;

                n._syntaxTree = tree;
            }

            nodes.Free();
        }

        return tree;
    }
}

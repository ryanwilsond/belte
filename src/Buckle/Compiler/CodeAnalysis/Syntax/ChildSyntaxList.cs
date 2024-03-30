using System;
using System.Collections;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Represents and list of child nodes and tokens.
/// </summary>
public sealed partial class ChildSyntaxList : IReadOnlyList<SyntaxNodeOrToken> {
    internal ChildSyntaxList(SyntaxNode node) {
        this.node = node;
        Count = CountNodes(node.green);
    }

    /// <summary>
    /// The number of children.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Gets the child at the given slot index.
    /// </summary>
    public SyntaxNodeOrToken this[int index] {
        get {
            if (unchecked((uint)index < (uint)Count))
                return ItemInternal(node, index);

            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    /// <summary>
    /// The parent of the child list.
    /// </summary>
    internal SyntaxNode node { get; }

    public Enumerator GetEnumerator() {
        return new Enumerator(node, Count);
    }

    IEnumerator<SyntaxNodeOrToken> IEnumerable<SyntaxNodeOrToken>.GetEnumerator() {
        if (node is null)
            return new EmptyEnumerator<SyntaxNodeOrToken>();

        return new EnumeratorImpl(node, Count);
    }

    IEnumerator IEnumerable.GetEnumerator() {
        if (node is null)
            return new EmptyEnumerator<SyntaxNodeOrToken>();

        return new EnumeratorImpl(node, Count);
    }

    /// <summary>
    /// Gets the node or token at the given index from the given node. Unlike <see cref="SyntaxNode.GetNodeSlot" />,
    /// it uses the underlying node to get tokens as well.
    /// </summary>
    internal static SyntaxNodeOrToken ItemInternal(SyntaxNode node, int index) {
        GreenNode greenChild;
        var green = node.green;
        var idx = index;
        var slotIndex = 0;
        var position = node.position;

        while (true) {
            greenChild = green.GetSlot(slotIndex);

            if (greenChild != null) {
                var currentOccupancy = Occupancy(greenChild);

                if (idx < currentOccupancy)
                    break;

                idx -= currentOccupancy;
                position += greenChild.fullWidth;
            }

            slotIndex++;
        }

        var red = node.GetNodeSlot(slotIndex);

        if (!greenChild.isList) {
            if (red != null)
                return red;
        } else if (red != null) {
            var redChild = red.GetNodeSlot(idx);

            if (redChild != null)
                return redChild;

            greenChild = greenChild.GetSlot(idx);
            position = red.GetChildPosition(idx);
        } else {
            position += greenChild.GetSlotOffset(idx);
            greenChild = greenChild.GetSlot(idx);
        }

        return new SyntaxNodeOrToken(node, greenChild, position, index);
    }

    /// <summary>
    /// Returns the child node or token of the given node whose span contains the target position.
    /// </summary>
    internal static SyntaxNodeOrToken ChildThatContainsPosition(SyntaxNode node, int targetPosition) {
        var green = node.green;
        var position = node.position;
        var index = 0;

        int slot;

        for (slot = 0; ; slot++) {
            var greenChild = green.GetSlot(slot);

            if (greenChild != null) {
                var endPosition = position + greenChild.fullWidth;

                if (targetPosition < endPosition) {
                    green = greenChild;
                    break;
                }

                position = endPosition;
                index += Occupancy(greenChild);
            }
        }

        var red = node.GetNodeSlot(slot);

        if (!green.isList) {
            if (red != null)
                return red;
        } else {
            slot = green.FindSlotIndexContainingOffset(targetPosition - position);

            if (red != null) {
                red = red.GetNodeSlot(slot);

                if (red != null)
                    return red;
            }

            position += green.GetSlotOffset(slot);
            green = green.GetSlot(slot);
            index += slot;
        }

        return new SyntaxNodeOrToken(node, green, position, index);
    }

    internal Reversed Reverse() {
        return new Reversed(node, Count);
    }

    internal bool Any() {
        return Count != 0;
    }

    internal SyntaxNodeOrToken First() {
        if (Any())
            return this[0];

        throw new InvalidOperationException();
    }

    internal SyntaxNodeOrToken Last() {
        if (Any())
            return this[Count - 1];

        throw new InvalidOperationException();
    }

    private static int CountNodes(GreenNode green) {
        var n = 0;

        for (int i = 0, s = green.slotCount; i < s; i++) {
            var child = green.GetSlot(i);

            if (child != null) {
                if (!child.isList)
                    n++;
                else
                    n += child.slotCount;
            }
        }

        return n;
    }

    private static SyntaxNode? ItemInternalAsNode(SyntaxNode node, int index) {
        GreenNode? greenChild;
        var green = node.green;
        var idx = index;
        var slotIndex = 0;

        while (true) {
            greenChild = green.GetSlot(slotIndex);

            if (greenChild != null) {
                var currentOccupancy = Occupancy(greenChild);

                if (idx < currentOccupancy)
                    break;

                idx -= currentOccupancy;
            }

            slotIndex++;
        }

        var red = node.GetNodeSlot(slotIndex);

        if (greenChild.isList && red != null)
            return red.GetNodeSlot(idx);

        return red;
    }

    private static int Occupancy(GreenNode green) {
        return green.isList ? green.slotCount : 1;
    }
}

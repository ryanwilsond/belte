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
    public SyntaxNode node { get; }

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

    internal static SyntaxNodeOrToken ItemInternal(SyntaxNode node, int index) {
        var slotData = new SlotData(node);
        return ItemInternal(node, index, ref slotData);
    }

    /// <summary>
    /// Gets the node or token at the given index from the given node. Unlike <see cref="SyntaxNode.GetNodeSlot" />,
    /// it uses the underlying node to get tokens as well.
    /// </summary>
    internal static SyntaxNodeOrToken ItemInternal(SyntaxNode node, int index, ref SlotData slotData) {
        GreenNode greenChild;
        var green = node.green;
        var idx = index - slotData.precedingOccupantSlotCount;
        var slotIndex = slotData.slotIndex;
        var position = slotData.positionAtSlotIndex;

        while (true) {
            greenChild = green.GetSlot(slotIndex);

            if (greenChild is not null) {
                var currentOccupancy = Occupancy(greenChild);

                if (idx < currentOccupancy)
                    break;

                idx -= currentOccupancy;
                position += greenChild.fullWidth;
            }

            slotIndex++;
        }

        if (slotIndex != slotData.slotIndex)
            slotData = new SlotData(slotIndex, index - idx, position);

        var red = node.GetNodeSlot(slotIndex);

        if (!greenChild.isList) {
            if (red is not null)
                return red;
        } else if (red is not null) {
            var redChild = red.GetNodeSlot(idx);

            if (redChild is not null)
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
    public static SyntaxNodeOrToken ChildThatContainsPosition(SyntaxNode node, int targetPosition) {
        var green = node.green;
        var position = node.position;
        var index = 0;

        int slot;

        for (slot = 0; ; slot++) {
            var greenChild = green.GetSlot(slot);

            if (greenChild is not null) {
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
            if (red is not null)
                return red;
        } else {
            slot = green.FindSlotIndexContainingOffset(targetPosition - position);

            if (red is not null) {
                red = red.GetNodeSlot(slot);

                if (red is not null)
                    return red;
            }

            position += green.GetSlotOffset(slot);
            green = green.GetSlot(slot);
            index += slot;
        }

        return new SyntaxNodeOrToken(node, green, position, index);
    }

    public Reversed Reverse() {
        return new Reversed(node, Count);
    }

    public bool Any() {
        return Count != 0;
    }

    public SyntaxNodeOrToken First() {
        if (Any())
            return this[0];

        throw new InvalidOperationException();
    }

    public SyntaxNodeOrToken Last() {
        if (Any())
            return this[Count - 1];

        throw new InvalidOperationException();
    }

    private static int CountNodes(GreenNode green) {
        var n = 0;

        for (int i = 0, s = green.slotCount; i < s; i++) {
            var child = green.GetSlot(i);

            if (child is not null) {
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

            if (greenChild is not null) {
                var currentOccupancy = Occupancy(greenChild);

                if (idx < currentOccupancy)
                    break;

                idx -= currentOccupancy;
            }

            slotIndex++;
        }

        var red = node.GetNodeSlot(slotIndex);

        if (greenChild.isList && red is not null)
            return red.GetNodeSlot(idx);

        return red;
    }

    private static int Occupancy(GreenNode green) {
        return green.isList ? green.slotCount : 1;
    }
}

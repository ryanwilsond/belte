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

    public int Count { get; }

    public SyntaxNodeOrToken this[int index] {
        get {
            if (unchecked((uint)index < (uint)Count))
                return ItemInternal(node, index);

            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    internal SyntaxNode node { get; }

    IEnumerator<SyntaxNodeOrToken> IEnumerable<SyntaxNodeOrToken>.GetEnumerator() {
        if (node == null)
            return new EmptyEnumerator<SyntaxNodeOrToken>();

        return new EnumeratorImpl(node, Count);
    }

    IEnumerator IEnumerable.GetEnumerator() {
        if (node == null)
            return new EmptyEnumerator<SyntaxNodeOrToken>();

        return new EnumeratorImpl(node, Count);
    }

    internal static int CountNodes(GreenNode green) {
        int n = 0;

        for (int i = 0, s = green.slotCount; i < s; i++) {
            var child = green.GetSlot(i);

            if (child != null)
                n++;
        }

        return n;
    }

    internal static SyntaxNode? ItemInternalAsNode(SyntaxNode node, int index) {
        GreenNode? greenChild;
        var green = node.green;
        var idx = index;
        var slotIndex = 0;

        while (true) {
            greenChild = green.GetSlot(slotIndex);

            if (greenChild != null) {
                int currentOccupancy = Occupancy(greenChild);

                if (idx < currentOccupancy)
                    break;

                idx -= currentOccupancy;
            }

            slotIndex++;
        }

        var red = node.GetNodeSlot(slotIndex);
        return red;
    }

    internal static SyntaxNodeOrToken ItemInternal(SyntaxNode node, int index) {
        GreenNode greenChild;
        var green = node.green;
        var idx = index;
        var slotIndex = 0;
        var position = node.position;

        while (true) {
            greenChild = green.GetSlot(slotIndex);

            if (greenChild != null) {
                int currentOccupancy = Occupancy(greenChild);

                if (idx < currentOccupancy)
                    break;

                idx -= currentOccupancy;
                position += greenChild.fullWidth;
            }

            slotIndex++;
        }

        var red = node.GetNodeSlot(slotIndex);

        if (red != null)
            return new SyntaxNodeOrToken(red);

        return new SyntaxNodeOrToken(node, greenChild, position, index);
    }

    internal static SyntaxNodeOrToken ChildThatContainsPosition(SyntaxNode node, int targetPosition) {
        GreenNode? green = node.green;
        var position = node.position;
        var index = 0;

        int slot;

        for (slot = 0; ; slot++) {
            GreenNode? greenChild = green.GetSlot(slot);

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

        if (red != null)
            return new SyntaxNodeOrToken(red);

        return new SyntaxNodeOrToken(node, green, position, index);
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

    private static int Occupancy(GreenNode node) {
        // Allowing room to add IsList in the future
        return node.slotCount;
    }
}

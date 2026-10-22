using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Represents list of SyntaxTrivias.
/// </summary>
public sealed partial class SyntaxTriviaList : IReadOnlyList<SyntaxTrivia> {
    internal SyntaxTriviaList() { }

    /// <summary>
    /// Creates a new <see cref="SyntaxTriviaList" /> from an existing token, an underlying node, a position,
    /// and an optional start index.
    /// </summary>
    internal SyntaxTriviaList(SyntaxToken token, GreenNode node, int position, int index = 0) {
        this.token = token;
        this.node = node;
        this.position = position;
        this.index = index;
    }

    /// <summary>
    /// The number of items in the list.
    /// </summary>
    public int Count => node is null ? 0 : (node.isList ? node.slotCount : 1);

    /// <summary>
    /// The token that this list is wrapping.
    /// </summary>
    internal SyntaxToken token { get; }

    /// <summary>
    /// The underlying list node.
    /// </summary>
    internal GreenNode node { get; }

    /// <summary>
    /// The position of this list.
    /// </summary>
    internal int position { get; }

    /// <summary>
    /// What index to start at in relation to the underlying list node.
    /// </summary>
    internal int index { get; }

    /// <summary>
    /// The combined full span of all the children. Should be the same as <see cref="span" />.
    /// </summary>
    internal TextSpan fullSpan => node is null ? null : new TextSpan(position, node.fullWidth);

    /// <summary>
    /// The combined span of all the children. Should be the same as <see cref="fullSpan" />.
    /// </summary>
    internal TextSpan span => node is null
        ? null
        : TextSpan.FromBounds(
            position + node.GetLeadingTriviaWidth(), position + node.fullWidth - node.GetTrailingTriviaWidth()
          );

    /// <summary>
    /// Gets the child trivia at the given index.
    /// </summary>
    public SyntaxTrivia this[int index] {
        get {
            if (node is not null) {
                if (node.isList) {
                    if (unchecked((uint)index < (uint)node.slotCount)) {
                        return new SyntaxTrivia(
                            token, node.GetSlot(index), position + node.GetSlotOffset(index), this.index + index
                        );
                    }
                } else if (index == 0) {
                    return new SyntaxTrivia(token, node, position, this.index);
                }
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public SyntaxTrivia First() {
        if (Any())
            return this[0];

        throw new InvalidOperationException();
    }

    public SyntaxTrivia Last() {
        if (Any())
            return this[Count - 1];

        throw new InvalidOperationException();
    }

    public Reversed Reverse() {
        return new Reversed(this);
    }

    public bool Any() {
        return node is not null;
    }

    internal int IndexOf(SyntaxTrivia triviaInList) {
        for (int i = 0, n = Count; i < n; i++) {
            var trivia = this[i];

            if (trivia == triviaInList)
                return i;
        }

        return -1;
    }

    internal SyntaxTriviaList ReplaceRange(SyntaxTrivia triviaInList, IEnumerable<SyntaxTrivia> newTrivia) {
        var index = IndexOf(triviaInList);

        if (index >= 0 && index < Count) {
            var list = this.ToList();
            list.RemoveAt(index);
            list.InsertRange(index, newTrivia);
            return new SyntaxTriviaList(default, GreenNode.CreateList(list, static n => n.green), 0, 0);
        }

        throw new ArgumentOutOfRangeException(nameof(triviaInList));
    }

    internal SyntaxTriviaList InsertRange(int index, IEnumerable<SyntaxTrivia> trivia) {
        var thisCount = Count;

        if (index < 0 || index > thisCount)
            throw new ArgumentOutOfRangeException(nameof(index));

        ArgumentNullException.ThrowIfNull(trivia);

        if (trivia is ICollection<SyntaxTrivia> triviaCollection && triviaCollection.Count == 0)
            return this;

        var builder = GetBuilder();

        try {
            for (var i = 0; i < index; i++)
                builder.Add(this[i]);

            builder.AddRange(trivia);

            for (var i = index; i < thisCount; i++)
                builder.Add(this[i]);

            return builder.Count == thisCount ? this : builder.ToList();
        } finally {
            ClearAndFreeBuilder(builder);
        }
    }

    private static readonly ObjectPool<SyntaxTriviaListBuilder> BuilderPool =
        new ObjectPool<SyntaxTriviaListBuilder>(() => SyntaxTriviaListBuilder.Create());

    private static SyntaxTriviaListBuilder GetBuilder()
        => BuilderPool.Allocate();

    private static void ClearAndFreeBuilder(SyntaxTriviaListBuilder builder) {
        const int MaxBuilderCount = 16;

        if (builder.Count <= MaxBuilderCount) {
            builder.Clear();
            BuilderPool.Free(builder);
        }
    }

    internal void CopyTo(int offset, SyntaxTrivia[] array, int arrayOffset, int count) {
        if (offset < 0 || count < 0 || Count < offset + count)
            throw new IndexOutOfRangeException();

        if (count == 0)
            return;

        var first = this[offset];
        array[arrayOffset] = first;

        var position = first.position;
        var current = first;

        for (var i = 1; i < count; i++) {
            position += current.fullWidth;
            current = new SyntaxTrivia(token, GetGreenNodeAt(offset + i), position, index + i);

            array[arrayOffset + i] = current;
        }
    }

    private GreenNode GetGreenNodeAt(int i) {
        return GetGreenNodeAt(node, i);
    }

    public Enumerator GetEnumerator() {
        return new Enumerator(this);
    }

    IEnumerator<SyntaxTrivia> IEnumerable<SyntaxTrivia>.GetEnumerator() {
        if (node is null)
            return new EmptyEnumerator<SyntaxTrivia>();

        return new EnumeratorImpl(this);
    }

    IEnumerator IEnumerable.GetEnumerator() {
        if (node is null)
            return new EmptyEnumerator<SyntaxTrivia>();

        return new EnumeratorImpl(this);
    }

    private static GreenNode GetGreenNodeAt(GreenNode node, int i) {
        return node.isList ? node.GetSlot(i) : node;
    }
}

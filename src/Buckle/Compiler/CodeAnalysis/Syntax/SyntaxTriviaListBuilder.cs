using System;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax;

internal class SyntaxTriviaListBuilder {
    private SyntaxTrivia[] _nodes;

    internal SyntaxTriviaListBuilder(int size) {
        _nodes = new SyntaxTrivia[size];
    }

    internal static SyntaxTriviaListBuilder Create() {
        return new SyntaxTriviaListBuilder(4);
    }

    internal static SyntaxTriviaList Create(IEnumerable<SyntaxTrivia> trivia) {
        if (trivia is null)
            return new SyntaxTriviaList();

        var builder = Create();
        builder.AddRange(trivia);
        return builder.ToList();
    }

    internal int Count { get; private set; }

    internal void Clear() {
        Count = 0;
    }

    internal SyntaxTrivia this[int index] {
        get {
            if (index < 0 || index >= Count)
                throw new IndexOutOfRangeException();

            return _nodes[index];
        }
    }

    internal void AddRange(IEnumerable<SyntaxTrivia>? items) {
        if (items is not null) {
            foreach (var item in items)
                Add(item);
        }
    }

    internal SyntaxTriviaListBuilder Add(SyntaxTrivia item) {
        if (Count >= _nodes.Length)
            Grow(Count == 0 ? 8 : _nodes.Length * 2);

        _nodes[Count++] = item;
        return this;
    }

    internal void Add(SyntaxTrivia[] items) {
        Add(items.AsSpan());
    }

    internal void Add(ReadOnlySpan<SyntaxTrivia> items) {
        Add(items, 0, items.Length);
    }

    internal void Add(SyntaxTrivia[] items, int offset, int length) {
        Add(items.AsSpan(), offset, length);
    }

    internal void Add(ReadOnlySpan<SyntaxTrivia> items, int offset, int length) {
        if (Count + length > _nodes.Length)
            Grow(Count + length);

        items.Slice(offset, length).CopyTo(_nodes.AsSpan().Slice(Count, length));
        Count += length;
    }

    internal void Add(in SyntaxTriviaList list) {
        Add(list, 0, list.Count);
    }

    internal void Add(in SyntaxTriviaList list, int offset, int length) {
        if (Count + length > _nodes.Length)
            Grow(Count + length);

        list.CopyTo(offset, _nodes, Count, length);
        Count += length;
    }

    private void Grow(int size) {
        var tmp = new SyntaxTrivia[size];
        Array.Copy(_nodes, tmp, _nodes.Length);
        _nodes = tmp;
    }

    public static implicit operator SyntaxTriviaList(SyntaxTriviaListBuilder builder) {
        return builder.ToList();
    }

    internal SyntaxTriviaList ToList() {
        if (Count > 0) {
            switch (Count) {
                case 1:
                    return new SyntaxTriviaList(default, _nodes[0].green, position: 0, index: 0);
                case 2:
                    return new SyntaxTriviaList(default,
                        InternalSyntax.SyntaxList.List(
                        _nodes[0].green,
                        _nodes[1].green), position: 0, index: 0);
                // TODO Optimization: WithThreeChildren
                // case 3:
                //     return new SyntaxTriviaList(default(SyntaxToken),
                //         InternalSyntax.SyntaxList.List(
                //             _nodes[0].green,
                //             _nodes[1].green,
                //             _nodes[2].green),
                //         position: 0, index: 0);
                default: {
                        var tmp = new ArrayElement<GreenNode>[Count];

                        for (var i = 0; i < Count; i++)
                            tmp[i].value = _nodes[i].green;

                        return new SyntaxTriviaList(
                            default,
                            InternalSyntax.SyntaxList.List(tmp),
                            position: 0,
                            index: 0
                        );
                    }
            }
        } else {
            return default;
        }
    }
}

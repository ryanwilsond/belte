
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// A builder for a <see cref="SyntaxList<T>" />.
/// </summary>
internal sealed class SyntaxListBuilder<T> where T : GreenNode {
    private readonly SyntaxListBuilder _builder;

    /// <summary>
    /// Creates a new <see cref="SyntaxListBuilder<T>" /> with the given starting size.
    /// </summary>
    public SyntaxListBuilder(int size) : this(new SyntaxListBuilder(size)) { }

    /// <summary>
    /// Creates a new <see cref="SyntaxListBuilder<T>" /> from a <see cref="SyntaxListBuilder" />.
    /// </summary>
    internal SyntaxListBuilder(SyntaxListBuilder builder) {
        _builder = builder;
    }

    internal SyntaxListBuilder() { }

    /// <summary>
    /// Creates a <see cref="SyntaxListBuilder<T>" /> with the default starting size of 8.
    /// </summary>
    public static SyntaxListBuilder<T> Create() {
        return new SyntaxListBuilder<T>(8);
    }

    /// <summary>
    /// If the underlying builder is null.
    /// </summary>
    public bool isNull => _builder is null;

    /// <summary>
    /// The number if items currently in the builder.
    /// </summary>
    public int Count => _builder.Count;

    /// <summary>
    /// Gets the item at the given index.
    /// </summary>
    public T this[int index] {
        get {
            var result = _builder[index];
            return (T)result;
        }
        set {
            _builder[index] = value;
        }
    }

    /// <summary>
    /// Clears the builder.
    /// </summary>
    public void Clear() {
        _builder.Clear();
    }

    /// <summary>
    /// Adds a node to the end of the builder.
    /// </summary>
    public SyntaxListBuilder<T> Add(T node) {
        _builder.Add(node);
        return this;
    }

    /// <summary>
    /// Adds a subrange of an array to the end of the builder.
    /// </summary>
    public void AddRange(T[] items, int offset, int length) {
        _builder.AddRange(items, offset, length);
    }

    /// <summary>
    /// Adds a <see cref="SyntaxList<T>" /> to the end of the builder.
    /// </summary>
    public void AddRange(SyntaxList<T> nodes) {
        _builder.AddRange(nodes);
    }

    /// <summary>
    /// Adds a subrange of a <see cref="SyntaxList<T>" /> to the end of the builder.
    /// </summary>
    public void AddRange(SyntaxList<T> nodes, int offset, int length) {
        _builder.AddRange(nodes, offset, length);
    }

    /// <summary>
    /// Converts the builder into a <see cref="SyntaxList<T>" />.
    /// </summary>
    public SyntaxList<T> ToList() {
        return _builder.ToList();
    }

    /// <summary>
    /// Converts the builder into a <see cref="SyntaxList<TDerived>" />.
    /// </summary>
    public SyntaxList<TDerived> ToList<TDerived>() where TDerived : GreenNode {
        return new SyntaxList<TDerived>(ToListNode());
    }

    /// <summary>
    /// Converts the builder into a type of list node depending on the current size.
    /// </summary>
    public GreenNode ToListNode() {
        return _builder.ToListNode();
    }

    public bool Any(SyntaxKind kind) {
        return _builder.Any(kind);
    }

    public static implicit operator SyntaxListBuilder(SyntaxListBuilder<T> builder) {
        return builder._builder;
    }

    public static implicit operator SyntaxList<T>(SyntaxListBuilder<T> builder) {
        if (builder._builder is not null) {
            return builder.ToList();
        }

        return default;
    }
}

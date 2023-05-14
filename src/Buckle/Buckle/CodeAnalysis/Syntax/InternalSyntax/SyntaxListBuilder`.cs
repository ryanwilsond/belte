
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed class SyntaxListBuilder<T> where T : GreenNode {
    private readonly SyntaxListBuilder _builder;

    public SyntaxListBuilder(int size) : this(new SyntaxListBuilder(size)) { }

    public static SyntaxListBuilder<T> Create() {
        return new SyntaxListBuilder<T>(8);
    }

    internal SyntaxListBuilder(SyntaxListBuilder builder) {
        _builder = builder;
    }

    public bool isNull => _builder == null;

    public int count => _builder.count;

    public T this[int index] {
        get {
            var result = _builder[index];
            return (T)result;
        }
        set {
            _builder[index] = value;
        }
    }

    public void Clear() {
        _builder.Clear();
    }

    public SyntaxListBuilder<T> Add(T node) {
        _builder.Add(node);
        return this;
    }

    public void AddRange(T[] items, int offset, int length) {
        _builder.AddRange(items, offset, length);
    }

    public void AddRange(SyntaxList<T> nodes) {
        _builder.AddRange(nodes);
    }

    public void AddRange(SyntaxList<T> nodes, int offset, int length) {
        _builder.AddRange(nodes, offset, length);
    }

    public bool Any(SyntaxKind kind) {
        return _builder.Any(kind);
    }

    public SyntaxList<T> ToList() {
        return _builder.ToList();
    }

    public GreenNode ToListNode() {
        return _builder.ToListNode();
    }

    public static implicit operator SyntaxListBuilder(SyntaxListBuilder<T> builder) {
        return builder._builder;
    }

    public static implicit operator SyntaxList<T>(SyntaxListBuilder<T> builder) {
        if (builder._builder != null) {
            return builder.ToList();
        }

        return default(SyntaxList<T>);
    }

    public SyntaxList<TDerived> ToList<TDerived>() where TDerived : GreenNode {
        return new SyntaxList<TDerived>(ToListNode());
    }
}


namespace Buckle.CodeAnalysis.Syntax;

internal sealed class SyntaxListBuilder<T> where T : SyntaxNode {
    private readonly SyntaxListBuilder _builder;

    internal SyntaxListBuilder(int size) : this(new SyntaxListBuilder(size)) { }

    internal SyntaxListBuilder(SyntaxListBuilder builder) {
        _builder = builder;
    }

    internal static SyntaxListBuilder<T> Create() {
        return new SyntaxListBuilder<T>(8);
    }

    internal bool isNull => _builder is null;

    internal int count => _builder.count;

    internal void Clear() {
        _builder.Clear();
    }

    internal SyntaxListBuilder<T> Add(T node) {
        _builder.Add(node);
        return this;
    }

    internal SyntaxList<T> ToList() {
        return (SyntaxList<T>)_builder.ToList();
    }
}

using System;

namespace Buckle.CodeAnalysis.Syntax;

internal sealed class SeparatedSyntaxListBuilder<T> where T : SyntaxNode {
    private readonly SyntaxListBuilder _builder;
    private bool _expectSeparator;

    internal SeparatedSyntaxListBuilder(int size) : this(new SyntaxListBuilder(size)) { }

    internal SeparatedSyntaxListBuilder(SyntaxListBuilder builder) {
        _builder = builder;
        _expectSeparator = false;
    }

    internal static SeparatedSyntaxListBuilder<T> Create() {
        return new SeparatedSyntaxListBuilder<T>(8);
    }

    internal bool isNull => _builder is null;

    internal int count => _builder.count;

    internal void Clear() {
        _builder.Clear();
    }

    internal SeparatedSyntaxListBuilder<T> Add(T node) {
        CheckExpectedElement();
        _expectSeparator = true;
        _builder.Add(node);
        return this;
    }

    internal SeparatedSyntaxListBuilder<T> AddSeparator(SyntaxToken separatorToken) {
        CheckExpectedSeparator();
        _expectSeparator = false;
        _builder.AddInternal(separatorToken.node);
        return this;
    }

    internal SeparatedSyntaxList<T> ToList() {
        if (_builder is null)
            return null;

        return _builder.ToSeparatedList<T>();
    }

    private void CheckExpectedElement() {
        if (_expectSeparator)
            throw new InvalidOperationException("Expected separator");
    }

    private void CheckExpectedSeparator() {
        if (!_expectSeparator)
            throw new InvalidOperationException("Expected element");
    }
}

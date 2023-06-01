using System.Text;

namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// Implementation of <see cref="SourceTextWriter" /> that writes to a <see cref="StringText" />.
/// </summary>
internal sealed class StringTextWriter : SourceTextWriter {
    private readonly StringBuilder _builder;

    /// <summary>
    /// Creates a new <see cref="StringTextWriter" /> with a starting capacity.
    /// </summary>
    internal StringTextWriter(int capacity) {
        _builder = new StringBuilder(capacity);
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value) {
        _builder.Append(value);
    }

    public override void Write(string? value) {
        _builder.Append(value);
    }

    public override void Write(char[] buffer, int index, int count) {
        _builder.Append(buffer, index, count);
    }

    internal override SourceText ToSourceText() {
        return new StringText(null, _builder.ToString());
    }
}

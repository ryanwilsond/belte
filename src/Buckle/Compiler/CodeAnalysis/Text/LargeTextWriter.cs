using System;
using System.Text;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// Implementation of <see cref="SourceTextWriter" /> for large texts, writes to a <see cref="LargeText" />.
/// </summary>
internal sealed class LargeTextWriter : SourceTextWriter {
    private readonly ArrayBuilder<char[]> _chunks;
    private readonly int _bufferSize;

    private char[]? _buffer;
    private int _currentUsed;

    /// <summary>
    /// Creates a new <see cref="LargeTextWriter" /> with a starting capacity.
    /// </summary>
    internal LargeTextWriter(int length) {
        _chunks = ArrayBuilder<char[]>.GetInstance(1 + length / LargeText.ChunkSize);
        _bufferSize = Math.Min(LargeText.ChunkSize, length);
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value) {
        if (_buffer is not null && _currentUsed < _buffer.Length) {
            _buffer[_currentUsed] = value;
            _currentUsed++;
        } else {
            Write(new char[] { value }, 0, 1);
        }
    }

    public override void Write(string? value) {
        if (value is null)
            return;

        var count = value.Length;
        var index = 0;

        while (count > 0) {
            EnsureBuffer();

            var remaining = _buffer.Length - _currentUsed;
            var copy = Math.Min(remaining, count);

            value.CopyTo(index, _buffer, _currentUsed, copy);

            _currentUsed += copy;
            index += copy;
            count -= copy;

            if (_currentUsed == _buffer.Length)
                Flush();
        }
    }

    public override void Write(char[] chars, int index, int count) {
        while (count > 0) {
            EnsureBuffer();

            var remaining = _buffer!.Length - _currentUsed;
            var copy = Math.Min(remaining, count);

            Array.Copy(chars, index, _buffer, _currentUsed, copy);
            _currentUsed += copy;
            index += copy;
            count -= copy;

            if (_currentUsed == _buffer.Length)
                Flush();
        }
    }

    public override void Flush() {
        if (_buffer is not null && _currentUsed > 0) {
            if (_currentUsed < _buffer.Length)
                Array.Resize(ref _buffer, _currentUsed);

            _chunks.Add(_buffer);
            _buffer = null;
            _currentUsed = 0;
        }
    }

    internal override SourceText ToSourceText() {
        Flush();
        return new LargeText(_chunks.ToImmutableAndFree());
    }

    /// <summary>
    /// Adds a chunk to the end of the text.
    /// </summary>
    internal void AppendChunk(char[] chunk) {
        if (CanFitInAllocatedBuffer(chunk.Length)) {
            Write(chunk, 0, chunk.Length);
        } else {
            Flush();
            _chunks.Add(chunk);
        }
    }

    /// <summary>
    /// If the buffer has enough room for the given number of chars.
    /// </summary>
    internal bool CanFitInAllocatedBuffer(int chars) {
        return _buffer is not null && chars <= (_buffer.Length - _currentUsed);
    }

    private void EnsureBuffer() {
        if (_buffer is null)
            _buffer = new char[_bufferSize];
    }
}

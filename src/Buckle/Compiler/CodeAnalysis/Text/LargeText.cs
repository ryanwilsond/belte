using System;
using System.Collections.Immutable;
using System.IO;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// Implementation of <see cref="SourceText" /> that is optimized for large sources.
/// </summary>
internal sealed class LargeText : SourceText {
    internal const int ChunkSize = LargeObjectHeapLimitInChars;

    private readonly ImmutableArray<char[]> _chunks;
    private readonly int[] _chunkStartOffsets;
    private readonly int _length;

    /// <summary>
    /// Creates a <see cref="LargeText" /> provided the file name and contents.
    /// </summary>
    /// <param name="fileName">File name of the <see cref="LargeText" /> (where the text came from).</param>
    /// <param name="text">The contents of the file the <see cref="LargeText" /> comes from.</param>
    internal LargeText(ImmutableArray<char[]> chunks) {
        _chunks = chunks;
        _chunkStartOffsets = new int[chunks.Length];

        var offset = 0;
        for (var i = 0; i < chunks.Length; i++) {
            _chunkStartOffsets[i] = offset;
            offset += chunks[i].Length;
        }

        _length = offset;
    }

    public override int length => _length;

    public override char this[int index] {
        get {
            var i = GetIndexFromPosition(index);
            return _chunks[i][index - _chunkStartOffsets[i]];
        }
    }

    public override void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count) {
        if (count == 0)
            return;

        var chunkIndex = GetIndexFromPosition(sourceIndex);
        var chunkStartOffset = sourceIndex - _chunkStartOffsets[chunkIndex];

        while (true) {
            var chunk = _chunks[chunkIndex];
            var charsToCopy = Math.Min(chunk.Length - chunkStartOffset, count);
            Array.Copy(chunk, chunkStartOffset, destination, destinationIndex, charsToCopy);
            count -= charsToCopy;

            if (count <= 0)
                break;

            destinationIndex += charsToCopy;
            chunkStartOffset = 0;
            chunkIndex++;
        }
    }

    public override void Write(TextWriter writer) {
        var count = length;

        if (length == 0)
            return;

        var chunkIndex = GetIndexFromPosition(0);
        var chunkStartOffset = 0 - _chunkStartOffsets[chunkIndex];

        while (true) {
            var chunk = _chunks[chunkIndex];
            var charsToWrite = Math.Min(chunk.Length - chunkStartOffset, count);

            if (writer is LargeTextWriter chunkWriter && chunkStartOffset == 0 && charsToWrite == chunk.Length)
                chunkWriter.AppendChunk(chunk);
            else
                writer.Write(chunk, chunkStartOffset, charsToWrite);

            count -= charsToWrite;

            if (count <= 0)
                break;

            chunkStartOffset = 0;
            chunkIndex++;
        }
    }

    private protected override void EnsureLines() {
        throw ExceptionUtilities.Unreachable();
    }

    private int GetIndexFromPosition(int position) {
        var index = _chunkStartOffsets.BinarySearch(position);
        return index >= 0 ? index : (~index - 1);
    }
}

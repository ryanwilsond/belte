using System;
using System.Collections.Immutable;
using System.IO;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// Implementation of <see cref="SourceText" /> that is optimized for large sources.
/// </summary>
internal sealed class LargeText : SourceText {
    internal const int ChunkSize = SourceText.LargeObjectHeapLimitInChars;

    private readonly ImmutableArray<char[]> _chunks;
    private readonly int[] _chunkStartOffsets;
    private readonly int _length;

    internal LargeText(ImmutableArray<char[]> chunks) {
        _chunks = chunks;
        _chunkStartOffsets = new int[chunks.Length];

        int offset = 0;
        for (int i = 0; i < chunks.Length; i++) {
            _chunkStartOffsets[i] = offset;
            offset += chunks[i].Length;
        }

        _length = offset;
    }

    public override int length => _length;

    public override char this[int index] {
        get {
            int i = GetIndexFromPosition(index);
            return _chunks[i][index - _chunkStartOffsets[i]];
        }
    }

    public override void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count) {
        if (count == 0)
            return;

        int chunkIndex = GetIndexFromPosition(sourceIndex);
        int chunkStartOffset = sourceIndex - _chunkStartOffsets[chunkIndex];

        while (true) {
            var chunk = _chunks[chunkIndex];
            int charsToCopy = Math.Min(chunk.Length - chunkStartOffset, count);
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
        int count = length;

        if (length == 0)
            return;

        var chunkWriter = writer as LargeTextWriter;
        int chunkIndex = GetIndexFromPosition(0);
        int chunkStartOffset = 0 - _chunkStartOffsets[chunkIndex];

        while (true) {
            var chunk = _chunks[chunkIndex];
            int charsToWrite = Math.Min(chunk.Length - chunkStartOffset, count);

            if (chunkWriter != null && chunkStartOffset == 0 && charsToWrite == chunk.Length)
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

    protected override void EnsureLines() {
        throw ExceptionUtilities.Unreachable();
    }

    private int GetIndexFromPosition(int position) {
        int index = _chunkStartOffsets.BinarySearch(position);
        return index >= 0 ? index : (~index - 1);
    }
}

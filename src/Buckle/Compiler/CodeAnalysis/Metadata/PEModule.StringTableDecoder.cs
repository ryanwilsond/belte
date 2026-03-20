using System;
using System.Reflection.Metadata;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

internal sealed partial class PEModule {
    private sealed class StringTableDecoder : MetadataStringDecoder {
        public static readonly StringTableDecoder Instance = new StringTableDecoder();

        private StringTableDecoder() : base(System.Text.Encoding.UTF8) { }

        public override unsafe string GetString(byte* bytes, int byteCount) {
            return StringTable.AddSharedUtf8(new ReadOnlySpan<byte>(bytes, byteCount));
        }
    }
}

using System.Diagnostics;
using System.IO;

namespace Buckle.CodeAnalysis;

internal static class BinaryReaderExtensions {
    internal static bool ReadBooleanSafe(this BinaryReader reader) {
        var @byte = reader.ReadByte();
        Debug.Assert(@byte == 1 || @byte == 0);
        return @byte != 0;
    }
}

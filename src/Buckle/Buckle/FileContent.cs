using System.Collections.Generic;

namespace Buckle;

/// <summary>
/// Contents of a file either represented as text or bytes.
/// </summary>
public struct FileContent {
    /// <summary>
    /// Text representation of file.
    /// </summary>
    public string text;

    /// <summary>
    /// Byte representation of file (usually only used with .o or .exe files).
    /// </summary>
    public List<byte> bytes;
}

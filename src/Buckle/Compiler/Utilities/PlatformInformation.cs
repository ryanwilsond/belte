using System.IO;

namespace Buckle.Utilities;

internal static class PlatformInformation {
    internal static bool IsWindows => Path.DirectorySeparatorChar == '\\';
    internal static bool IsUnix => Path.DirectorySeparatorChar == '/';
}

using System.IO;

namespace Buckle.Utilities;

internal static class PathUtilities {
    internal static char DirectorySeparatorChar => Path.DirectorySeparatorChar;
    internal const char AltDirectorySeparatorChar = '/';
    internal const char VolumeSeparatorChar = ':';

    internal static bool IsDirectorySeparator(char c) => c == DirectorySeparatorChar || c == AltDirectorySeparatorChar;

    internal static bool IsAbsolute(string path) {
        if (string.IsNullOrEmpty(path))
            return false;

        if (PlatformInformation.IsUnix)
            return path[0] == DirectorySeparatorChar;

        if (IsDriveRootedAbsolutePath(path))
            return true;

        return path.Length >= 2 &&
            IsDirectorySeparator(path[0]) &&
            IsDirectorySeparator(path[1]);
    }

    private static bool IsDriveRootedAbsolutePath(string path) {
        return path.Length >= 3 && path[1] == VolumeSeparatorChar && IsDirectorySeparator(path[2]);
    }
}

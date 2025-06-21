using System;

namespace Buckle.Utilities;

internal static class VersionHelper {
    public static Version GenerateVersionFromPatternAndCurrentTime(DateTime time, Version pattern) {
        if (pattern is null || pattern.Revision != ushort.MaxValue)
            return pattern;

        if (time == default)
            time = DateTime.Now;

        var revision = (int)time.TimeOfDay.TotalSeconds / 2;

        if (pattern.Build == ushort.MaxValue) {
            var days = time.Date - new DateTime(2000, 1, 1);
            var build = Math.Min(ushort.MaxValue, (int)days.TotalDays);
            return new Version(pattern.Major, pattern.Minor, (ushort)build, (ushort)revision);
        } else {
            return new Version(pattern.Major, pattern.Minor, pattern.Build, (ushort)revision);
        }
    }
}

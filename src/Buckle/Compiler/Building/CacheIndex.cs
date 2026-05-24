using System.Collections.Generic;

namespace Buckle.Building;

public class CacheIndex {
    public Dictionary<string, CacheIndexEntry> entries { get; set; } = [];

    public long totalSizeBytes { get; set; }
}

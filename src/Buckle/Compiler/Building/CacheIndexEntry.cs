
namespace Buckle.Building;

public class CacheIndexEntry {
    public string path { get; set; }

    public long lastAccess { get; set; }

    public long sizeBytes { get; set; }
}

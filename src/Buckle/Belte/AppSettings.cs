
namespace Belte;

/// <summary>
/// All information from the App.config.
/// </summary>
public struct AppSettings {
    /// <summary>
    /// Path to where the program is running from.
    /// </summary>
    public string executingPath;

    /// <summary>
    /// Extra search path for assemblies (DLLs).
    /// </summary>
    public string probingPath;

    /// <summary>
    /// Path to any program resources.
    /// </summary>
    public string resourcesPath;
}

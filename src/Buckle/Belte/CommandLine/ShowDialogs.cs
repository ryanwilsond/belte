
namespace Belte.CommandLine;

/// <summary>
/// Flags that tell the <see cref="BuckleCommandLine" /> what dialogs to display.
/// </summary>
public struct ShowDialogs {
    /// <summary>
    /// Display help dialog.
    /// </summary>
    public bool help;

    /// <summary>
    /// Display machine information dialog.
    /// </summary>
    public bool machine;

    /// <summary>
    /// Display compiler version information dialog.
    /// </summary>
    public bool version;

    /// <summary>
    /// Display error help.
    /// </summary>
    public string error;
}

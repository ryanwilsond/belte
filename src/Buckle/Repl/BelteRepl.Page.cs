
namespace Repl;

public sealed partial class BelteRepl {
    /// <summary>
    /// Indicated to the state what page is being displayed to the user.
    /// </summary>
    internal enum Page : byte {
        Repl,
        Settings,
    }
}

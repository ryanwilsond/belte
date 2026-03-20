using System;

namespace Repl.Themes;

/// <summary>
/// Purpura theme. Mostly dark colors with magenta and blue accents.
/// </summary>
internal sealed class PurpuraTheme : DarkTheme {
    internal override ConsoleColor textDefault => ConsoleColor.Black;
    internal override ConsoleColor result => ConsoleColor.Gray;
    internal override ConsoleColor background => ConsoleColor.DarkMagenta;
    internal override ConsoleColor literal => ConsoleColor.DarkCyan;
    internal override ConsoleColor @string => ConsoleColor.Cyan;
    internal override ConsoleColor keyword => ConsoleColor.Blue;
    internal override ConsoleColor typeName => ConsoleColor.Red;
    internal override ConsoleColor errorText => ConsoleColor.Gray;
}

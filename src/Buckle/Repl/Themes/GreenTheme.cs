using System;

namespace Repl.Themes;

/// <summary>
/// Green theme. Mostly dark colors with green background.
/// </summary>
internal sealed class GreenTheme : DarkTheme {
    internal override ConsoleColor textDefault => ConsoleColor.Black;
    internal override ConsoleColor result => ConsoleColor.DarkGreen;
    internal override ConsoleColor background => ConsoleColor.Green;
    internal override ConsoleColor literal => ConsoleColor.DarkCyan;
    internal override ConsoleColor @string => ConsoleColor.DarkMagenta;
    internal override ConsoleColor keyword => ConsoleColor.DarkBlue;
    internal override ConsoleColor typeName => ConsoleColor.Red;
    internal override ConsoleColor errorText => ConsoleColor.Gray;
}

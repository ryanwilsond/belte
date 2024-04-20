using System;

namespace Repl.Themes;

/// <summary>
/// Blue theme. Mostly dark colors with blue background.
/// </summary>
internal sealed class BlueTheme : DarkTheme {
    internal override ConsoleColor textDefault => ConsoleColor.Black;
    internal override ConsoleColor result => ConsoleColor.DarkGreen;
    internal override ConsoleColor background => ConsoleColor.Blue;
    internal override ConsoleColor number => ConsoleColor.DarkCyan;
    internal override ConsoleColor @string => ConsoleColor.DarkMagenta;
    internal override ConsoleColor keyword => ConsoleColor.DarkBlue;
    internal override ConsoleColor typeName => ConsoleColor.Red;
    internal override ConsoleColor errorText => ConsoleColor.Gray;
}

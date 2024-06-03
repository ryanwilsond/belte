using System;

namespace Repl.Themes;

/// <summary>
/// Traffic stop theme. Mostly yellow, green, and red colors.
/// </summary>
internal sealed class TrafficStopTheme : LightTheme {
    internal override ConsoleColor result => ConsoleColor.DarkGreen;
    internal override ConsoleColor text => ConsoleColor.Yellow;
    internal override ConsoleColor textDefault => ConsoleColor.Yellow;
    internal override ConsoleColor typeName => ConsoleColor.Red;
    internal override ConsoleColor errorText => ConsoleColor.Red;
    internal override ConsoleColor identifier => ConsoleColor.Green;
    internal override ConsoleColor literal => ConsoleColor.DarkCyan;
    internal override ConsoleColor @string => ConsoleColor.DarkBlue;
    internal override ConsoleColor keyword => ConsoleColor.DarkRed;
    internal override ConsoleColor blueNode => ConsoleColor.DarkYellow;
}

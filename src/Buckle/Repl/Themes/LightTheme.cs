using System;

namespace Repl.Themes;

/// <summary>
/// Light theme. Mostly bright colors and pairs well with light themed terminals.
/// </summary>
internal class LightTheme : ColorTheme {
    internal override ConsoleColor @default => ConsoleColor.DarkGray;
    internal override ConsoleColor selection => ConsoleColor.DarkGray;
    internal override ConsoleColor textDefault => ConsoleColor.Black;
    internal override ConsoleColor result => ConsoleColor.Black;
    internal override ConsoleColor background => ConsoleColor.White;
    internal override ConsoleColor identifier => ConsoleColor.Black;
    internal override ConsoleColor literal => ConsoleColor.DarkCyan;
    internal override ConsoleColor @string => ConsoleColor.DarkYellow;
    internal override ConsoleColor comment => ConsoleColor.DarkGray;
    internal override ConsoleColor keyword => ConsoleColor.DarkBlue;
    internal override ConsoleColor typeName => ConsoleColor.DarkBlue;
    internal override ConsoleColor text => ConsoleColor.DarkGray;
    internal override ConsoleColor escape => ConsoleColor.DarkCyan;
    internal override ConsoleColor errorText => ConsoleColor.Black;
    internal override ConsoleColor redNode => ConsoleColor.Red;
    internal override ConsoleColor greenNode => ConsoleColor.Green;
    internal override ConsoleColor blueNode => ConsoleColor.Blue;
}

using System;

namespace Repl.Themes;

/// <summary>
/// Dark theme (default). Mostly dark colors and pairs well with dark themed terminals.
/// </summary>
internal class DarkTheme : ColorTheme {
    internal override ConsoleColor @default => ConsoleColor.DarkGray;
    internal override ConsoleColor selection => ConsoleColor.DarkGray;
    internal override ConsoleColor textDefault => ConsoleColor.White;
    internal override ConsoleColor result => ConsoleColor.White;
    internal override ConsoleColor background => ConsoleColor.Black;
    internal override ConsoleColor identifier => ConsoleColor.White;
    internal override ConsoleColor literal => ConsoleColor.Cyan;
    internal override ConsoleColor @string => ConsoleColor.Yellow;
    internal override ConsoleColor comment => ConsoleColor.DarkGray;
    internal override ConsoleColor keyword => ConsoleColor.Blue;
    internal override ConsoleColor typeName => ConsoleColor.Blue;
    internal override ConsoleColor text => ConsoleColor.DarkGray;
    internal override ConsoleColor escape => ConsoleColor.Cyan;
    internal override ConsoleColor errorText => ConsoleColor.White;
    internal override ConsoleColor redNode => ConsoleColor.Red;
    internal override ConsoleColor greenNode => ConsoleColor.Green;
    internal override ConsoleColor blueNode => ConsoleColor.Blue;
}

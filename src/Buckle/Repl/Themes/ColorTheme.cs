using System;

namespace Repl.Themes;

/// <summary>
/// All required fields to implement for a Repl color theme (only supported if using System.Console as out).
/// </summary>
internal abstract class ColorTheme {
    /// <summary>
    /// Default color to result to for unformatted text.
    /// </summary>
    internal abstract ConsoleColor @default { get; }

    /// <summary>
    /// Background color to indicate selected text.
    /// </summary>
    internal abstract ConsoleColor selection { get; }

    /// <summary>
    /// Default color for text with no special color.
    /// </summary>
    internal abstract ConsoleColor textDefault { get; }

    /// <summary>
    /// Color of all results.
    /// </summary>
    internal abstract ConsoleColor result { get; }

    /// <summary>
    /// Background color of terminal.
    /// </summary>
    internal abstract ConsoleColor background { get; }

    /// <summary>
    /// Color of identifer tokens.
    /// </summary>
    internal abstract ConsoleColor identifier { get; }

    /// <summary>
    /// Color of number literals.
    /// </summary>
    internal abstract ConsoleColor number { get; }

    /// <summary>
    /// Color of string literals.
    /// </summary>
    internal abstract ConsoleColor @string { get; }

    /// <summary>
    /// Color of comments (all types).
    /// </summary>
    internal abstract ConsoleColor comment { get; }

    /// <summary>
    /// Color of keywords.
    /// </summary>
    internal abstract ConsoleColor keyword { get; }

    /// <summary>
    /// Color of type names (not full type clauses).
    /// </summary>
    internal abstract ConsoleColor typeName { get; }

    /// <summary>
    /// Color any other code text.
    /// </summary>
    internal abstract ConsoleColor text { get; }

    /// <summary>
    /// Color of a string escape sequence.
    /// </summary>
    internal abstract ConsoleColor escape { get; }

    /// <summary>
    /// Color of code text that could not parse.
    /// </summary>
    internal abstract ConsoleColor errorText { get; }

    /// <summary>
    /// Color of red Nodes.
    /// </summary>
    internal abstract ConsoleColor redNode { get; }

    /// <summary>
    /// Color of green Nodes.
    /// </summary>
    internal abstract ConsoleColor greenNode { get; }

    /// <summary>
    /// Color of blue Nodes.
    /// </summary>
    internal abstract ConsoleColor blueNode { get; }
}

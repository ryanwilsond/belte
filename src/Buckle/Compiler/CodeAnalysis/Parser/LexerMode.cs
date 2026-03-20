
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// What type of source the Lexer is currently lexing.
/// </summary>
internal enum LexerMode : byte {
    Syntax,
    Directive,
}

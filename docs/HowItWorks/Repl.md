# Repl

The repl is split into two parts, the direct console handling, and the language handling. The console handling has an
internal representation of all the text, and manually updates and renders the text whenever a key is pressed. This is
how the repl is able to add special functionality like page-up and page-down browsing previous submissions. This code
is unimportant to the actual compiler, so it wont be talked about much. There are many tutorials on producing similar
functionality that are unrelated to compilers.

The language handling parses the input text each key press to allow simple highlighting. To produce unique colors it
checks each produced node's type and gives as color accordingly. To allow multiline submissions, the enter key only
evaluates a submission if there are 1) no errors, or 2) the enter key has been pressed twice in a row before. This
allows the user to force a invalid submission to view lexer and parser errors messages.

There are some repl specific zulu commands mainly for debugging. You can view a full list with descriptions by typing
`#help` in the repl and pressing enter. A couple important ones are `#showTree` and `#showProgram` which show the
generated parse tree (concrete syntax tree) and bound program (abstract syntax tree, syntax tree, or bound syntax tree)
respectively. The utility of these functions only lie in debugging, or to quell a user's curiosity.

### Mentioned Components

-> [Lexer](Lexer.md)

-> [Parser](Parser.md)

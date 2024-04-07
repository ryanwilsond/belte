---
layout: post
title: Repl
---

# Repl

The Repl is split into two parts, direct console handling, and language handling. The console handling has an
internal representation of all the text, and manually updates and renders the text whenever a key is pressed. This is
how the Repl can add special functionality like page-up and page-down browsing previous submissions. This code
is unimportant to the actual compiler, so it will not be talked about much. There are many tutorials on producing
similar functionality that is unrelated to compilers.

The language handling parses the input text with each keypress to allow simple highlighting. To produce unique colors
it checks each produced node's type and gives it a color accordingly. To allow multiline submissions, the enter key only
evaluates a submission if there are 1) no errors, or 2) the enter key has been pressed twice in a row before. This
allows the user to force an invalid submission to view Lexer and Parser error messages.

There are some Repl-specific "Zulu" commands mainly for debugging. You can view a full list with descriptions by typing
`#help` in the Repl and pressing enter. A couple of important ones are `#showTree` and `#showProgram` which show the
generated parse tree (concrete syntax tree / abstract syntax tree) and bound program (bound syntax tree) respectively.
The utility of these functions only lies in debugging or quelling a user's curiosity.

### Mentioned Components

-> [Lexer](Lexer.md)

-> [Parser](Parser.md)

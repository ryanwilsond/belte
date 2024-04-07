---
layout: post
title: Parser
---

# Parser

The parser converts a list of syntax tokens into a parse tree that represents the source file in a simpler way that
makes processing it a lot easier and more robust.

Parsers work by attempting to parse the tokens (produced by the lexer) into various expressions and statements, and then
if it fails, send it to the next check. This creates a priority system where certain expressions and statements get
parsing priority over others. For example `true || false && false` would evaluate to `false` if the OR token took
priority over the AND token. In reality AND has a higher priority so this expression evaluates to `true` instead.

Checking for method and variable declarations is just a matter of peeking ahead to see if the correct tokens are
there and if not pass the tokens down the priority ladder.

After creating a parse tree, control passes onto the Binder which does most of the heavy lifting including type
checking.

### Mentioned Components

-> [Lexer](Lexer.md)

-> [Binder](Binder.md)

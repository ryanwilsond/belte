---
layout: post
title: Lexer
---

# Lexer

The Lexer is the simplest component in the compiler, as it creates a 1:1 mapping from text to syntax tokens. No complex
logic takes place. The most interesting part is how comments and whitespace are handled. They are not syntax tokens, but
rather syntax trivia to the surrounding tokens. Each token has its leading and trailing trivia to organized comments
and whitespace to the most relevant token.

Apart from trivia, the only other challenge when making a Lexer is handling line endings. The source text handles the
three types of line endings (LF, CRLF, and CR). After handling, this problem does not come up in any other parts of the
compiler, but it can create a few off-by-one errors while trying to tackle it.

After lexing, the controlling syntax tree hands off the token list to the Parser.

### Mentioned Components

-> [Parser](Parser.md)

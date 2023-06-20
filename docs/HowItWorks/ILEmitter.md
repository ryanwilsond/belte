---
layout: post
title: IL Emitter
---

# IL Emitter

The emitter is a tricky component to implement, but in theory, is not complicated. Most of the logic is straightforward.
The way it works is by initializing an assembly, then adding IL statements by going through the code statement by
statement, similar to the Evaluator. Because all control of flow is simplified into gotos, this method works without
error.

The only hiccup is null, which has some logic to it. Even though there is only one meaning of null, there are different
ways to handle it in IL, for different circumstances. The differences are subtle (and boring), so the main takeaway is
to lay out the emitter in a clear way to handle all these differences from the beginning. Otherwise, some spaghetti code
may be the consequence. Because all errors are checked, the code is flattened, and the methods abstracted, no actual
code processing logic happens, just IL-specific changes.

The only exception is try-catch-finally. In IL you can either have a catch or finally block, so some doubling up on
try-catch blocks is required. However, that is still not a hard task to accomplish.

After emitting you have a .NET executable that you can run anywhere.

### Mentioned Components

-> [Evaluator](Evaluator.md)

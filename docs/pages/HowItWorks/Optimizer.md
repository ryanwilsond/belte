---
layout: post
title: Optimizer
---

# Optimizer

Happens directly after lowering, and some small optimizations occur. This includes dead code path removal using a CFG.
Other small optimizations are simplifying some expressions with constant values, for example, ternary conditional
operations are removed if the first operand is always true or false (e.g. `true ? 5 : 3` -> `5`).

After that, if not going back to the Binder, the code is finally sent to either an emitter or the Evaluator.

### Mentioned Components

-> [IL Emitter](ILEmitter.md)

-> [C# Emitter](CSharpEmitter.md)

-> [Evaluator](Evaluator.md)

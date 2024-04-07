---
layout: post
title: Expander
---

# Expander

Some expressions can get 'corrupted' during lowering. Compound assignment expressions get messed up as when checking
for null, an assignment operation might happen twice instead of once (once while checking if it is null, the second
time when performing the assignment). To prevent this, expansion occurs before lowering to separate these
expressions into multiple expression statements. For example `a += b += c;` expands into `b += c; a += b;`.

The Expander does this by walking through every statement and expression to see if it should be expanded. If so, it
collects any produced statements and inserts them in place of the original statement or expression.

After expansion, the code goes directly to the Lowerer.

### Mentioned Components

-> [Lowerer](Lowerer.md)

---
layout: post
title: Lowerer
---

# Lowerer

The lowerer simplifies the output from the binder to make it easier to emit.

Overall the lowering step is pretty simple. It converts compound assignments, prefix operations, and postfix operations
into their more verbose counterparts and converts all control of flow into a series of goto statements to labels. After
that, the code is flattened into a single scope. This is because there are only one layer deep scopes in IL.

A new addition to the Lowerer is making expressions with `null` work. In IL, you cannot perform basic operations such as
addition or subtraction with nullable values. Thus every expression working with null must be rewritten to first check
for null and then cast everything into their non-nullable counterparts to perform the actual operation.

After lowering the code is sent to the Optimizer.

### Mentioned Components

-> [Optimizer](Optimizer.md)

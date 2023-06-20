---
layout: post
title: Evaluator
---

# Evaluator

The evaluator is extremely simple. Instead of compiling the code into an executable, the code instead gets interpreted
by the evaluator. This is primarily used for testing small files, or to get immediate results for the Repl. To do this
it runs the lowered code statement by statement, tracking the results. This method is more exception prone, and much
slower to run. A common example of an interpreted language is Python.

To handle structure and reference types, the objects being passed around the Evaluator are custom objects that have many
flags indicating if the object is a reference, if it is an explicit reference expression, if it is a constant reference,
etc. It also has a dictionary of stored members for structures, and finally, a single object to store by-value variables
and literals.

### Mentioned Components

-> [Repl](Repl.md)

-> [Lowerer](Lowerer.md)

# Evaluator

The evaluator is extremely simple. Instead of compiling the code into an executable, the code instead gets interpreted
by the evaluator. This is primarily used for testing small files, or to get immediate results for the REPL. To do this
it runs the lowered code statement by statement, tracking the results. This method is more exception prone, and much
slower to run. A common example of an interpreted language is Python.

### Mentioned Components

-> [REPL](Repl.md)

-> [Lowerer](Lowerer.md)

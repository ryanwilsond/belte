# Lowerer

The lowerer simplifies the output from the binder to make it easier to emit. Some optimizations also happen here.

Overall the lowering step is pretty simple. It converts compound assignment operators into their more verbose
counterparts, and converts all control of flow into a series of goto statements to labels. After that the code is
flattened into a single scope. This is because there is only one layer deep scopes in IL. CFG optimization also happens
hear.

After that, if not going back to the binder, the code is finally sent to either the emitter or evaluator.

### Mentioned Components

-> [Emitter](ILEmitter.md)

-> [Evaluator](Evaluator.md)

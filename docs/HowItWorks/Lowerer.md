# Lowerer

The lowerer simplifies the output from the binder to make it easier to emit. Some optimizations also happen here.

Overall the lowering step is pretty simple. It converts compound assignment operators into their more verbose
counterparts and converts all control of flow into a series of goto statements to labels. After that, the code is
flattened into a single scope. This is because there are only one layer deep scopes in IL. CFG optimization also happens
here.

A new addition to the Lowerer is making expressions with `null` work. In IL, you cannot perform basic operations such as
addition or subtraction with nullable values. Thus every expression working with null must be rewritten to first check
for null and then cast everything into their non-nullable counterparts to perform the actual operation.

After that, if not going back to the Binder, the code is finally sent to either the Emitter or Evaluator.

### Mentioned Components

-> [Emitter](ILEmitter.md)

-> [Evaluator](Evaluator.md)

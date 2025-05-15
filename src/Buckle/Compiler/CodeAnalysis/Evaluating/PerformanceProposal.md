# Evaluator Performance Proposal

This document highlights the performance losses with the Evaluator and how they could potentially be addressed.

Because of the dominance of the REPL as a maintained feature, the Evaluator has been elevated to first-class, while the
proper emitters are now secondary. Because of this, it has become a priority to improve the Evaluator's performance.

## Slot Accessions

Currently, the `EvaluatorObject`s store a dictionary mapping `Symbol`s to `EvaluatorObject`s. This acts as a way for
complex objects to store their own field data. This means that when accessing that data when evaluating a
`BoundFieldAccessExpression`, a dictionary lookup is made. This could be optimized to an array lookup.

Because when the object is constructed it's layout is fixed, it can be a fixed-length array.

Proposal: During Lowering, replace all `BoundFieldAccessExpression`s with `BoundSlotAccessExpression`s. During Lowering,
perform a class layout analysis and map all fields to slots (it doesn't matter where this map is stored). Using this
map, perform a series of lookups to replace the field accessions with slot accessions. After this, the map is no longer
used. During Evaluation, the slot accessions now can perform array lookups to get field data. Because all of the slots
are calculated during lowering, no more dictionary lookups are performed using the slot map (it can be disposed).

## Replacing EvaluatorObjects

`EvaluatorObject`s are expensive as every local allocates it's own, and even primitive data creates them. To optimize
primitive arithmetic, locals could instead be mapped to `EvaluatorCell`s, which are wrappers around a struct
`EvaluatedValue`. For RValue evaluations, only the `EvaluatedValue` struct is used. This is much cheaper on the heap
and garbage collector. These data types would look like:

```cs
struct EvaluatedValue {
  ValueKind kind;
  long intValue;
  double decimalValue;
  bool boolValue;
  object objectValue;
  EvaluatorObject[] members;
  TypeSymbol type;
}

class EvaluatorCell {
  EvaluatedValue value;
}

struct EvaluatorObject {
  bool isReference;
  EvaluatedValue value;
  EvaluatorCell cell;
}
```

Locals would all be this new `EvaluatorObject`. If they are primitives, they just store value and `isReference` is
`false`. Otherwise, the value is stored in an `EvaluatorCell` object which means that the value is references when
passed instead of copied. This is how all object types will behave. This means on all primitive arithmetic, values
are passed in light-weight structs and are cheaper. Using `ValueKind` instead of checking the `type` and casting is
also slightly more performant. The `type` would only be used in certain `is` and `as` operations. The `objectValue`
field would store strings or other data (`Evaluator` specific data such as textures and fonts).

Additionally, an object bool could be created for `EvaluatorCell`s, as they are easy to reuse storing only one field.

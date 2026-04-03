# 1 Overview

The Belte language is syntactically very similar to C#; Belte is a "C-Style" language that supports both object-oriented
and procedural paradigms.

Currently, the Belte compiler, Buckle, supports interpretation and execution with .NET integration.
You can read more about using the compiler [here](../Buckle.md).

- [1.1](#11-supported-features) Supported Features
- [1.2](#12-partially-supported-features) Partially Supported Features

## 1.1 Supported Features

The following list gives an exceptionally brief overview of what the language is currently capable of.

- Multi-file compilations
- Functions/methods
- Basic data types and arrays
- Structs, classes, and enums
- Inheritance, templating, and operator overloading
- A simple Standard Library
  - Which includes complex types such as List and Dictionary
- Lowlevel features meant for interop (pointers, dll imports, etc.)

## 1.2 Partially Supported Features

Some features are not supported across all endpoints for various reasons.

The following list describes all of the features where full parity is not currently implemented or was not always
implemented.

- Evaluator: the internal interpreter endpoint. Used for the [REPL](../Repl.md), `--evaluate` builds, and [compile-time expressions](Data.md#37-compile-time-expressions).
- Executor: the default endpoint which relies the compiler infrastructure.
- IL Emitter: the endpoint for emitting to an executable which relies on .NET.

| Feature | Evaluator | Executor | IL Emitter | Explanation |
|-|-|-|-|-|
| `--type=graphics` projects | ✓ | ✓ | ✕ | Standalone graphics DLL under development |
| Non-type templates | ✓ | ✕ | ✕ | Not supported by the .NET runtime |
| Non-integral enums | ✓ | ✕ | ✕ | Not supported by the .NET runtime |
| Pointers | ✕ | ✓ | ✓ | Partially supported the Evaluator but not stable due to internal memory structure |
| Function pointers | ✕ | ✓ | ✓ | Disallowed in the Evaluator due to internal memory structure |
| Externs/DLL imports | ✕ | ✓ | ✓ | Incompatible with the Evaluator |
| Inline IL | ✕ | ✓ | ✓ | Incompatible with the Evaluator |

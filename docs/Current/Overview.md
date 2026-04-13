# 1 Overview

The Belte language is syntactically very similar to C#; Belte is a "C-Style" language that focuses on the
object-oriented paradigm.

Currently, the Belte compiler, Buckle, supports interpretation and building to a .NET executable.

> [Using the compiler CLI](../Buckle.md)

- [1.1](#11-endpoint-specific-features) Endpoint Specific Features
- [1.2](#12-keywords) Keywords

## 1.1 Endpoint Specific Features

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
| Externs/DllImport | ✕ | ✓ | ✓ | Incompatible with the Evaluator |
| Inline IL | ✕ | ✓ | ✓ | Incompatible with the Evaluator |
| .NET DLL references | ✕ | ✓ | ✓ | Incompatible with the Evaluator |

## 1.2 Keywords

All keywords are reserved names and cannot be used as identifiers. No type names are reserved.

Some keywords have multiple meanings depending on context. Those keywords will be disambiguated in the list below.

- [abstract](ClassesAndObjects.md#432-static--constexpr)
- [as](Data.md#32-operators)
- [base](ClassesAndObjects.md#413-base-access)
- [break](ControlFlow.md#245-break)
- [case](ControlFlow.md#25-switch)
- [class](ClassesAndObjects.md#41-classes)
- [constexpr](ClassesAndObjects.md#433-static--constexpr)
- [const](Data.md#33-variables-and-constants) (locals)
- [const](ClassesAndObjects.md#434-const) (methods)
- [constructor](ClassesAndObjects.md#44-constructors)
- [continue](ControlFlow.md#246-continue)
- [default](ControlFlow.md#25-switch)
- [define](Preprocessor.md#71-defineundef)
- [do](ControlFlow.md#242-do-while-loops)
- [elif](Preprocessor.md#72-control)
- [else](ControlFlow.md#23-conditionals)
- [endif](Preprocessor.md#72-control)
- [enum](ClassesAndObjects.md#46-enums)
- [explicit](ClassesAndObjects.md#4232-casts)
- [extends](ClassesAndObjects.md#412-inheritance) (inheritance)
- [extends](ClassesAndObjects.md#4512-special-constraints) (template constraints)
- [extern](LowLevelFeatures.md#67-extern-methods)
- [false](Data.md#31-data-types)
- [flags](ClassesAndObjects.md#461-flags)
- [for](ControlFlow.md#243-for-loops) (for loop)
- [for](ControlFlow.md#244-for-each-loops) (for each loop)
- [global](ClassesAndObjects.md#483-global-using-directive) (global using)
- [global](ClassesAndObjects.md#482-global-disambiguation) (global disambiguation)
- [goto](ControlFlow.md#25-switch)
- [handle](LowLevelFeatures.md#613-compiler-handle)
- [if](ControlFlow.md#23-conditionals) (conditional)
- [if](Preprocessor.md#72-control) (preprocessor)
- [il](LowLevelFeatures.md#611-inline-il)
- [implicit](ClassesAndObjects.md#4232-casts)
- [in](ControlFlow.md#244-for-each-loops)
- [is](Data.md#32-operators)
- [isnt](Data.md#32-operators)
- [lowlevel](LowLevelFeatures.md#61-low-level-contexts)
- [nameof](Data.md#32-operators)
- [namespace](ClassesAndObjects.md#47-namespaces)
- [new](ClassesAndObjects.md#411-declaring-and-using-classes) (instantiation)
- [new](ClassesAndObjects.md#432-overriding-modifiers) (modifier)
- [notnull](ClassesAndObjects.md#4512-special-constraints)
- [noverify](LowLevelFeatures.md#6111-verification)
- [null](Data.md#31-data-types)
- [nullptr](LowLevelFeatures.md#651-creating-and-dereferencing-pointers)
- [operator](ClassesAndObjects.md#423-operators) (normal operators)
- [operator](ControlFlow.md#244-for-each-loops) (for each operators)
- [override](ClassesAndObjects.md#432-overriding-modifiers)
- [pinned](LowLevelFeatures.md#612-pinned-locals)
- [primitive](ClassesAndObjects.md#4512-special-constraints)
- [private](ClassesAndObjects.md#431-accessibility-modifiers)
- [protected](ClassesAndObjects.md#431-accessibility-modifiers)
- [public](ClassesAndObjects.md#431-accessibility-modifiers)
- [ref](Data.md#35-references)
- [return](ControlFlow.md#21-functions)
- [sealed](ClassesAndObjects.md#435-sealed--abstract) (classes)
- [sealed](ClassesAndObjects.md#432-overriding-modifiers) (members)
- [sizeof](LowLevelFeatures.md#69-sizeof-operator)
- [stackalloc](LowLevelFeatures.md#610-stackalloc-operator)
- [static](ClassesAndObjects.md#433-static--constexpr) (modifier)
- [static](ClassesAndObjects.md#48-using-directives) (using directive)
- [struct](LowLevelFeatures.md#62-structures)
- [switch](ControlFlow.md#25-switch)
- [this](ClassesAndObjects.md#411-declaring-and-using-classes)
- [throw](ControlFlow.md#26-exceptions)
- [true](Data.md#31-data-types)
- [typeof](Data.md#32-operators)
- [undef](Preprocessor.md#71-defineundef)
- [using](ClassesAndObjects.md#48-using-directives)
- [virtual](ClassesAndObjects.md#432-overriding-modifiers)
- [where](ClassesAndObjects.md#451-constraint-clauses)
- [while](ControlFlow.md#241-while-loops)

The following keywords are reserved names but are not yet used:

- catch
- finally
- try

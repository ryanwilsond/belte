# 10 Undocumented Features

This document contains entries covering previously undocumented features that are not meant to be used in most
circumstances. This document exists for completeness sake.

- [10.1](#101-cli-options) CLI Options
  - [10.1.1](#1011---nostdlib) `--nostdlib`
  - [10.1.2](#1012---nobootstrap) `--nobootstrap`
  - [10.1.3](#1013--s--c-and--n) `-s`, `-c`, and `-n`
  - [10.1.4](#1014---script) `--script`
  - [10.1.5](#1015---emulate) `--emulate`
- [10.2](#102-evaluator-only-string-enums) Evaluator-Only String Enums
- [10.3](#103-double-verbatim-identifiers) Double Verbatim Identifiers

## 10.1 CLI Options

### 10.1.1 `--nostdlib`

This flag prevents loading the precompiled `Belte.Core.dll` assembly to use for standard library types. This is used
for compiling the standard library itself which defines it's own standard library types.

### 10.1.2 `--nobootstrap`

This flag prevents loading the precompiled `Belte.Core.dll` assembly and instead re-compiles the standard library from
embedded source files. This is used automatically by the [Repl](../Repl.md) to allow the internal Evaluator to be able
to execute standard library methods.

### 10.1.3 `-s`, `-c`, and `-n`

These flags tell the compiler to stop at various stages of compilation related to linking, assembly, etc. They are
reserved but unsupported.

### 10.1.4 `--script`

This flag uses the Interpreter backend which compiles one statement at a time to run. Using this flag will invoke
the Interpreter but it is not stable.

### 10.1.5 `--emulate`

This flag uses the Emulator backend which compiles the program into .NET CIL and interprets that. Using this flag will
invoke the Emulator but it is not stable.

## 10.2 Evaluator-Only String Enums

The Evaluator endpoint only can declare enums with an underlying type of `string`.

```belte
enum MyEnum extends string {
  A = "str1",
  B = "str2",
  C = "str3"
}
```

Every field must have an explicit value.

## 10.3 Double Verbatim Identifiers

The double verbatim specifier `@@` reads all trailing characters as a part of the identifier terminating at whitespace
or a subsequent `@`. This could be used to directly reference compiler-generated symbols. Here be dragons.

# 7 Preprocessor Directives

A very limited set of preprocessor directives exist, primarily to determine
whether or not a program was compiled in debug mode.

> Note: The REPL disables preprocessor directives completely in place of [REPL-specific commands](../Repl.md#repl-meta-commands)

- [7.1](#71-defineundef) Define/Undef
- [7.2](#72-control) Control

Information on `#handle` is [documented elsewhere](LowLevelFeatures.md#613-compiler-handle).

## 7.1 Define/Undef

Use `#define <name>` to define a constant. Use `#undef <name>` to remove that
constant. Constants do not have values that are textually replaced by the
preprocessor like in C, but rather exist as flags to determine whether or not
something is true.

```belte
#define SOME_CONSTANT

#if SOME_CONSTANT

...

#endif
```

When building debug mode, `DEBUG` is defined. Otherwise, `RELEASE` is defined.

## 7.2 Control

`#if`, `#elif`, `#else`, and `#endif` can be used to conditionally include
certain pieces of source.

For example:

```belte
#define SOME_CONSTANT

#if SOME_CONSTANT

Console.PrintLine("SOME_CONSTANT is defined");

#else

Console.PrintLine("SOME_CONSTANT is not defined");

#endif
```

In this example, only the first print call is compiled.

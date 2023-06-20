# Preprocessor Directives

Preprocessor directives are used to do basic computation before compiling. A common example is a check to only compile
some code depending on the build system.

```belte
#if defined(win64)
...
#else
...
#end
```

This is only possible with preprocessor directives as even unreachable code is compiled, and could produce errors for
platform specific code (very rare).

## Conditionals

### \#if, \#elif, \#else, and \#end

Allows for conditionals/branching, however it must be able to resolve at preprocess-time.

```belte
// Allowed
#if defined(win64)
...
#elif defined(win32)
...
#end

// Not allowed, my_int is an unknown value until compile-time
const int my_int = 4;

#if my_int != 5
...
#else
...
#end

```

___

## Definitions

You can define constants that search and replace in the source file, for example:

```belte
// Makes for hard to read code, but preprocessor constants can be used as symbol "aliases"
#define MY_CONSTANT }
// More common use
#define MY_NUM 42

int main() {
    int myint = MY_NUM;
    ...
MY_CONSTANT
```

They can also be used as flags, with out any value.

```belte
#define MY_FLAG

#if defined(MY_FLAG)
...
#endif
```

You can also undefine constants (this allows for redefining constant).

```belte
#define MY_CONST 4

...

#undef MY_CONST
```

### Built-in defined symbols

| Name | Description |
|-|-|
| win64 | When compiling for Windows x64 |
| win32 | When compiling for Windows x86 or x64 (x86_64) |
| dotnet | When compiling for .NET core |

___

## Messages

You can produce a custom error or warning message using `#error` and `#warning` respectively.

They are treated as real errors and warnings. All text after `#error` or `#warning` will be included in the message.

```belte
#warning Some useful warning message

#error Some useful error message that will stop compilation
```

___

## Pragmas

Pragma statements are used to communicate extra instructions to the compiler.

## \#pragma warning

Disables or enables a warning message.

```belte
// Prevents compiler from creating warning messages because of unused variables
#pragma warning disable unused

...

// Either work to restore back to normal state
#pragma warning enable unused
#pragma warning default unused
```

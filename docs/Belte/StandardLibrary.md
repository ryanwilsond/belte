# 5 The Standard Library

The Standard Library is a collection of classes that are implicitly included in all Belte compilations (i.e. they do not
need importing).

- [5.1](#51-built-in-functions) Built-in Functions
- [5.2](StandardLibrary/Console.md) Console
- [5.3](StandardLibrary/Math.md) Math

## 5.1 Built-In Functions

Belte includes functions that are available everywhere that achieve basic functionality. These serve as temporary
placeholders for eventual Standard Library modules.

| Signature | Description | Exceptions |
|-|-|-|
| `int! RandInt(int max)` | Gets a random number from 0 to {max} | |
| `any! Value(any value)` | Converts {value} to non-nullable any | NullReferenceException |
| `bool! Value(bool value)` | Converts {value} to non-nullable boolean | NullReferenceException |
| `decimal! Value(decimal value)` | Converts {value} to non-nullable decimal | NullReferenceException |
| `string! Value(string value)` | Converts {value} to non-nullable string | NullReferenceException |
| `int! Value(int value)` | Converts {value} to non-nullable integer | NullReferenceException |
| `bool! HasValue(any value)` | Returns `true` is {value} is not null, `false` otherwise | |
| `bool! HasValue(bool value)` | Returns `true` is {value} is not null, `false` otherwise | |
| `bool! HasValue(string value)` | Returns `true` is {value} is not null, `false` otherwise | |
| `bool! HasValue(decimal value)` | Returns `true` is {value} is not null, `false` otherwise | |
| `bool! HasValue(int value)` | Returns `true` is {value} is not null, `false` otherwise | |
| `string! Hex(int! value, bool! prefix = false)` | Converts {value} to its base16 representation; if {prefix} is `true`, the representation includes the `0x` prefix | |
| `int! Ascii(string! char)` | Converts {char} its respective ASCII code | ArgumentException |
| `string! Char(int! ascii)` | Converts {ascii} to its respective character | ArgumentException |

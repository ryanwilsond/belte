# 5 The Standard Library

The Standard Library is a collection of classes that are implicitly included in all Belte compilations (i.e. they do not
need importing).

Some Standard Library classes are included by default (i.e. need no import and are automatically declared), while others
require an explicit import.

- [5.1](#51-built-in-functions) Built-in Functions
- [5.2](StandardLibrary/Console.md) Console (Included By Default)
- [5.3](StandardLibrary/Math.md) Math (Included By Default)
- [5.4](StandardLibrary/List.md) List
- [5.5](StandardLibrary/Dictionary.md) Dictionary

## 5.1 Built-In Functions

Belte includes functions that are available everywhere that achieve basic functionality. These serve as temporary
placeholders for eventual Standard Library modules.

| Signature | Description | Exceptions |
|-|-|-|
| `int Ascii(string char)` | Converts {char} its respective ASCII code | |
| `int! Ascii(string! char)` | Converts {char} its respective ASCII code | ArgumentException |
| `string Char(int ascii)` | Converts {ascii} to its respective character | |
| `string! Char(int! ascii)` | Converts {ascii} to its respective character | ArgumentException |
| `string Hex(int value, bool! prefix = false)` | Converts {value} to its base16 representation; if {prefix} is `true`, the representation includes the `0x` prefix | |
| `string! Hex(int! value, bool! prefix = false)` | Converts {value} to its base16 representation; if {prefix} is `true`, the representation includes the `0x` prefix | |
| `int Length(any array)` | Gets the length of {array}, or null if it has no length | |
| `int! RandInt(int! max)` | Gets a random number from 0 to {max} | |

Additionally, there are multiple built-in functions that exist only to support the Evaluator back end and should not be
used in higher-level applications. These are:

| `bool ObjectsEqual(Object x, Object y)` | Value compares two objects. | |
| `bool ObjectReferencesEqual(ref Object x, ref Object y)` | Reference compares two objects. | |
| `any ToAny(any primitive)` | Converts a pure `any` type to a specifically non-object `any`. | |
| `any ToObject(any object)` | Converts a pure `any` type to a specifically object `any`. | |

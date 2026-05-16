# 5.9 Int and other Numerics

The Int class provides static `int` helpers.

The Belte public interface for the Int class can be found
[on the Belte GitHub repository](https://github.com/ryanwilsond/belte/blob/main/src/Belte/Native/Standard/Int.blt).

- [5.9.1](#591-fields) Fields
- [5.9.2](#592-methods) Methods

## 5.9.1 Fields

The `Int64`, `UInt64`, `Int32`, `UInt32`, `Int16`, `UInt16`, `Int8`, and `UInt8` classes define the following fields for
`int64`, `uint64`, `int32`, `uint32`, `int16`, `uint16`, `int8`, and `uint8` respectively:

| Signature | Description |
| - | - |
| `<type>! MinValue` | Minimum value of `<type>`. |
| `<type>! MaxValue` | Maximum value of `<type>`. |

## 5.9.2 Methods

| Signature | Description |
| - | - |
| `int? Parse(string?)` | Tries to parse the given string into an int. Returns null if the string is not a valid int. |
| `string? ToString(int!, string!)` | Tries to convert the given int into a string with a format (e.g. "X" for hexadecimal). |

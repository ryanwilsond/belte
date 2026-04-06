# 5.4 String

The String class provides static `string` helpers.

The Belte public interface for the String class can be found [here](../../../src/Belte/Native/Standard/String.blt).

- [5.4.1](#541-methods) Methods

## 5.4.1 Methods

| Signature | Description |
|-|-|
| `string![]! Split(string!, string!)` | Splits the first given string at every instance of the second given string. The return value will not contain any instances of the second given string. |
| `int Ascii(string!)` | Returns the ascii value of the given string given that it is 1 character long. Returns null otherwise. (Note that the ascii value of a `char` can be attained directly by casting it to an `int`.) |
| `string! Char(int!)` | Returns a string of length 1 containing the corresponding character of the given ascii code. (Note that the `char` of an ascii value can be attained directly by casting an `int` to a `char`.) |
| `int! Length(string!)` | Returns the length of a string. |

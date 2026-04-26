# 5.4 String

The String class provides static `string` helpers.

The Belte public interface for the String class can be found
[on the Belte GitHub repository](https://github.com/ryanwilsond/belte/blob/main/src/Belte/Native/Standard/String.blt).

- [5.4.1](#541-methods) Methods

## 5.4.1 Methods

| Signature | Description |
|-|-|
| `int? Ascii(string!)` | Returns the ascii value of the given string given that it is 1 character long. Returns null otherwise. (Note that the ascii value of a `char` can be attained directly by casting it to an `int`.) |
| `string! Char(int!)` | Returns a string of length 1 containing the corresponding character of the given ascii code. (Note that the `char` of an ascii value can be attained directly by casting an `int` to a `char`.) |
| `bool! IsDigit(char?)` | If the given char is 0 through 9. |
| `bool! IsNullOrWhiteSpace(string?)` | Returns true if the given string is null, empty, or contains only whitespace characters. |
| `bool! IsNullOrWhiteSpace(char?)` | Returns true if the given char is null or is a whitespace character. |
| `int! Length(string!)` | Returns the length of a string. |
| `string![]! Split(string!, string!)` | Splits the first given string at every instance of the second given string. The return value will not contain any instances of the second given string. |
| `string? Substring(string?, int?, int?)` | Returns a copy of the given string starting at the given index with a given length. |

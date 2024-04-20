# 5 Built-In Functions

| Signature | Description | Exceptions |
|-|-|-|
| `void Print(any msg)` | Displays {msg} to the console | |
| `void PrintLine(any msg)` | Displays {msg} to the console with an added line ending | |
| `void PrintLine()` | Displays a line ending to the console | |
| `string! Input()` | Gets user input from the console | |
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

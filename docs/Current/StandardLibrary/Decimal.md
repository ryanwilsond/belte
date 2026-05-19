# 5.10 Decimal, Float64, and Float32

The Decimal, Float64, and Float32 classes provide static `decimal`, `float64`, and `float32` helpers respectively.

The Belte public interface for the described classes can be found
[on the Belte GitHub repository](https://github.com/ryanwilsond/belte/blob/main/src/Belte/Native/Standard/Decimal.blt).

- [5.10.1](#5101-fields) Fields
- [5.10.2](#5102-methods) Methods

## 5.10.1 Fields

The Float64 class defines the following constants:

| Signature | Description |
| - | - |
| `float64! MinValue` | Double precision float minimum value. |
| `float64! MaxValue` | Double precision float maximum value. |
| `float64! Epsilon` | Double precision float epsilon value. |
| `float64! NegativeInfinity` | Double precision float negative infinity. |
| `float64! PositiveInfinity` | Double precision float positive infinity. |
| `float64! NaN` | Double precision float NaN (Not a Number). |

The Float32 class defines the following constants:

| Signature | Description |
| - | - |
| `float32! MinValue` | Single precision float minimum value. |
| `float32! MaxValue` | Single precision float maximum value. |
| `float32! Epsilon` | Single precision float epsilon value. |
| `float32! NegativeInfinity` | Single precision float negative infinity. |
| `float32! PositiveInfinity` | Single precision float positive infinity. |
| `float32! NaN` | Single precision float NaN (Not a Number). |

## 5.10.2 Methods

The Decimal class defines the following methods:

| Signature | Description |
| - | - |
| `bool! IsNaN(float64!)` | Returns true if the given value is NaN. |
| `bool! IsNaN(float32!)` | Returns true if the given value is NaN. |
| `bool! IsPosInfinity(float64!)` | Returns true if the given value is positive infinity. |
| `bool! IsPosInfinity(float32!)` | Returns true if the given value is positive infinity. |
| `bool! IsNegInfinity(float64!)` | Returns true if the given value is negative infinity. |
| `bool! IsNegInfinity(float32!)` | Returns true if the given value is negative infinity. |
| `decimal? Parse(string?)` | Tries to parse the given string into a decimal. Returns null if the string is not a valid decimal. |
| `string? ToString(decimal!, string!)` | Tries to convert the given decimal into a string with a format. |

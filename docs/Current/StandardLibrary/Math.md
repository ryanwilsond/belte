# 5.2 Math

The Math class provides an interface for common math values and functions.

The Belte public interface for the Math class can be found [here](../../../src/Belte/Native/Standard/Math.blt).

- [5.2.1](#521-fields) Fields
- [5.2.1](#522-methods) Methods

## 5.2.1 Fields

| Signature | Description |
|-|-|
| `decimal! E` | Euler's number (e). |
| `decimal! PI` | The mathematical constant PI (π). |

## 5.2.2 Methods

Many of the methods have multiple overloads for integer/decimal
nullable/non-nullable values. Only one overload for each method will be listed.

| Name | Description |
|-|-|
| `decimal Abs(decimal)` | Absolute value. |
| `decimal Acos(decimal)` | Arccosine. |
| `decimal Acosh(decimal)` | Inverse hyperbolic cosine. |
| `decimal Asin(decimal)` | Arcsine. |
| `decimal Asinh(decimal)` | Inverse hyperbolic sine. |
| `decimal Atan(decimal)` | Arctangent. |
| `decimal Atanh(decimal)` | Inverse hyperbolic tangent. |
| `decimal Ceiling(decimal)` | Ceiling rounding. |
| `decimal Clamp(decimal, decimal, decimal)` | Clamp a given value between a given min and max. |
| `decimal Cos(decimal)` | Cosine. |
| `decimal Cosh(decimal)` | Hyperbolic cosine. |
| `decimal Exp(decimal)` | Euler's number to a power. |
| `decimal Floor(decimal)` | Floor rounding. |
| `decimal Lerp(decimal, decimal, decimal)` | Linear interpolation of a given start, end, and rate. |
| `decimal Log(decimal, decimal)` | Logarithm of a given value to a given base. |
| `decimal Log(decimal)` | Natural logarithm of a given value. |
| `decimal Max(decimal, decimal)` | Maximum value between two given values. |
| `decimal Min(decimal, decimal)` | Minimum value between two given values. |
| `decimal Pow(decimal, decimal)` | Power/exponentiation of a given value and power. |
| `decimal Round(decimal)` | Rounding. |
| `int Sign(decimal)` | The sign of a given value, returning `1` for positive and `-1` for negative. |
| `decimal Sin(decimal)` | Sine. |
| `decimal Sinh(decimal)` | Hyperbolic sine. |
| `decimal Sqrt(decimal)` | Square root. |
| `decimal Tan(decimal)` | Tangent. |
| `decimal Tanh(decimal)` | Hyperbolic tangent. |
| `decimal Truncate(decimal)` | Truncation. |

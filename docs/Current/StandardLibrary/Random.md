# 5.3 Random

The Random class provides a way to get certain random values.

The Belte public interface for the Random class can be found
[on the Belte GitHub repository](https://github.com/ryanwilsond/belte/blob/main/src/Belte/Native/Standard/Random.blt).

- [5.3.1](#531-methods) Methods

## 5.3.1 Methods

| Signature | Description |
|-|-|
| `int! RandInt(int? max)` | Gets a random non-negative `int` that is less that `max`, or less than the int-64 maximum if `max` is null. |
| `decimal! Random()` | Gets a random `decimal` greater than or equal to `0` and less than `1`. |

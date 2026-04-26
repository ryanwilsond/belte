# 5.1 Console

The Console class provides a way to interact with the console.

The Belte public interface for the Console class can be found
[on the Belte GitHub repository](https://github.com/ryanwilsond/belte/blob/main/src/Belte/Native/Standard/Console.blt).

- [5.1.1](#511-methods) Methods
- [5.1.2](#512-classes) Classes
  - [5.1.2.1](#5121-color) Color

## 5.1.1 Methods

| Signature | Description |
|-|-|
| `int GetWidth()` | Gets the console character width. |
| `int GetHeight()` | Gets the console character height. |
| `string! Input()` | Gets a line of input from the console. |
| `void PrintLine(string?)` | Writes a string to the console followed by a line return. |
| `void PrintLine(any?)` | Writes a value to the console followed by a line return. |
| `void PrintLine(Object)` | Writes the result of `Object.ToString()` to the console followed by a line return. |
| `void PrintLine(char?[]?)` | Writes a char array to the console as a string followed by a line return. |
| `void PrintLine()` | Writes an empty line to the console. |
| `void Print(string?)` | Writes a string to the console. |
| `void Print(any?)` | Writes a value to the console. |
| `void Print(Object)` | Writes the result of `Object.ToString()` to the console. |
| `void Print(char?[]?)` | Writes a char array to the console as a string. |
| `void ResetColor()` | Resets the foreground and background colors of the console. |
| `void SetForegroundColor(int!)` | Sets the console foreground color based on [Color](#5121-color). |
| `void SetBackgroundColor(int!)` | Sets the console background color based on [Color](#5121-color). |
| `void SetCursorPosition(int?, int?)` | Sets the console cursor position based on left and top. If either argument is null it will be ignored i.e. that axis of the cursor will not change. |
| `void SetCursorVisibility(bool!)` | Sets the console cursor to be visible or not. |

## 5.1.2 Classes

### 5.1.2.1 Color

The Color class contains all available console colors that can be used via static, constant-expression fields. These
values are passed into `SetForegroundColor` `SetBackgroundColor`.

| Name | Value |
|-|-|
| `Black` | `0` |
| `DarkBlue` | `1` |
| `DarkGreen` | `2` |
| `DarkCyan` | `3` |
| `DarkRed` | `4` |
| `DarkMagenta` | `5` |
| `DarkYellow` | `6` |
| `Gray` | `7` |
| `DarkGray` | `8` |
| `Blue` | `9` |
| `Green` | `10` |
| `Cyan` | `11` |
| `Red` | `12` |
| `Magenta` | `13` |
| `Yellow` | `14` |
| `White` | `15` |

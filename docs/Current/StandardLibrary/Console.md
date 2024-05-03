# 5.2 Console (Included By Default)

The Console class provides a way to interact with the console/terminal via IO.

The Belte public interface for the Console class can be found [here](../../../src/Belte/Standard/Console.blt).

- [5.2.1](#521-color) Color
- [5.2.2](#522-printline) PrintLine
- [5.2.3](#523-print) Print
- [5.2.4](#524-input) Input
- [5.2.5](#525-setforegroundcolor) SetForegroundColor
- [5.2.6](#526-setbackgroundcolor) SetBackgroundColor
- [5.2.7](#527-resetcolor) ResetColor

## 5.2.1 Color

The Color class contains all available console colors that can be used via static, constant-expression fields. These
values are passed into [SetForegroundColor](#525-setforegroundcolor) and [SetBackgroundColor](#526-setbackgroundcolor).

| Field Name | Value |
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

## 5.2.2 PrintLine

Writes the specified data, followed by the current line terminator, to the standard output stream.

### Overloads

|||
|-|-|
| `PrintLine(string)` | Prints the string as-is, followed by the current line terminator. |
| `PrintLine(object)` | Prints the object after converting it to a string, followed by the current line terminator. |
| `PrintLine()` | Prints only the current line terminator. |

## 5.2.3 Print

Writes the specified data to the standard output stream.

### Overloads

|||
|-|-|
| `Print(string)` | Prints the string as-is. |
| `Print(object)` | Prints the object after converting it to a string. |

## 5.2.4 Input

Reads the next line of characters from the standard input stream.

`Input(string)`

## 5.2.5 SetForegroundColor

Sets the foreground color of the console using a color code from [Color](#521-color).

`SetForegroundColor(int!)`

## 5.2.6 SetBackgroundColor

Sets the background color of the console using a color code from [Color](#521-color).

`SetBackgroundColor(int!)`

## 5.2.7 ResetColor

Resets the foreground and background color of the console to default.

`ResetColor()`

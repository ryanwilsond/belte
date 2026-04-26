# 5.6 IO

The File and Directory classes allowing reading and writing to files/directories.

The Belte public interface for the File class can be found
[on the Belte GitHub repository](https://github.com/ryanwilsond/belte/blob/main/src/Belte/Native/Standard/IO/File.blt).

The Belte public interface for the Directory class can be found
[on the Belte GitHub repository](https://github.com/ryanwilsond/belte/blob/main/src/Belte/Native/Standard/IO/Directory.blt).

- [5.6.1](#561-file-methods) File Methods
- [5.6.2](#562-directory-methods) Directory Methods

## 5.6.1 File Methods

| Signature | Description |
|-|-|
| `void AppendLines(string!, List<string!>!)` | Appends lines to an existing file at the given path string. |
| `void AppendText(string!, string!)` | Appends a string to an existing file at the given path string. |
| `void Create(string!)` | Creates a file at the given path string if it doesn't already exist. |
| `void Delete(string!)` | Deletes the file at the given path string if it exists. |
| `bool! Exists(string!)` | Returns `true` if the file at the given path string exists. |
| `List<string!>! ReadLines(string!)` | Reads the lines of an existing file at the given path string. |
| `string! ReadText(string!)` | Reads all of the text of an existing file at the given path string. |
| `void WriteLines(string!, List<string!>!)` | Writes lines to an existing file at the given path string. |
| `void WriteText(string!, string!)` | Writes a string to an existing file at the given path string. |

## 5.6.2 Directory Methods

| Signature | Description |
|-|-|
| `void Create(string!)` | Creates a directory at the given path string if it doesn't already exist. |
| `void Delete(string!)` | Deletes the directory at the given path string if it exists. |
| `bool! Exists(string!)` | Returns `true` if the directory at the given path string exists. |
| `string! GetCurrentDirectory()` | Gets the current working directory of the application. |
| `List<string!>! GetDirectories(string!)` | Gets a list of directories names at the given path string. |
| `List<string!>! GetFiles(string!)` | Gets a list of file names at the given path string. |

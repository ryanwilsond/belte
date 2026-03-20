# 5.5 Directory (Included By Default)

The File class provides an interface for interacting with files.

- [5.5.1](#551-definition) Definition

## 5.5.1 Definition

The Belte public interface for the File class can be found [here](../../../src/Belte/Standard/IO/File.blt).

| Method | Description |
|-|-|
| `void AppendLines(string!, List<string!>!)` | At the given path, appends the given lines to the file found. |
| `void AppendText(string!, string!)` | At the given path, appends the given text to the file found. |
| `void Create(string!)` | Creates a file at the given path. |
| `void Copy(string!, string!)` | Copies the contents of a file at the given path to a file at the next given path. |
| `void Delete(string!)` | Deletes a file at the given path. |
| `bool! Exists(string!)` | Checks if a file exists at the give path. |
| `List<string!>! ReadLines(string!)` | Reads all the lines from the file at the given path. |
| `string! ReadText(string!)` | Reads all the text from the file at the given path. |
| `void WriteLines(string!, List<string!>!)` | At the given path, writes the given lines to the file found. |
| `void WriteText(string!, string!)` | At the given path, writes the given text to the file found. |

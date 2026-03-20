# 5.4 Directory (Included By Default)

The Directory class provides an interface for interacting with directories.

- [5.4.1](#541-definition) Definition

## 5.4.1 Definition

The Belte public interface for the Directory class can be found [here](../../../src/Belte/Standard/IO/Directory.blt).

| Method | Description |
|-|-|
| `void Create(string!)` | Creates a directory at the given path. |
| `void Delete(string!)` | Deletes a directory at the given path. |
| `bool! Exists(string!)` | Checks if a directory exists at the give path. |
| `string! GetCurrentDirectory()` | Gets the current full path of the current directory. |
| `List<string!>! GetDirectories(string!)` | Gets a list of the directories found at the given path. |
| `List<string!>! GetFiles(string!)` | Gets a list of the files found at the given path. |

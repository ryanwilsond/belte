# 5.3 Dictionary (Included By Default)

A Dictionary template is an ordered hashmap implementation.

- [5.3.1](#531-definition) Definition
- [5.3.2](#532-initializer-dictionaries) Initializer Dictionaries

## 5.3.1 Definition

The Belte public interface for the List class can be found [here](../../../src/Belte/Standard/Collections/Dictionary.blt).

| Method | Description |
|-|-|
| `void Add(TKey, TValue)` | Adds a key value pair. |
| `void Clear()` | Removes all elements. |
| `bool! ContainsKey(TKey)` | If the dictionary contains the given key. |
| `bool! ContainsValue(TValue)` | If the dictionary contains the given value. |
| `int! Length()` | The number of elements. |
| `bool Remove(TKey)` | Removes the pair with the given key. Returns true if succeeded, or false if the key was not present. |

## 5.3.2 Initializer Dictionaries

Initializer dictionaries create Dictionaries.

```belte
var a = { "A": 1, "B": 2, "C": 3 };
```

The previous example could more explicitly be written as:

```belte
Dictionary<string, int> a = new Dictionary<string, int>();
a.Add("A", 1);
a.Add("B", 2);
a.Add("C", 3);
```

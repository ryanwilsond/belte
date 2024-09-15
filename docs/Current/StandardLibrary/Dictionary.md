# 5.3 Dictionary

A Dictionary template is an ordered hashmap implementation.

- [5.3.1](#531-definition) Definition
- [5.3.2](#532-initializer-dictionaries) Initializer Dictionaries

## 5.3.1 Definition

The Belte public interface for the List class can be found [here](../../../src/Belte/Standard/Collections/Dictionary.blt).

| Method | Description |
|-|-|

## 5.3.2 Initializer Dictionaries

Initializer dictionaries create Dictionaries.

```belte
var a = { "A": 1, "B": 2, "C": 3 };
```

The previous example could more explicitly be written as:

```belte
Dictionary<string, int> a = new Dictionary<string, int>({ { "A", 1 }, { "B", 2 }, { "C", 3 } });
```

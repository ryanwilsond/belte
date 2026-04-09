# 5.7.2 Dictionary

A Dictionary template is an ordered hashmap implementation.

The Belte public interface for the Dictionary template can be found
[on the Belte GitHub repository](https://github.com/ryanwilsond/belte/blob/main/src/Belte/Native/Standard/Collections/Dictionary.blt).

- [5.7.2.1](#5721-constructors) Constructors
- [5.7.2.2](#5722-methods) Methods
- [5.7.2.3](#5723-operators) Operators
- [5.7.2.4](#5724-initializer-dictionaries) Initializer Dictionaries

## 5.7.2.1 Constructors

The Dictionary template has two template parameters. The first corresponds to
the key type, the second corresponds to the value type.

| Signature | Description |
|-|-|
| `new Dictionary<type TKey, type TValue>()` | Creates an empty dictionary. |
| `Dictionary<type TKey, type TValue>(int!)` | Creates an empty dictionary with a starting capacity. |
| `new Dictionary<type TKey, type TValue>(EqualityComparer<TKey>)` | Creates an empty dictionary with a custom equality comparer. |
| `new Dictionary<type TKey, type TValue>(int!, EqualityComparer<TKey>)` | Creates an empty dictionary with a starting capacity and a custom equality comparer. |
| `new Dictionary<type TKey, type TValue>(Dictionary<TKey, TValue>)` | Copies the given dictionary. |
| `new Dictionary<type TKey, type TValue>(Dictionary<TKey, TValue>, EqualityComparer<TKey>)` | Copies the given dictionary with a new equality comparer. |

For example, to create an empty dictionary where the key type is `int` and the
value type is `string`:

```belte
new Dictionary<int, string>();
```

## 5.7.2.2 Methods

| Signature | Description |
|-|-|
| `void Add(TKey, TValue)` | Adds a key value pair. |
| `void Clear()` | Removes all elements. |
| `const bool! ContainsKey(TKey)` | If the dictionary contains the given key. |
| `const bool! ContainsValue(TValue)` | If the dictionary contains the given value. |
| `const int! Length()` | The number of elements. |
| `bool Remove(TKey)` | Removes the pair with the given key. Returns true if succeeded, or false if the key was not present. |

## 5.7.2.3 Operators

| Signature | Description |
|-|-|
| `static ref TValue operator[](Dictionary<TKey, TValue>, TKey)` | Gets the value associated with the given key. |
| `static Enumerator<KeyValuePair<TKey, TValue>>! operator iter(Dictionary<TKey, TValue>)` | Used in [iterating for loops](../ControlFlow.md#2444-enumerated-collections). |

For example, to set and get a value from a dictionary:

```belte
var myDict = new Dictionary<int, string>();
myDict.Add(3, "test");

myDict[3] = "new string";
var myValue = myDict[3];
```

Note that the indexing operator does not create new entries in the dictionary, only modifies them.

## 5.7.2.4 Initializer Dictionaries

Initializer dictionaries are special syntax that create dictionaries implicitly.

```belte
var myDict = { "A": 1, "B": 2, "C": 3 };
```

The above is shorthand for:

```belte
Dictionary<string, int> myDict = new Dictionary<string, int>();
myDict.Add("A", 1);
myDict.Add("B", 2);
myDict.Add("C", 3);
```

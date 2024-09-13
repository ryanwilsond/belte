# 5.4 List

The List template is a dynamic array implementation.

- [5.4.1](#541-definition) Definition
- [5.4.2](#542-initializer-lists) Initializer Lists

## 5.4.1 Definition

The Belte public interface for the List class can be found [here](../../../src/Belte/Standard/List.blt).

| Method | Description |
|-|-|
| `void Append(T)` | Adds an element to the end of the list. |
| `void AppendRange(List<T>)` | Adds a List of elements to the end of the list. |
| `void Assign(int!, T)` | Assigns a value at a specified index. |
| `void Clear()` | Removes all elements form the list. |
| `void Fill(T)` | Fills the entire list with a value. |
| `const T Index(int!)` | Gets the element at the given index. |
| `const int Length()` | Gets the number of elements in the list. |
| `void Pop()` | Removes the last element from the list. |
| `const List<T> Subset(int!, int!)` | Copies list elements in a sub range to a new list. |
| `const T[] ToArray()` | Copies all list elements into an array. |

## 5.4.2 Initializer Lists

Initializer lists create Lists.

```belte
var a = { 1, 2, 3 };
```

The previous example could more explicitly be written as:

```belte
List<int> a = new List<int>({ 1, 2, 3 });
```

If an array is intended, use a [low-level context](../LowLevelFeatures.md).

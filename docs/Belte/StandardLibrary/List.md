# 5.4 List

The List template is a dynamic array implementation.

The Belte public interface for the List class can be found [here](../../../src/Belte/Standard/List.blt).

| Method | Description |
|-|-|
| `void Append(T)` | Adds an element to the end of the list. |
| `void Pop()` | Removes the last element from the list. |
| `void Clear()` | Removes all elements form the list. |
| `void Assign(int!, T)` | Assigns a value at a specified index. |
| `const int Length()` | Gets the number of elements in the list. |
| `const T[] ToArray()` | Copies all list elements into an array. |
| `const T Index(int!)` | Gets the element at the given index. |
| `const List<T> Subset(int!, int!)` | Copies list elements in a sub range to a new list. |

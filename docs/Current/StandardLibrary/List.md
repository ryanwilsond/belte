# 5.7.1 List

The List template is a dynamic array implementation.

The Belte public interface for the List template can be found
[on the Belte GitHub repository](https://github.com/ryanwilsond/belte/blob/main/src/Belte/Native/Standard/Collections/List.blt).

- [5.7.1.1](#5711-constructors) Constructors
- [5.7.1.2](#5712-methods) Methods
- [5.7.1.3](#5713-operators) Operators

## 5.7.1.1 Constructors

The List template has one template parameter corresponding to the element type.

| Signature | Description |
|-|-|
| `new List<type T>()` | Creates an empty list. |
| `new List<type T>(int!)` | Creates a list of a given length where each element is it's default value. |
| `new List<type T>(int!, T)` | Creates a list of a given length and fills it with the given value. |
| `new List<type T>(T[])` | Creates a list of the same length as the given array and copies the values of the given array into it. |
| `new List<type T>(List<T>)` | Copies the given list. |

For example, to create an empty list where the elements are of type `int`:

```belte
new List<int>();
```

## 5.7.1.2 Methods

| Signature | Description |
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

## 5.7.1.3 Operators

| Signature | Description |
|-|-|
| `static ref T operator[](List<T>, int)` | Gets the value at the given index. |
| `static int! operator length(List<T>)` | Used in [iterating for loops](../ControlFlow.md#2443-indexed-collections). |
| `static implicit operator List<T>(T[])` | Creates a list from an array. |

For example, to index a list:

```belte
var myList = new List<int>(10);
var firstElement = myList[0];
```

Because the index operator returns a reference, you can also assign to the
result:

```belte
var myList = new List<int>(10);
myList[0] = 5;
```

The implicit `List<T>` cast lets you create a list from an array without having
manually write out a constructor call:

```belte
List<int> myList = { 1, 2, 3 };
```

# 5.8 LowLevel

The LowLevel class provides various helpers for users who are writing
"lower level" code. Calling these methods does not require being inside a
`lowlevel` context.

The Belte public interface for the String class can be found [here](../../../src/Belte/Native/Standard/LowLevel.blt).

- [5.8.1](#581-methods) Methods

## 5.8.1 Methods

| Signature | Description |
|-|-|
| `int! GetHashCode(Object!)` | Equivalent to calling `Object.GetHashCode()`. |
| `string! GetTypeName(Object!)` | Equivalent to calling `Object.GetTypeName()`. |
| `int! Length<type T>(T!)` | Gets the length of the given array, or 0 if not passed an array. |
| `void Sort<type T>(T!)` | Sorts the given array, or does nothing if not passed an array. |
| `int32 SizeOf<type T>()` | Gets the size of the template argument type in number of bytes. (Using the [`sizeof` operator](../LowLevelFeatures.md#69-sizeof-operator) is preferred.) |
| `uint8* CreateLPCSTR(string!)` | Creates a raw `uint8` (representing ascii characters) array with the content of the passed string and returns a pointer to the first element. |
| `char* CreateLPCWSTR(string!)` | Creates a raw `char!` (representing unicode characters) array with the content of the passed string and returns a pointer to the first element. |
| `void FreeLPCSTR(uint8*)` | Frees the memory used by a raw `uint8` array. |
| `void FreeLPCWSTR(char*)` | Frees the memory used by a raw `char!` array. |
| `string! ReadLPCSTR(uint8*)` | Creates a string with the contents of a raw null-terminated `uint8` array. |
| `string! ReadLPCWSTR(char*)` | Creates a string with the contents of a raw null-terminated `char!` array. |
| `void* GetGCPtr(Object!)` | Creates a garbage collector handle for the given object and returns a pointer to that handle.* |
| `void FreeGCHandle(void*)` | Frees the given garbage collector handle.* |
| `Object! GetObject(void*)` | Gets the object associated with the given garbage collector handle.* |

*Note that while you can get the address of `this`, it is safer to use
`GetGCPtr` and `GetObject` as they will stay accurate even if the garbage
collector moves the object, while storing the result of `&this` can become
stale. In addition, `&this` points to the start of the object and not the start
of the object's vtable, meaning calling methods of it will be incorrect unless
the pointer is offset to adjust.

For example:

```belte
class MyClass {
  int f = 5;

  public int GetF() {
    return f;
  }

  public MyClass* GetPtr() {
    return &this;
  }
}

var myClass = new MyClass();
var ptr = myClass.GetPtr();

// ! Incorrect, undefined behavior
var f = ptr->GetF();
```

You might think to pin the object and adjust the pointer:

```belte
class MyClass {
  int f = 5;

  public int GetF() {
    return f;
  }

  public MyClass* GetPtr() {
    return (MyClass*) (((int64)&this) + 232);
  }
}

pinned var myClass = new MyClass();
var ptr = myClass.GetPtr();
var f = ptr->GetF();
```

This ensures the object does not move around on the heap, meaning `&this` will
not be stale. The offset of `232` moves the pointer to the start of the vtable.

However, this should not be done because it relies on the CoreCLR implementation
of objects, which could change meaning the `232` offset becomes invalid. It is
safer to use `GetGCPtr` and `GetObject`:

```belte
class MyClass {
  int f = 5;

  public int GetF() {
    return f;
  }

  public void* GetPtr() {
    return LowLevel.GetGCPtr(this);
  }
}

var myClass = new MyClass();
var ptr = myClass.GetPtr();

var obj = LowLevel.GetObject(ptr);
var f = obj.GetF();

LowLevel.FreeGCHandle(ptr);
```

Remember to always free the garbage collector handle when finished using it.

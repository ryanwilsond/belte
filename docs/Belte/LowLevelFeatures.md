# 6 Low-Level Features

These features are only enabled in low-level contexts. Currently this only includes Repl submissions.

- [6.1](#61-structures) Structures

## 6.1 Structures

Structures are less feature rich but more efficient classes. Eventually, the compiler will automatically rewrite classes]]
into structures if possible to optimize, but this is currently unsupported.

Structures only allow field declarations with no initializers. Fields within structures cannot be constants or
references.

```belte
struct MyStruct {
  int a;
  string b;
}
```

Creating a new instance of a structure uses the same `new` keyword as classes, but the constructor cannot be overridden
and always takes no arguments:

```belte
var myInstance = new MyStruct();
```

Because of this, all fields must manually be written to after structure creation:

```belte
myInstance.a = 3;
myInstance.b = "Hello";
```

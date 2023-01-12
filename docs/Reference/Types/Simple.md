# Simple Types

## Any

The `any` type keyword indicates the Any type, which acts as a temporary
placeholder for the `Object` type, as there are currently no objects or classes
in Belte. You can treat the Any type as another other type:

```blt
any myVar = 3;
myVar = "test";
myVar = true;
```

### Casts

You have to cast the Any type to use it in a context expecting another type:

```blt
any myVar = 3;
int myInt = myVar; // Will be an error, instead:
int myInt = (int)myVar;
```

Be aware that casting an Any type will never throw an error at compile-time,
so an exception will be thrown if the cast fails at runtime.

___

## Boolean

The `bool` type keyword indicates the Boolean type, which can either be `true`
or `false`. By default, any boolean value supports three-value boolean logic, as
in the boolean can additionally be `null`. To disable three-value boolean logic,
use the `[NotNull]` attribute on the type.



### Literals

You can use the `true` and `false` literals to initialize a `bool` variable or
to pass a `bool` value:

```blt
bool check = true;
Print(check ? "Checked" : "Not checked"); // output: Checked

Print(false ? "Checked" : "Not checked"); // output: Not checked
```

### Casts



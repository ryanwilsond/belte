# 0 Future Features Overview

Belte in its current state is largely similar to C#, but many ambitious diverging features are planned for the
(near-ish) future. This document goes over some of the planned features as well as smaller features needed before taking
them on. This includes features present in C# that aren't in Belte yet.

- [0.1](#01-tuples) Tuples
- [0.2](#02-lambdas) Lambdas
- [0.3](#03-interfaces) Interfaces
- [0.4](#04-verse-style-concurrencyasync) Verse-Style Concurrency/Async
  - [0.4.1](#041-sync) `sync`
    - [0.4.1.1](#0411-async-and-suspend) `async` and `suspend`
  - [0.4.2](#042-race) `race`
    - [0.4.2.1](#0421-cancel) `cancel`
  - [0.4.3](#043-branch) `branch`
  - [0.4.4](#044-spawn) `spawn`
  - [0.4.5](#045-lock) `lock`
- [0.5](#05-lsp) LSP
- [0.6](#06-has-constraints) Has Constraints

## 0.1 Tuples

C#-style tuples that represent a small collection of values. This includes implicit-typing and deconstruction:

```belte
(int, bool) MyFunc() {
  return (10, true);
}

var (myInt, myBool) = MyFunc();
```

For-each loops would also support deconstruction:

```belte
(int, string)[] myArr = /* ... */;

for ((iInt, iString), idx in myArr) {
  // ...
}
```

## 0.2 Lambdas

Multiple levels of conciseness for different circumstances. Lambdas would create closures.

```belte
// The 4 following lambdas are functionally equivalent
int(int) squareFunc = x => x * x;
int(int) squareFunc = (x) => x * x;
int(int) squareFunc = int(int x) => x * x;
int(int) squareFunc = int(int x) => { return x * x; };

int s = squareFunc(10); // s = 100
```

The more verbose variants are used in situations where the types cannot be inferred:

```belte
var squareFunc = x => x * x; // What is the type of x?
var squareFunc = int(int x) => x * x; // Gives enough information to deduce the type of squareFunc as `int(int)`
```

## 0.3 Interfaces

Interfaces make generics much more useful. These would be C#-style interfaces with the only notable difference being
a non-combined base-list for classes (Java-style):

*C#*

```cs
class A : BaseClass, IInterface1, IInterface2 { }
```

*Belte*

```belte
class A extends BaseClass implements Interface1, Interface2 { }
```

## 0.4 Verse-Style Concurrency/Async

Unreal 6 comes with a new scripting language called "Verse". I believe its approach to concurrency/async code execution
is much cleaner that C#'s design.

### 0.4.1 `sync`

This relies on [tuples](#01-tuples). A `sync` block is a list of operations that run concurrently (this means shared
state must be thread-safe or locked, and the order in which operations finish is nondeterministic). In its most verbose
form, each nested block indicates a concurrent operation:

```belte
var (result1, result2) = sync {
  {
    return SomeOperation1();
  }
  {
    return SomeOperation2();
  }
}
```

Where the return value of `SomeOperation1` is placed in `result1` and the return value of `SomeOperation2` is placed in
`result2`. Both operations are run in parallel. Any statements inside of a nested block run linearly:

```belte
var (result1, result2) = sync {
  {
    SomeCall1();
    SomeCall2();
    return SomeOperation1();
  }
  {
    return SomeOperation2();
  }
}
```

Instead of blocks, an expression can be used:

```belte
var (result1, result2) = sync {
  SomeOperation1();
  SomeOperation2();
}
```

#### 0.4.1.1 `async` and `suspend`

An operation in a `sync` block can be any ordinary code, but `async` code is also supported. A block marked `async`
supports suspending that task in a non-blocking way.

To sleep a task for a certain amount of time, a `suspend` statement can be used which sleeps a specified number of
seconds:

```belte
var (result1, result2) = sync {
  async {
    SomeCall1();
    suspend 3.5; // suspends this task for 3.5 seconds
    return SomeOperation1();
  }
  SomeOperation2();
}
```

A method can also be marked `async`, in which case an `await` expression can be used:

```belte
var (result1, result2) = sync {
  await SomeAsyncOperation1();
  await SomeAsyncOperation2();
}
```

### 0.4.2 `race`

The `race` block executes a list of `cancellable` methods and returns the result of the first one that succeeds
(non-except):

```belte
var result = race {
  SomeMethod1();
  SomeMethod2();
  SomeMethod3();
}
```

The other methods are cancelled once one of them succeeds. This is done by hidden checking of cancellation tokens at
certain points. These points are inserted by the compiler on any method marked `cancellable` hence the requirement of
each `race` method call being a `cancellable` method. The user can insert explicit cancellation checks with
`checkpoint`.

```belte
cancellable int MyMethod() {
  for (var i = 0; i < 10000; i++) {
    checkpoint; // cancellation checked here
    //...
  }
  // ...
}
```

#### 0.4.2.1 `cancel`

To forcibly make an async method cancel and fail in a race, a `cancel` statement can be used.

```belte

var result = race {
  SomeMethod1();
  SomeMethod2();
  SomeMethod3();
}

cancellable int SomeMethod1() {
  if (!TrySomeCall())
    cancel;

  return SomeCall2();
}
```

### 0.4.3 `branch`

The `branch` block runs code in the background while continuing execution immediately past the block. The block is
awaited at scope exit:

```belte
int MyMethod() {
  branch {
    SomeCall1();
    SomeCall2();
    SomeCall3();
  }

  // Work continues immediately, the three calls above run in the background linearly
  var result = SomeOperation();

  return result; // branch code is awaited here
}
```

The branch task cannot be cancelled. To do that, a [`spawn`](#044-spawn) expression should be used instead.

### 0.4.4 `spawn`

The `spawn` expression creates and starts a task. It can be managed or let free (which should only be done in rare
circumstances). A captured task must be a `cancellable` method. Tasks support `.Cancel()` and `.Await()`.

```belte
var task = spawn SomeCancellableOperation();
task.Cancel(); // uses `cancellable` hidden cancellation checks
```

When a task is let free, it can contain any call because it can't be cancelled. It always won't be awaited at scope
exit:

```belte
void MyMethod() {
  spawn free SomeNonCancellableOperation();
} // task keeps running even after `MyMethod` returns
```

### 0.4.5 `lock`

Many structures are not concurrency-safe. In such a case, a `lock` can be used to ensure only the task with the lock is
reading or writing the captured value:

```belte
lock (myList) {
  myList.Append(10);
}
```

## 0.5 LSP

A Language Server Protocol would massively boost ergonomics by offering intellisense to code editors. This requires a
semantic model which is a large undertaking. This change is mostly behind the scenes, but know its coming (eventually)!

## 0.6 Has Constraints

An alternative to interfaces. Instead of using virtual dispatch at runtime, `has` constraints would allow static
duck-typing at compile-time which improves performance. The main complication is integrating this with the .NET runtime,
which is currently unsolved.

```belte
void M<type T>(T param) where { T has void SomeMethod(int); } {
  param.SomeMethod(10);
}
```

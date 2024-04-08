# 4 Classes and Objects

- [4.1](#41-classes) Classes
- [4.2](#42-members) Members
- [4.3](#43-modifiers) Modifiers
- [4.4](#44-constructors) Constructors

## 4.1 Classes

Classes are structures that contain data and functionality in the form of fields and methods. Fields are similar to
variables, and methods are similar to functions both in syntax and functionality.

Classes are declared using the `class` keyword:

```belte
class MyClass {
  // Members
}
```

An object is an instance of a class, and can be created using the `new` keyword:

```belte
new MyClass();
```

## 4.2 Members

Members of an instance can be accessed externally using a member accession:

```belte
myInstance.member;
```

Members can also be accessed internally (i.e. inside of a method) using the `this` keyword:

```belte
this.member;
```

If no local symbol names conflict, the `this` keyword can be omitted:

```belte
member;
```

### 4.2.1 Fields

Fields are similar to variables or constants. They are declared as such:

```belte
class MyClass {
  int myField1;
  string myField2 = "Starting Value";
  // etc.
}
```

Fields have the same flexibility as traditional variables and constants, meaning they can be any type including another
class:

```belte
class A { }

class B {
  A myField = new A();
}
```

### 4.2.2 Methods

Methods are similar to functions. They are declared as such:

```belte
class MyClass {
  void MyMethod(int param1) {
    // Body
  }
}
```

When accessing a method, it must be called:

```belte
var myInstance = new MyClass();
myInstance.MyMethod();
```

## 4.3 Modifiers

Class members are instance members by default, meaning they require an instance to access. With the `static` and `const`
keywords methods and fields respectively can be accessed without an instance.

```belte
class MyClass {
  const int a = 3;

  static void B() { }
}

var myA = MyClass.a;
MyClass.B();
```

## 4.4 Constructors

When creating an object, values can be passed to modify the creation process. By default, no values are passed:

```belte
class MyClass { }

new MyClass();
```

But with a constructor data can be allowed to be passed when creating the object. Constructors are declared similar to
methods, but do not have a return type and must have the same name as the class.

```belte
class MyClass {
  int myField;

  MyClass(int a) {
    this.myField = a;
  }
}

new MyClass(4);
```

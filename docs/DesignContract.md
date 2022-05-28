# Design by Contract

Belte supports and encourages contract programming by making it native and very easy with built-in types. This also
allows built-in types to be more versatile, and reduces the amount of types. For example, instead of having an unsigned
int, you can add a bound to an int to make it greater than or equal to 0.

Declaring an unsigned int in C vs Belte:

```cpp
int myInt;
unsigned int myUint;
```

```belte
int myInt;
int<0> myUint;
```

Some restrictions you can add to all types are nullability and simple evaluation checks. For example, you can explicitly
declare that a type cannot be null (as they can be by default). You can also give a type an expression that must
evaluate to true for every value passing into it.

```belte
int<T => T > 2 && T < 10> myVar;
myVar = 8;
myVar = 1; // throws
```

## Integers

Integers have 2 bound options, minimum and maximum. The most common use of this is to make an unsigned int: `int<0>`.
This feature is very useful in other scenarios as well. For example, the C# `CompareTo()` method of IComparable returns
an int. The return value can mean less than, equal to, or greater than. Instead of returning a normal int, Belte would
return an `int<-1,1>` to restrict it to 3 values, because that is all that is needed (-1 being the minimum with 1 the
maximum).

## Strings

Similar to integers, strings have a minimum and maximum bound for length, as well as an exact length and regex match. To
create a `char` (which there is no type for) you would declare `string<1>` (constant length 1).

As mentioned you can enforce a regular expression where the string always needs to comply, else an error is thrown.

```belte
string<match="^(\+\d{1,2}\s)?\(?\d{3}\)?[\s.-]?\d{3}[\s.-]?\d{4}$"> phone_number;

phone_number = "123-456-7890";
phone_number = "(123) 456-7890";
phone_number = "123 456 7890";
phone_number = "123.456.7890";
phone_number = "Hello, world!"; // error
```

In this example, the string `phone_number` must always be a 10-digit phone number.

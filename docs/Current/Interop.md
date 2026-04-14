# 8 .NET DLL References

> Note: This feature is experimental and may be unstable

> To call code in non-.NET (unmanaged) DLLs, consider using [externs](LowLevelFeatures.md#67-extern-methods)

- [8.1](#81-referencing-net-dlls) Referencing .NET DLLs
- [8.2](#82-feature-workarounds) Feature Workarounds

## 8.1 Referencing .NET DLLs

.NET DLLs can be referenced in a limited manor. References only work if the imported metadata can be represented in
native Belte. For example, referencing a C# property from a DLL will not work as Belte does not currently have
properties. (Though for many features, [workarounds exist](#82-feature-workarounds).)

Because of this limitation, the safest way to interact with imported references is through static methods defined in the
DLL.

To specify a reference, the [`--ref=<file>` command-line argument](../Buckle.md#--reffile---referencefile) can be used.

For example:

```bash
buckle MyProgram.blt -d --ref="C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/10.0.3/ref/net10.0/System.Runtime.Intrinsics.dll" -o MyProgram.exe
```

This would allow you to interact with the `System.Runtime.Intrinsics` and constituent namespaces directly. Here is an
example of adding two `Vector128<float32>` structs together using SIMD:

```belte
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;

var a = Vector128.Create((float32)1, 2, 3, 4);
var b = Vector128.Create((float32)1, 2, 3, 4);
var c = Sse.Add(a, b);

Console.PrintLine(c);
```

Output:

```txt
<2, 4, 6, 8>
```

## 8.2 Feature Workarounds

There are a few situations where you can still call C# (or other .NET) code even if certain features are not implemented
yet.

### 8.2.1 Properties

Interacting with properties is very straightforward. Properties define a getter and setter method that can be called
directly:

```belte
using System.Collections.Generic;

var a = new List<int>();
a.Add(3);

var b = a.get_Item(0);
a.set_Item(0, 5);
```

Where `a.get_Item(0)` is equivalent to `a[0]` and `a.set_Item(0, 5)` is equivalent to `a[0] = 5` in C#.

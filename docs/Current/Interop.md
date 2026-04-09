# 8 .NET DLL References

> Note: This feature is experimental and may be unstable

.NET DLLs can be referenced in a limited manor. References only work if the imported metadata can be represented in
native Belte. For example, referencing a C# property from a DLL will not work as Belte does not currently have
properties.

> To call code in non-.NET (unmanaged) DLLs, consider using [externs](LowLevelFeatures.md#67-extern-methods)

Because of this limitation, the safest way to interact with imported references is through static methods defined in the
DLL.

To specify a reference, the [`--ref=<file>` command-line argument](../Buckle.md#--reffile---referencefile) can be used.
The [`-d` flag](../Buckle.md#-d---dotnet) must also be used to specify that the compiler should emit a .NET executable.

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

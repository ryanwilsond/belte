using System;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Syntax;
using Diagnostics;
using Xunit;

namespace Buckle.Tests.CodeAnalysis;

public sealed class CSharpEmitterTests {
    [Theory]
    [InlineData(
        /* Belte Code */
        @"
void Main() { }
        ",
        /* C# Code */
        @"
using System;
using System.Collections.Generic;

namespace CSharpEmitterTests;

public static class Program {

    public static void Main() {
        return;
    }

}
        "
    )]
    [InlineData(
        /* Belte Code */
        @"
int Main() {
    ;
    var a = 1;
    a += (2 + 3);
    return a;
}
        ",
        /* C# Code */
        @"
using System;
using System.Collections.Generic;

namespace CSharpEmitterTests;

public static class Program {

    public static int Main() {
        Nullable<int> a = 1;
        a += 5;
        return (a) ?? 0;
    }

}
        "
    )]
    [InlineData(
        /* Belte Code */
        @"
int a = 3;
PrintLine(a);
        ",
        /* C# Code */
        @"
using System;
using System.Collections.Generic;

namespace CSharpEmitterTests;

public static class Program {

    public static void Main() {
        Nullable<int> a = 3;
        Console.WriteLine((object)a);
        return;
    }

}
        "
    )]
    [InlineData(
        /* Belte Code */
        @"",
        /* C# Code */
        @"
using System;
using System.Collections.Generic;

namespace CSharpEmitterTests;

public static class Program {

    public static void Main() { }

}
        "
    )]
    [InlineData(
        /* Belte Code */
        @"
struct TypeTests {
    int int1;
    [NotNull]int int2;
    int[] int3;
    [NotNull]int[] int4;

    bool bool1;
    [NotNull]bool bool2;
    bool[] bool3;
    [NotNull]bool[] bool4;

    string string1;
    [NotNull]string string2;
    string[] string3;
    [NotNull]string[] string4;

    decimal decimal1;
    [NotNull]decimal decimal2;
    decimal[] decimal3;
    [NotNull]decimal[] decimal4;

    any any1;
    [NotNull]any any2;
    any[] any3;
    [NotNull]any[] any4;
}
        ",
        /* C# Code */
        @"
using System;
using System.Collections.Generic;

namespace CSharpEmitterTests;

public static class Program {

    public class TypeTests {
        public Nullable<int> int1;
        public int int2;
        public List<Nullable<int>> int3;
        public List<int> int4;
        public Nullable<bool> bool1;
        public bool bool2;
        public List<Nullable<bool>> bool3;
        public List<bool> bool4;
        public string string1;
        public string string2;
        public List<string> string3;
        public List<string> string4;
        public Nullable<double> decimal1;
        public double decimal2;
        public List<Nullable<double>> decimal3;
        public List<double> decimal4;
        public object any1;
        public object any2;
        public List<object> any3;
        public List<object> any4;
    }

    public static void Main() { }

}
        "
    )]
    [InlineData(
        /* Belte Code */
        @"
int Main() {
    var a = 1;
    var b = -1;
    var c = 2 + 3 + a;
    var d = 2 + 3 * a;
    var e = (2 + 3) * a;
    var f = a ?? 3;
    a += (2 + 3);
    var bo = a > 4;
    PrintLine(bo ? 3 : 65);

    return a * 10;
}
        ",
        /* C# Code */
        @"
using System;
using System.Collections.Generic;

namespace CSharpEmitterTests;

public static class Program {

    public static int Main() {
        Nullable<int> a = 1;
        Nullable<int> b = -1;
        Nullable<int> c = (Nullable<int>)(a.HasValue ? (5 + a.Value) : null);
        Nullable<int> d = (Nullable<int>)(2 + (a.HasValue ? (3 * a.Value) : null));
        Nullable<int> e = (Nullable<int>)(a.HasValue ? (5 * a.Value) : null);
        Nullable<int> f = (a.HasValue ? a.Value : 3);
        a += 5;
        Nullable<bool> bo = (Nullable<bool>)(a.HasValue ? (a.Value > 4) : null);
        Console.WriteLine((object)(((bo) ?? throw new NullReferenceException()) ? 3 : 65));
        return ((Nullable<int>)(a.HasValue ? (a.Value * 10) : null)) ?? 0;
    }

}
        "
    )]
    [InlineData(
        /* Belte Code */
        @"
void Main() {
    var a = 0;

    if (a == 0) {
        a = 10;
    } else {
        a = 5;
    }

    int result = 1;

    for (int i=0; i<=10; i++) {
        result *= 2;
        break;
    }

    PrintLine(result + a);

    int x = 0;

    while (x <= 10) {
        a++;
        continue;
    }

    do {
        result++;
    } while (result < 20);

    try {
        var b = 5 / a;
    } catch {
        var b = 6;
    }
}
        ",
        /* C# Code */
        @"
using System;
using System.Collections.Generic;

namespace CSharpEmitterTests;

public static class Program {

    public static void Main() {
        Nullable<int> a = 0;
        if ((((Nullable<bool>)(a.HasValue ? (a.Value == 0) : null)) ?? throw new NullReferenceException())) {
            a = 10;
        }
        else {
            a = 5;
        }

        Nullable<int> result = 1;
        for (Nullable<int> i = 0;
            (((Nullable<bool>)(i.HasValue ? (i.Value <= 10) : null)) ?? throw new NullReferenceException()); i++) {
            result *= 2;
            break;
        }

        Console.WriteLine((object)((result.HasValue && a.HasValue) ? (result.Value + a.Value) : null));
        Nullable<int> x = 0;
        while ((((Nullable<bool>)(x.HasValue ? (x.Value <= 10) : null)) ?? throw new NullReferenceException())) {
            a++;
            continue;
        }

        do {
            result++;
        }
        while ((((Nullable<bool>)(result.HasValue ? (result.Value < 20) : null)) ?? throw new NullReferenceException()));

        try {
            Nullable<int> b = (Nullable<int>)(a.HasValue ? (5 / a.Value) : null);
        }
        catch {
            Nullable<int> b = 6;
        }
        return;
    }

}
        "
    )]
    [InlineData(
        /* Belte Code */
        @"
void Main() {
    int[] a = {1, 2, 3};
    int b = a[0];
    any[] c = {1, 3.3, false};
    int d = (int)c[0];
    b++;
    --d;

    int x = 4;
    ref int y = ref x;
    x++;
    PrintLine(y);
    a[1] = 4;
    PrintLine(a[1]);

    int z = 3;
    [NotNull]int g = Value(z);
}
        ",
        /* C# Code */
        @"
using System;
using System.Collections.Generic;

namespace CSharpEmitterTests;

public static class Program {

    public static void Main() {
        List<Nullable<int>> a = new List<Nullable<int>> { 1, 2, 3 };
        Nullable<int> b = a[0];
        List<object> c = new List<object> { 1, 3.3, false };
        Nullable<int> d = (Nullable<int>)c[0];
        b++;
        --d;
        Nullable<int> x = 4;
        ref Nullable<int> y = ref x;
        x++;
        Console.WriteLine((object)y);
        a[1] = 4;
        Console.WriteLine((object)a[1]);
        Nullable<int> z = 3;
        int g = z.Value;
        return;
    }

}
        "
    )]
    [InlineData(
        /* Belte Code */
        @"
void Main() {
    Print(Add(2) + Add(5, 6) + Add(a: 1, b: 5));
    Print(Add(,));
    Print(Add(1,));
    Print(Add(,2));
}

int Add(int a, int b = 3) {
    return a + b;
}
        ",
        /* C# Code */
        @"
using System;
using System.Collections.Generic;

namespace CSharpEmitterTests;

public static class Program {

    public static void Main() {
        Console.Write((object)(Add(1, 5).HasValue ? (((Add(2, 3).HasValue && Add(5, 6).HasValue) ? (Add(2, 3).Value + Add(5, 6).Value) : null) + Add(1, 5).Value) : null));
        Console.Write((object)Add(null, null));
        Console.Write((object)Add(1, null));
        Console.Write((object)Add(null, 2));
        return;
    }

    public static Nullable<int> Add(Nullable<int> a, Nullable<int> b) {
        return ((Nullable<int>)((a.HasValue && b.HasValue) ? (a.Value + b.Value) : null));
    }

}
        "
    )]
    [InlineData(
        /* Belte Code */
        @"
void Main() {
    int a = 5;

    void A(int c) {
        a += c;
    }

    A(5);
}
        ",
        /* C# Code */
        @"
using System;
using System.Collections.Generic;

namespace CSharpEmitterTests;

public static class Program {

    public static void Main() {
        Nullable<int> a = 5;
        @_Main_g__A(5, ref a);
        return;
    }

    public static void @_Main_g__A(Nullable<int> c, ref Nullable<int> a) {
        a += c;
        return;
    }

}
        "
    )]
    [InlineData(
        /* Belte Code */
        @"
struct A {
    int a;
    bool b;
}

void Main() {
    var g = A();
    g.a = 5;
    bool c = g.b;
    bool d = c is null;

    A h;

    int j = h?.a;
    PrintLine(j is null);
}
        ",
        /* C# Code */
        @"
using System;
using System.Collections.Generic;

namespace CSharpEmitterTests;

public static class Program {

    public class A {
        public Nullable<int> a;
        public Nullable<bool> b;
    }

    public static void Main() {
        A g = (A)new A();
        g.a = 5;
        Nullable<bool> c = g.b;
        Nullable<bool> d = (Nullable<bool>)(!c.HasValue);
        A h = null;
        Nullable<int> j = (((h) is not null) ? h.a : null);
        Console.WriteLine((object)(!j.HasValue));
        return;
    }

}
        "
    )]
    public void Emitter_EmitsCorrectly(string text, string expectedText) {
        AssertText(text, expectedText.Trim() + Environment.NewLine);
    }

    private void AssertText(string text, string expectedText) {
        var syntaxTree = SyntaxTree.Parse(text);
        var compilation = Compilation.Create(true, syntaxTree);
        var result = compilation.EmitToString(BuildMode.CSharpTranspile, "CSharpEmitterTests", false);

        Assert.Empty(compilation.diagnostics.FilterOut(DiagnosticType.Warning).ToArray());
        Assert.Equal(expectedText, result);
    }
}

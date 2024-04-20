using System;
using Xunit;
using static Buckle.Tests.Assertions;

namespace Buckle.Tests.CodeAnalysis.Emitting;

/// <summary>
/// Tests on the <see cref="Buckle.CodeAnalysis.Emitting.ILEmitter" /> and
/// <see cref="Buckle.CodeAnalysis.Emitting.CSharpEmitter" /> classes.
/// </summary>
public sealed class EmitterTests {
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

namespace EmitterTests;

public static class Program {

    public static void Main() {
        return;
    }

}
        ",
        /* IL Code */
        @"
<Program>$ {
    System.Void <Program>$::Main() {
        IL_0000: ret
    }
}
        "
    )]
    [InlineData(
        /* Belte Code */
        @"
int Main() {
    return 1;
}
        ",
        /* C# Code */
        @"
using System;
using System.Collections.Generic;

namespace EmitterTests;

public static class Program {

    public static int Main() {
        return 1;
    }

}
        ",
        /* IL Code */
        @"
<Program>$ {
    System.Int32 <Program>$::Main() {
        IL_0000: ldc.i4.1
        IL_0001: ret
    }
}
        "
    )]
    [InlineData(
        /* Belte Code */
        @"
int Main() {
    return null;
}
        ",
        /* C# Code */
        @"
using System;
using System.Collections.Generic;

namespace EmitterTests;

public static class Program {

    public static int Main() {
        return 0;
    }

}
        ",
        /* IL Code */
        @"
<Program>$ {
    System.Int32 <Program>$::Main() {
        IL_0000: ldc.i4.0
        IL_0001: ret
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

namespace EmitterTests;

public static class Program {

    public static int Main() {
        Nullable<int> a = 1;
        a += 5;
        return a ?? 0;
    }

}
        ",
        /* IL Code */
        @"
        "
    )]
    [InlineData(
        /* Belte Code */
        @"
int a = 3;
Console.PrintLine(a);
        ",
        /* C# Code */
        @"
using System;
using System.Collections.Generic;

namespace EmitterTests;

public static class Program {

    public static void Main() {
        Nullable<int> a = 3;
        global::System.Console.WriteLine((object)a);
        return;
    }

}
        ",
        /* IL Code */
        @"
        "
    )]
    [InlineData(
        /* Belte Code */
        @"",
        /* C# Code */
        @"
using System;
using System.Collections.Generic;

namespace EmitterTests;

public static class Program {

    public static void Main() { }

}
        ",
        /* IL Code */
        @"
        "
    )]
    [InlineData(
        /* Belte Code */
        @"
class TypeTests {
    int int1;
    int! int2;
    int[] int3;
    int[]! int4;

    bool bool1;
    bool! bool2;
    bool[] bool3;
    bool[]! bool4;

    string string1;
    string! string2;
    string[] string3;
    string[]! string4;

    decimal decimal1;
    decimal! decimal2;
    decimal[] decimal3;
    decimal[]! decimal4;

    any any1;
    any! any2;
    any[] any3;
    any[]! any4;
}
        ",
        /* C# Code */
        @"
using System;
using System.Collections.Generic;

namespace EmitterTests;

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

        public TypeTests() {
            return;
        }

    }

    public static void Main() { }

}
        ",
        /* IL Code */
        @"
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
    Console.PrintLine(bo ? 3 : 65);

    return a * 10;
}
        ",
        /* C# Code */
        @"
using System;
using System.Collections.Generic;

namespace EmitterTests;

public static class Program {

    public static int Main() {
        Nullable<int> a = 1;
        Nullable<int> b = -1;
        int temp0 = 5;
        Nullable<int> c = (a.HasValue ? (Nullable<int>)(temp0 + a.Value) : null);
        Nullable<int> temp1 = (a.HasValue ? (Nullable<int>)(3 * a.Value) : null);
        Nullable<int> d = (temp1.HasValue ? (Nullable<int>)(2 + temp1.Value) : null);
        int temp2 = 5;
        Nullable<int> e = (a.HasValue ? (Nullable<int>)(temp2 * a.Value) : null);
        Nullable<int> f = (a.HasValue ? a.Value : 3);
        a += 5;
        Nullable<bool> bo = (a.HasValue ? (Nullable<bool>)(a.Value > 4) : null);
        global::System.Console.WriteLine((object)(((bo) ?? throw new NullReferenceException()) ? 3 : 65));
        return (a.HasValue ? (Nullable<int>)(a.Value * 10) : null) ?? 0;
    }

}
        ",
        /* IL Code */
        @"
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

    for (int i = 0; i <= 10; i++) {
        result *= 2;
        break;
    }

    Console.PrintLine(result + a);

    int x = 0;

    while (x <= 10) {
        x++;
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

namespace EmitterTests;

public static class Program {

    public static void Main() {
        Nullable<int> a = 0;
        if (((a.HasValue ? (Nullable<bool>)(a.Value == 0) : null) ?? throw new NullReferenceException())) {
            a = 10;
        }
        else {
            a = 5;
        }

        Nullable<int> result = 1;
        for (Nullable<int> i = 0;
            ((i.HasValue ? (Nullable<bool>)(i.Value <= 10) : null) ?? throw new NullReferenceException()); i++) {
            result *= 2;
            break;
        }

        global::System.Console.WriteLine((object)((result.HasValue && a.HasValue) ? (Nullable<int>)(result.Value + a.Value) : null));
        Nullable<int> x = 0;
        while (((x.HasValue ? (Nullable<bool>)(x.Value <= 10) : null) ?? throw new NullReferenceException())) {
            x++;
            continue;
        }

        do {
            result++;
        }
        while (((result.HasValue ? (Nullable<bool>)(result.Value < 20) : null) ?? throw new NullReferenceException()));

        try {
            Nullable<int> b = (a.HasValue ? (Nullable<int>)(5 / a.Value) : null);
        }
        catch {
            Nullable<int> b = 6;
        }
        return;
    }

}
        ",
        /* IL Code */
        @"
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
    Console.PrintLine(y);
    a[1] = 4;
    Console.PrintLine(a[1]);

    int z = 3;
    int! g = Value(z);
}
        ",
        /* C# Code */
        @"
using System;
using System.Collections.Generic;

namespace EmitterTests;

public static class Program {

    public static void Main() {
        List<Nullable<int>> a = new List<Nullable<int>> { 1, 2, 3 };
        Nullable<int> b = a[0];
        List<object> c = new List<object> { 1, 3.3, false };
        Nullable<int> d = (Nullable<int>)global::System.Convert.ToInt32(c[0]);
        b++;
        --d;
        Nullable<int> x = 4;
        ref Nullable<int> y = ref x;
        x++;
        global::System.Console.WriteLine((object)y);
        a[1] = 4;
        global::System.Console.WriteLine((object)a[1]);
        Nullable<int> z = 3;
        int g = z.Value;
        return;
    }

}
        ",
        /* IL Code */
        @"
        "
    )]
    [InlineData(
        /* Belte Code */
        @"
void Main() {
    Console.Print(Add(2) + Add(5, 6) + Add(a: 1, b: 5));
    Console.Print(Add(,));
    Console.Print(Add(1,));
    Console.Print(Add(,2));
}

int Add(int a, int b = 3) {
    return a + b;
}
        ",
        /* C# Code */
        @"
using System;
using System.Collections.Generic;

namespace EmitterTests;

public static class Program {

    public static void Main() {
        Nullable<int> temp0 = Add(2, 3);
        Nullable<int> temp1 = Add(5, 6);
        Nullable<int> temp2 = ((temp0.HasValue && temp1.HasValue) ? (Nullable<int>)(temp0.Value + temp1.Value) : null);
        Nullable<int> temp3 = Add(1, 5);
        global::System.Console.Write((object)((temp2.HasValue && temp3.HasValue) ? (Nullable<int>)(temp2.Value + temp3.Value) : null));
        global::System.Console.Write((object)Add(null, null));
        global::System.Console.Write((object)Add(1, null));
        global::System.Console.Write((object)Add(null, 2));
        return;
    }

    public static Nullable<int> Add(Nullable<int> a, Nullable<int> b) {
        return ((a.HasValue && b.HasValue) ? (Nullable<int>)(a.Value + b.Value) : null);
    }

}
        ",
        /* IL Code */
        @"
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

namespace EmitterTests;

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
        ",
        /* IL Code */
        @"
        "
    )]
    [InlineData(
        /* Belte Code */
        @"
class A {
    int a;
    bool b;
}

void Main() {
    var g = new A();
    g.a = 5;
    bool c = g.b;
    bool d = c is null;

    A h;

    int j = h?.a;
    Console.PrintLine(j is null);
}
        ",
        /* C# Code */
        @"
using System;
using System.Collections.Generic;

namespace EmitterTests;

public static class Program {

    public class A {
        public Nullable<int> a;
        public Nullable<bool> b;

        public A() {
            return;
        }

    }

    public static void Main() {
        A g = new A();
        g.a = 5;
        Nullable<bool> c = g.b;
        Nullable<bool> d = !c.HasValue;
        A h = null;
        Nullable<int> j = (h is not null ? (Nullable<int>)h.a : null);
        global::System.Console.WriteLine((object)!j.HasValue);
        return;
    }

}
        ",
        /* IL Code */
        @"
        "
    )]
    [InlineData(
        /* Belte Code */
        @"
var max = (int)Console.Input();
var randInt = RandInt(max);
        ",
        /* C# Code */
        @"
using System;
using System.Collections.Generic;

namespace EmitterTests;

public static class Program {

    public static void Main() {
        Nullable<int> max = (Nullable<int>)global::System.Convert.ToInt32(global::System.Console.ReadLine());
        Nullable<int> randInt = ((Func<int>)(() => { var random = new global::System.Random(); var temp = max; return temp.HasValue ? random.Next(temp.Value) : random.Next(); }))();
        return;
    }

}
        ",
        /* IL Code */
        @"
        "
    )]
    [InlineData(
        /* Belte Code */
        @"
class A {
    void Test() { }
}

var myA = new A();
myA.Test();
        ",
        /* C# Code */
        @"
using System;
using System.Collections.Generic;

namespace EmitterTests;

public static class Program {

    public class A {

        public A() {
            return;
        }

        public void Test() {
            return;
        }

    }

    public static void Main() {
        A myA = new A();
        myA.Test();
        return;
    }

}
        ",
        /* IL Code */
        @"
        "
    )]
#pragma warning disable xUnit1026
    public void Emitter_Emits_CorrectText(string text, string expectedCSharpText, string expectedILText) {
        AssertText(text, expectedCSharpText.Trim() + Environment.NewLine, BuildMode.CSharpTranspile);
        // TODO Fix Mono.Cecil bug that is preventing further IL Emitter development
        // AssertText(text, expectedILText.Trim() + Environment.NewLine, BuildMode.Dotnet);
    }
#pragma warning restore xUnit1026
}

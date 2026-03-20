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

namespace EmitterTests {

    public static class Program {

        public static void Main() {
        }

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

namespace EmitterTests {

    public static class Program {

        public static int Main() {
            return 1;
        }

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

namespace EmitterTests {

    public static class Program {

        public static int Main() {
            return 0;
        }

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

namespace EmitterTests {

    public static class Program {

        public static int Main() {
            global::System.Nullable<int> a = 1;
            a += 5;
            return a ?? 0;
        }

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

namespace EmitterTests {

    public static class Program {

        public static void Main() {
            global::System.Nullable<int> a = 3;
            global::System.Console.WriteLine(a);
        }

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

namespace EmitterTests {

    public static class Program {

        public static void Main() { }

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
lowlevel class TypeTests {
    int int1;
    int! int2 = 0;
    int[] int3;
    int[]! int4 = { 0 };

    bool bool1;
    bool! bool2 = true;
    bool[] bool3;
    bool[]! bool4 = { true };

    string string1;
    string! string2 = """";
    string[] string3;
    string[]! string4 = { """" };

    decimal decimal1;
    decimal! decimal2 = 0;
    decimal[] decimal3;
    decimal[]! decimal4 = { 0 };

    any any1;
    any! any2 = 0;
    any[] any3;
    any[]! any4 = { 0 };
}
        ",
        /* C# Code */
        @"
using System;

namespace EmitterTests {

    public static class Program {

        public class TypeTests : Object {
            private global::System.Nullable<int> int1;
            private int int2;
            private global::System.Nullable<int>[]? int3;
            private int[]? int4;
            private global::System.Nullable<bool> bool1;
            private bool bool2;
            private global::System.Nullable<bool>[]? bool3;
            private bool[]? bool4;
            private string string1;
            private string string2;
            private string[]? string3;
            private string[]? string4;
            private global::System.Nullable<double> decimal1;
            private double decimal2;
            private global::System.Nullable<double>[]? decimal3;
            private double[]? decimal4;
            private object any1;
            private object any2;
            private object[]? any3;
            private object[]? any4;

            public TypeTests() : base() {
                this.int2 = 0;
                this.int4 = new int[]? { 0 };
                this.bool2 = true;
                this.bool4 = new bool[]? { true };
                this.string2 = """";
                this.string4 = new string[]? { """" };
                this.decimal2 = 0;
                this.decimal4 = new int[]? { 0 };
                this.any2 = 0;
                this.any4 = new int[]? { 0 };
            }

        }

        public static void Main() { }

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

namespace EmitterTests {

    public static class Program {

        public static int Main() {
            global::System.Nullable<int> a = 1;
            global::System.Nullable<int> b = -1;
            global::System.Nullable<int> c = (a.HasValue ? (global::System.Nullable<int>)(5 + a.Value) : null);
            global::System.Nullable<int> temp0 = (a.HasValue ? (global::System.Nullable<int>)(3 * a.Value) : null);
            global::System.Nullable<int> d = (temp0.HasValue ? (global::System.Nullable<int>)(2 + temp0.Value) : null);
            global::System.Nullable<int> e = (a.HasValue ? (global::System.Nullable<int>)(5 * a.Value) : null);
            global::System.Nullable<int> f = (a.HasValue ? a.Value : 3);
            a += 5;
            global::System.Nullable<bool> bo = (a.HasValue ? (global::System.Nullable<bool>)(a.Value > 4) : null);
            global::System.Console.WriteLine((((bo) ?? throw new global::System.NullReferenceException()) ? 3 : 65));
            return (a.HasValue ? (global::System.Nullable<int>)(a.Value * 10) : null) ?? 0;
        }

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

namespace EmitterTests {

    public static class Program {

        public static void Main() {
            global::System.Nullable<int> a = 0;
            if (((a.HasValue ? (global::System.Nullable<bool>)(a.Value == 0) : null) ?? throw new global::System.NullReferenceException())) {
                a = 10;
            }
            else {
                a = 5;
            }

            global::System.Nullable<int> result = 1;
            for (global::System.Nullable<int> i = 0;
                ((i.HasValue ? (global::System.Nullable<bool>)(i.Value <= 10) : null) ?? throw new global::System.NullReferenceException()); i++) {
                result *= 2;
                break;
            }

            global::System.Console.WriteLine(((result.HasValue && a.HasValue) ? (global::System.Nullable<int>)(result.Value + a.Value) : null));
            global::System.Nullable<int> x = 0;
            while (((x.HasValue ? (global::System.Nullable<bool>)(x.Value <= 10) : null) ?? throw new global::System.NullReferenceException())) {
                x++;
                continue;
            }

            do {
                result++;
            }
            while (((result.HasValue ? (global::System.Nullable<bool>)(result.Value < 20) : null) ?? throw new global::System.NullReferenceException()));

            try {
                global::System.Nullable<int> b = (a.HasValue ? (global::System.Nullable<int>)(5 / a.Value) : null);
            }
            catch {
                global::System.Nullable<int> b = 6;
            }
        }

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
lowlevel void Main() {
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
    int! g = z!;
}
        ",
        /* C# Code */
        @"
using System;

namespace EmitterTests {

    public static class Program {

        public static void Main() {
            global::System.Nullable<int>[]? a = new global::System.Nullable<int>[]? { 1, 2, 3 };
            global::System.Nullable<int> b = a[0];
            object[]? c = new object[]? { 1, 3.3, false };
            global::System.Nullable<int> d = global::System.Convert.ToInt32(c[0]);
            b++;
            --d;
            global::System.Nullable<int> x = 4;
            ref global::System.Nullable<int> y = ref x;
            x++;
            global::System.Console.WriteLine(y);
            a[1] = 4;
            global::System.Console.WriteLine(a[1]);
            global::System.Nullable<int> z = 3;
            int g = z.Value;
        }

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

namespace EmitterTests {

    public static class Program {

        public static void Main() {
            global::System.Nullable<int> temp0 = Add(2, 3);
            global::System.Nullable<int> temp1 = Add(5, 6);
            global::System.Nullable<int> temp2 = ((temp0.HasValue && temp1.HasValue) ? (global::System.Nullable<int>)(temp0.Value + temp1.Value) : null);
            global::System.Nullable<int> temp3 = Add(1, 5);
            global::System.Console.Write(((temp2.HasValue && temp3.HasValue) ? (global::System.Nullable<int>)(temp2.Value + temp3.Value) : null));
            global::System.Console.Write(Add(null, null));
            global::System.Console.Write(Add(1, null));
            global::System.Console.Write(Add(null, 2));
        }

        global::System.Nullable<int> Add(global::System.Nullable<int> a, global::System.Nullable<int> b) {
            return ((a.HasValue && b.HasValue) ? (global::System.Nullable<int>)(a.Value + b.Value) : null);
        }

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

namespace EmitterTests {

    public static class Program {

        public static void Main() {
            global::System.Nullable<int> a = 5;
            @_Main_g__A(5, ref a);
        }

        void @_Main_g__A(global::System.Nullable<int> c, ref global::System.Nullable<int> a) {
            a += c;
        }

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
    public int a;
    public bool b;
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

namespace EmitterTests {

    public static class Program {

        public class A : Object {
            public global::System.Nullable<int> a;
            public global::System.Nullable<bool> b;

            public A() : base() {
            }

        }

        public static void Main() {
            A g = new A();
            g.a = 5;
            global::System.Nullable<bool> c = g.b;
            global::System.Nullable<bool> d = !c.HasValue;
            A h = null;
            global::System.Nullable<int> j = (h is not null ? (global::System.Nullable<int>)h.a : null);
            global::System.Console.WriteLine(!j.HasValue);
        }

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
var randInt = RandInt(max!);
        ",
        /* C# Code */
        @"
using System;

namespace EmitterTests {

    public static class Program {

        public static void Main() {
            global::System.Nullable<int> max = global::System.Convert.ToInt32(global::System.Console.ReadLine());
            global::System.Nullable<int> randInt = ((global::System.Func<int>)(() => { var random = new global::System.Random(); var temp = max.Value; return random.Next(temp); }))();
        }

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
    public void Test() { }
}

var myA = new A();
myA.Test();
        ",
        /* C# Code */
        @"
using System;

namespace EmitterTests {

    public static class Program {

        public class A : Object {

            public A() : base() {
            }

            public void Test() {
            }

        }

        public static void Main() {
            A myA = new A();
            myA.Test();
        }

    }

}
        ",
        /* IL Code */
        @"
        "
    )]
#pragma warning disable xUnit1026
    public void Emitter_Emits_CorrectText(string text, string expectedCSharpText, string expectedILText) {
        // TODO Fix Mono.Cecil bug that is preventing further IL Emitter development
        // TODO Research combining IL with JIT to allow non-type templates
        // AssertText(text, expectedCSharpText.Trim() + Environment.NewLine, BuildMode.CSharpTranspile);
        // AssertText(text, expectedILText.Trim() + Environment.NewLine, BuildMode.Dotnet);
    }
#pragma warning restore xUnit1026
}

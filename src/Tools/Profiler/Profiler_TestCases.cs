
namespace Profiling;

public static partial class Profiler {
    private static string[] TestCases = [
        "Console.PrintLine(\"Hello, world!\");",
        @"
            var sum = 0;
            for (int! i = 0; i < 1_000_000; i++) sum += i;
            Console.PrintLine(sum);
        ",
        @"
            var p = 1;
            for (int! i = 1; i < 20; i++) p *= i;
            Console.PrintLine(p);
        ",
        @"
            var evens = 0;
            for (int! i = 0; i < 100000; i++) {
                if (i % 2 == 0) evens += 1;
            }
            Console.PrintLine(evens);
        ",
        @"
            int! a = 0;
            for (int! i = 0; i < 10000; i++) {
                if (i % 3 == 0) a += 1;
                else if (i % 5 == 0) a += 2;
                else a -= 1;
            }
            Console.PrintLine(a);
        ",
        @"
            public static class Program {
                static int! fib(int! n) {
                    return n <= 1 ? n : fib(n - 1) + fib(n - 2);
                }
                public static void Main() {
                    Console.PrintLine(fib(20));
                }
            }
        ",
        // TODO Broken
        // @"
        //     public static class Program {
        //         static int! sum(int! acc, int! n) {
        //             return n == 0 ? acc : sum(acc + n, n - 1);
        //         }
        //         public static void Main() {
        //             Console.PrintLine(sum(0, 10000));
        //         }
        //     }
        // ",
        @"
            class A {}
            for (int! i = 0; i < 100000; i++) {
                var x = new A();
            }
            Console.PrintLine(""done"");
        ",
        @"
            class Node {
                public Node next;
            }
            var head = new Node();
            var curr = head;
            for (int! i = 0; i < 10000; i++) {
                curr.next = new Node();
                curr = curr.next!;
            }
            Console.PrintLine(""done"");
        ",
        @"
            var sum = 0;
            for (int! i = 0; i < 100; i++) {
                for (int! j = 0; j < 100; j++) {
                    sum += i * j;
                }
            }
            Console.PrintLine(sum);
        ",
        @"
            int x = null;
            if (x is null) {
                x = 10;
            }
            Console.PrintLine(x);
        ",
        @"
            int a = 5;
            int! b = 3;
            int result = a + b;
            Console.PrintLine(result);
        ",
        @"
            int! AddOne(int x) {
                return x is null ? 1 : x + 1;
            }
            Console.PrintLine(AddOne(null));
            Console.PrintLine(AddOne(41));
        ",
        @"
            int MaybeDivide(int! x, int! y) {
                if (y == 0) return null;
                return x / y;
            }
            Console.PrintLine(MaybeDivide(10, 2));
            Console.PrintLine(MaybeDivide(10, 0));
        ",
        @"
            class A {
                public int! value;
            }

            A a = null;
            if (a is null) {
                a = new A();
                a.value = 42;
            }
            Console.PrintLine(a.value);
        ",
        @"
            int x;
            if (x is null) {
                x = 7;
            }
            Console.PrintLine(x);
        ",
        @"
            int! Square(int! x) {
                return x * x;
            }

            int maybeX = 5;
            if (maybeX isnt null) {
                Console.PrintLine(Square(maybeX!));
            }
        ",
        @"
            int x = null;
            int! y = x is null ? 123 : x!;
            Console.PrintLine(y);
        "
    ];
}

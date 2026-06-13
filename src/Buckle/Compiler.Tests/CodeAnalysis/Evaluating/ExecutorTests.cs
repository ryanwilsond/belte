using Xunit;
using static Buckle.Tests.Assertions;

namespace Buckle.Tests.CodeAnalysis.Evaluating;

/// <summary>
/// Tests for only the <see cref="Buckle.CodeAnalysis.Evaluating.Executor" /> class because they are
/// unsupported by <see cref="Buckle.CodeAnalysis.Evaluating.Evaluator" />.
/// </summary>
public sealed class ExecutorTests {
    [Theory]
    [InlineData(@"
        static int Func1(int a, int b) { return a + b; }
        static int Func2(int a, int b) { return a - b; }

        int Eval(int(int, int)* delegate) {
            return delegate(4, 5);
        }

        return Eval(&Func1);
    ", 9)]
    [InlineData(@"
        static int Func1(int a, int b) { return a + b; }
        static int Func2(int a, int b) { return a - b; }

        int Eval(int(int, int)* delegate) {
            return delegate(4, 5);
        }

        return Eval(&Func2);
    ", -1)]
    [InlineData(@"
        int a = 3;
        int* ptr = &a;
        (*ptr)++;
        return a;
    ", 4)]
    [InlineData(@"
        int32 a = 3;
        int32* ptr = &a;
        (*ptr) = 4;
        return a;
    ", 4)]
    [InlineData(@"
        int32 a = 3;

        il {
            ldc.i4.5;
            stloc.0;
        }

        return a;
    ", 5)]
    [InlineData(@"
        class A {
            public static int32 a = 3;
        }

        il {
            ldc.i4.5;
            stsfld A.a;
        }

        return A.a;
    ", 5)]
    [InlineData(@"
        int32 Func() {
            return 10;
        }

        int32 a = 0;

        il {
            call Func;
            stloc.0;
        }

        return a;
    ", 10)]
    [InlineData(@"
        int32 Func(int32 a, int32 b) {
            int32 ret = 0;

            il {
                ldarg.0;
                ldarg.1;
                add;
                stloc.0;
            }

            return ret;
        }

        return Func(5, 10);
    ", 15)]
    [InlineData(@"
        var myNum = 10;
        var text = cf""num is {myNum}"";
        var str = LowLevel.ReadLPCSTR(text);
        return str;
    ", "num is 10")]
    [InlineData(@"
        var myNum = 10;
        var text = wf""num is {myNum}"";
        var str = LowLevel.ReadLPCWSTR(text);
        return str;
    ", "num is 10")]
    public void Executor_Computes_CorrectValues(string text, object? expectedValue) {
        AssertValue(text, expectedValue, evaluator: false, executor: true);
    }
}

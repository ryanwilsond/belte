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
        int Func1(int a, int b) { return a + b; }
        int Func2(int a, int b) { return a - b; }

        int Eval(int(int, int)* delegate) {
            return delegate(4, 5);
        }

        return Eval(&Func1);
    ", 9)]
    [InlineData(@"
        int Func1(int a, int b) { return a + b; }
        int Func2(int a, int b) { return a - b; }

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
    public void Executor_Computes_CorrectValues(string text, object? expectedValue) {
        AssertValue(text, expectedValue, evaluator: false, executor: true);
    }
}

using System.Collections.Generic;
using Xunit;

namespace Diagnostics.Tests;

/// <summary>
/// Tests on the <see cref="Diagnostics.DiagnosticQueue" /> class.
/// </summary>
public sealed class DiagnosticQueueTests {
    [Fact]
    public void DiagnosticQueue_Any_ReturnsTrue() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticType.Error, ""));

        Assert.True(diagnosticQueue.Any());
    }

    [Fact]
    public void DiagnosticQueue_Any_ReturnsFalse() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();

        Assert.False(diagnosticQueue.Any());
    }

    [Fact]
    public void DiagnosticQueue_GetEnumerator_ReturnsEnumerator() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticType.Error, ""));
        var diagnosticEnumerator = diagnosticQueue.GetEnumerator();

        Assert.Equal(typeof(List<Diagnostic>.Enumerator), diagnosticEnumerator.GetType());
    }

    [Fact]
    public void DiagnosticQueue_ToArray_ReturnsArray() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticType.Error, ""));
        var diagnosticArray = diagnosticQueue.ToArray();

        Assert.Equal(typeof(Diagnostic[]), diagnosticArray.GetType());
        Assert.Single(diagnosticArray);
    }

    [Fact]
    public void DiagnosticQueue_AnyType_ReturnsTrue() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticType.Error, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticType.Error, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticType.Warning, ""));

        Assert.True(diagnosticQueue.Any(DiagnosticType.Error));
    }

    [Fact]
    public void DiagnosticQueue_AnyType_ReturnsFalse() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticType.Error, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticType.Error, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticType.Warning, ""));

        Assert.False(diagnosticQueue.Any(DiagnosticType.Fatal));
    }

    [Fact]
    public void DiagnosticQueue_Push_AddsDiagnostic() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticType.Error, "test diagnostic"));

        Assert.Single(diagnosticQueue.ToArray());
        Assert.Equal("test diagnostic", diagnosticQueue.ToArray()[0].message);
        Assert.Equal(DiagnosticType.Error, diagnosticQueue.ToArray()[0].info.severity);
    }

    [Fact]
    public void DiagnosticQueue_Move_MovesQueue() {
        var diagnosticQueue1 = new DiagnosticQueue<Diagnostic>();
        var diagnosticQueue2 = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue1.Push(new Diagnostic(DiagnosticType.Error, ""));
        diagnosticQueue2.Push(new Diagnostic(DiagnosticType.Error, ""));
        diagnosticQueue1.Move(diagnosticQueue2);

        Assert.Equal(2, diagnosticQueue1.ToArray().Length);
        Assert.Empty(diagnosticQueue2.ToArray());
    }

    [Fact]
    public void DiagnosticQueue_Pop_PopsFirst() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticType.Error, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticType.Warning, ""));
        var diagnostic = diagnosticQueue.Pop();

        Assert.Single(diagnosticQueue.ToArray());
        Assert.Equal(DiagnosticType.Error, diagnostic.info.severity);
    }

    [Fact]
    public void DiagnosticQueue_PopBack_PopsLast() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticType.Error, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticType.Warning, ""));
        var diagnostic = diagnosticQueue.PopBack();

        Assert.Single(diagnosticQueue.ToArray());
        Assert.Equal(DiagnosticType.Warning, diagnostic.info.severity);
    }

    [Fact]
    public void DiagnosticQueue_Clear_RemovesAllDiagnostics() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticType.Error, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticType.Error, ""));
        diagnosticQueue.Clear();

        Assert.Empty(diagnosticQueue.ToArray());
    }

    [Fact]
    public void DiagnosticQueue_AsList_ReturnsList() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticType.Error, ""));
        var diagnosticList = diagnosticQueue.AsList();

        Assert.Equal(typeof(List<Diagnostic>), diagnosticList.GetType());
        Assert.Single(diagnosticList);
    }

    [Fact]
    public void DiagnosticQueue_AsListType_ReturnsListType() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticType.Error, ""));
        var diagnosticList = diagnosticQueue.AsList<Diagnostic>();

        Assert.Equal(typeof(List<Diagnostic>), diagnosticList.GetType());
        Assert.Single(diagnosticList);
    }

    [Fact]
    public void DiagnosticQueue_FilterOut_FiltersOutErrors() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticType.Error, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticType.Error, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticType.Warning, ""));
        var newDiagnosticQueue = diagnosticQueue.FilterOut(DiagnosticType.Error);

        Assert.Single(newDiagnosticQueue.ToArray());
        Assert.Equal(DiagnosticType.Warning, newDiagnosticQueue.ToArray()[0].info.severity);
    }

    [Fact]
    public void DiagnosticQueue_CopyToFront_CopiesQueueToFront() {
        var diagnosticQueue1 = new DiagnosticQueue<Diagnostic>();
        var diagnosticQueue2 = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue1.Push(new Diagnostic(DiagnosticType.Error, ""));
        diagnosticQueue1.Push(new Diagnostic(DiagnosticType.Error, ""));
        diagnosticQueue2.Push(new Diagnostic(DiagnosticType.Warning, ""));
        diagnosticQueue2.Push(new Diagnostic(DiagnosticType.Warning, ""));
        diagnosticQueue1.CopyToFront(diagnosticQueue2);

        Assert.Equal(DiagnosticType.Warning, diagnosticQueue1.ToArray()[0].info.severity);
    }
}

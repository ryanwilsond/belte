using System.Collections.Generic;
using Xunit;

namespace Diagnostics.Tests;

/// <summary>
/// Tests on the <see cref="DiagnosticQueue<T>" /> class.
/// </summary>
public sealed class DiagnosticQueueTests {
    [Fact]
    public void DiagnosticQueue_Any_ReturnsTrue() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Error, ""));

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
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Error, ""));
        var diagnosticEnumerator = diagnosticQueue.GetEnumerator();

        Assert.Equal(typeof(List<Diagnostic>.Enumerator), diagnosticEnumerator.GetType());
    }

    [Fact]
    public void DiagnosticQueue_ToArray_ReturnsArray() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Error, ""));
        var diagnosticArray = diagnosticQueue.ToArray();

        Assert.Equal(typeof(Diagnostic[]), diagnosticArray.GetType());
        Assert.Single(diagnosticArray);
    }

    [Fact]
    public void DiagnosticQueue_AnyType_ReturnsTrue() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Error, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Error, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Warning, ""));

        Assert.True(diagnosticQueue.Any(DiagnosticSeverity.Error));
    }

    [Fact]
    public void DiagnosticQueue_AnyType_ReturnsFalse() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Error, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Error, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Warning, ""));

        Assert.False(diagnosticQueue.Any(DiagnosticSeverity.Fatal));
    }

    [Fact]
    public void DiagnosticQueue_Push_AddsDiagnostic() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Error, "test diagnostic"));

        Assert.Single(diagnosticQueue.ToArray());
        Assert.Equal("test diagnostic", diagnosticQueue.ToArray()[0].message);
        Assert.Equal(DiagnosticSeverity.Error, diagnosticQueue.ToArray()[0].info.severity);
    }

    [Fact]
    public void DiagnosticQueue_Move_MovesQueue() {
        var diagnosticQueue1 = new DiagnosticQueue<Diagnostic>();
        var diagnosticQueue2 = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue1.Push(new Diagnostic(DiagnosticSeverity.Error, ""));
        diagnosticQueue2.Push(new Diagnostic(DiagnosticSeverity.Error, ""));
        diagnosticQueue1.Move(diagnosticQueue2);

        Assert.Equal(2, diagnosticQueue1.ToArray().Length);
        Assert.Empty(diagnosticQueue2.ToArray());
    }

    [Fact]
    public void DiagnosticQueue_Pop_PopsFirst() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Error, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Warning, ""));
        var diagnostic = diagnosticQueue.Pop();

        Assert.Single(diagnosticQueue.ToArray());
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.info.severity);
    }

    [Fact]
    public void DiagnosticQueue_PopBack_PopsLast() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Error, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Warning, ""));
        var diagnostic = diagnosticQueue.PopBack();

        Assert.Single(diagnosticQueue.ToArray());
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.info.severity);
    }

    [Fact]
    public void DiagnosticQueue_Clear_RemovesAllDiagnostics() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Error, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Error, ""));
        diagnosticQueue.Clear();

        Assert.Empty(diagnosticQueue.ToArray());
    }

    [Fact]
    public void DiagnosticQueue_AsList_ReturnsList() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Error, ""));
        var diagnosticList = diagnosticQueue.ToList();

        Assert.Equal(typeof(List<Diagnostic>), diagnosticList.GetType());
        Assert.Single(diagnosticList);
    }

    [Fact]
    public void DiagnosticQueue_AsListType_ReturnsListType() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Error, ""));
        var diagnosticList = diagnosticQueue.ToList<Diagnostic>();

        Assert.Equal(typeof(List<Diagnostic>), diagnosticList.GetType());
        Assert.Single(diagnosticList);
    }

    [Fact]
    public void DiagnosticQueue_Filter_FiltersByErrors() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Error, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Error, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Warning, ""));
        var newDiagnosticQueue = diagnosticQueue.Filter(DiagnosticSeverity.Error);

        Assert.Equal(2, newDiagnosticQueue.ToArray().Length);
    }

    [Fact]
    public void DiagnosticQueue_FilterAbove_FiltersByErrorsAndFatals() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Error, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Fatal, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Fatal, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Warning, ""));
        var newDiagnosticQueue = diagnosticQueue.FilterAbove(DiagnosticSeverity.Error);

        Assert.Equal(3, newDiagnosticQueue.ToArray().Length);
    }

    [Fact]
    public void DiagnosticQueue_FilterOut_FiltersOutErrors() {
        var diagnosticQueue = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Error, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Error, ""));
        diagnosticQueue.Push(new Diagnostic(DiagnosticSeverity.Warning, ""));
        var newDiagnosticQueue = diagnosticQueue.FilterOut(DiagnosticSeverity.Error);

        Assert.Single(newDiagnosticQueue.ToArray());
        Assert.Equal(DiagnosticSeverity.Warning, newDiagnosticQueue.ToArray()[0].info.severity);
    }

    [Fact]
    public void DiagnosticQueue_CopyToFront_CopiesQueueToFront() {
        var diagnosticQueue1 = new DiagnosticQueue<Diagnostic>();
        var diagnosticQueue2 = new DiagnosticQueue<Diagnostic>();
        diagnosticQueue1.Push(new Diagnostic(DiagnosticSeverity.Error, ""));
        diagnosticQueue1.Push(new Diagnostic(DiagnosticSeverity.Error, ""));
        diagnosticQueue2.Push(new Diagnostic(DiagnosticSeverity.Warning, ""));
        diagnosticQueue2.Push(new Diagnostic(DiagnosticSeverity.Warning, ""));
        diagnosticQueue1.CopyToFront(diagnosticQueue2);

        Assert.Equal(DiagnosticSeverity.Warning, diagnosticQueue1.ToArray()[0].info.severity);
    }
}

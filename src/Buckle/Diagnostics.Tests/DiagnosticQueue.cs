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
}

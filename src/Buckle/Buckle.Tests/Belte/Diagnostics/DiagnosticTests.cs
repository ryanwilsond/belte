using Xunit;
using Xunit.Abstractions;
using static Buckle.Tests.Belte.Assertions;

namespace Buckle.Tests.Belte.Diagnostics;

/// <summary>
/// At least one test per diagnostic (any severity) if testable.
/// </summary>
public sealed class DiagnosticTests {
    private readonly ITestOutputHelper writer;

    public DiagnosticTests(ITestOutputHelper writer) {
        this.writer = writer;
    }

    [Fact]
    public void Reports_Error_CL0001_MissingFilenameO() {
        var args = new string[] { "-o" };

        var diagnostics = @"
            missing filename after '-o'
        ";

        AssertDiagnostics(args, diagnostics, writer);
    }

    [Fact]
    public void Reports_Error_CL0002_MultipleExplains() {
        var args = new string[] { "--explain1", "--explain2" };

        var diagnostics = @"
            cannot specify '--explain' more than once
        ";

        AssertDiagnostics(args, diagnostics, writer);
    }

    // TODO All the other ones

}

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
        var file = "reports_error_cl0001_missingfilename0.blt";
        var args = new string[] { file, "-o" };

        var diagnostics = @"
            missing filename after '-o'
        ";

        AssertDiagnostics(args, diagnostics, writer, false, file);
    }

    // TODO All the other ones

}

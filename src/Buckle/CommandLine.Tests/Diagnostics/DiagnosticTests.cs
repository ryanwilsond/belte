using System.IO;
using Diagnostics;
using Xunit;
using Xunit.Abstractions;
using static CommandLine.Tests.Assertions;

namespace CommandLine.Tests.Diagnostics;

/// <summary>
/// At least one test per diagnostic (any severity) if testable.
/// </summary>
public sealed class DiagnosticTests {
    private readonly ITestOutputHelper _writer;

    public DiagnosticTests(ITestOutputHelper writer) {
        _writer = writer;
    }

    [Fact]
    public void Reports_Error_CL0001_MissingFilenameO() {
        var args = new string[] { "-o" };

        var diagnostics = @"
            missing filename after '-o'
        ";

        AssertDiagnostics(args, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_CL0002_MultipleExplains() {
        var args = new string[] { "--explain1", "--explain2" };

        var diagnostics = @"
            cannot specify '--explain' more than once
        ";

        AssertDiagnostics(args, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_CL0003_MissingCodeExplain() {
        var args = new string[] { "--explain" };

        var diagnostics = @"
            missing diagnostic code after '--explain'
        ";

        AssertDiagnostics(args, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_CL0004_MissingModuleName() {
        var args = new string[] { "--modulename" };

        var diagnostics = @"
            missing name after '--modulename' (usage: '--modulename=<name>')
        ";

        AssertDiagnostics(args, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_CL0005_MissingReference() {
        var args = new string[] { "--ref" };

        var diagnostics = @"
            missing name after '--ref' (usage: '--ref=<name>')
        ";

        AssertDiagnostics(args, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_CL0006_UnableToOpenFile() {
        var fileName = "BelteTestsAssertDiagnosticCL0006.blt";
        var args = new string[] { fileName };

        var diagnostics = @"
            failed to open file 'BelteTestsAssertDiagnosticCL0006.blt'; most likely due to the file being used by another process
        ";

        var fileStream = File.Create(fileName);
        AssertDiagnostics(args, diagnostics, _writer);
        fileStream.Close();
        File.Delete(fileName);
    }

    [Fact]
    public void Reports_Error_CL0007_MissingSeverity() {
        var args = new string[] { "--severity" };

        var diagnostics = @"
            missing severity after '--severity' (usage: '--severity=<severity>')
        ";

        AssertDiagnostics(args, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_CL0008_UnrecognizedSeverity() {
        var args = new string[] { "--severity=asdf" };

        var diagnostics = @"
            unrecognized severity 'asdf'
        ";

        AssertDiagnostics(args, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_CL0009_UnrecognizedOption() {
        var args = new string[] { "-asdf" };

        var diagnostics = @"
            unrecognized command line option '-asdf'; see 'buckle --help'
        ";

        AssertDiagnostics(args, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Info_CL0010_ReplInvokeIgnore() {
        var args = new string[] { "-r" };

        var diagnostics = @"
            all arguments are ignored when invoking the repl
        ";

        AssertDiagnostics(args, diagnostics, _writer, DiagnosticSeverity.Info);
    }

    [Fact]
    public void Reports_Fatal_CL0011_CannotSpecifyWithDotnet() {
        var args = new string[] { "-d", "-s" };

        var diagnostics = @"
            cannot specify '-p', '-s', '-c', or '-t' with .NET integration
        ";

        AssertDiagnostics(args, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Fatal_CL0012_CannotSpecifyWithMultipleFiles() {
        var fileName = "BelteTestsAssertDiagnosticCL0012.blt";
        var args = new string[] {
            fileName, "-s", "-o", "BelteTestsAssertDiagnosticCL0012.exe"
        };

        var diagnostics = @"
            cannot specify output file with '-p', '-s', '-c', or '-t' with multiple files
        ";

        AssertDiagnostics(args, diagnostics, _writer, DiagnosticSeverity.Fatal, false, fileName);
    }

    [Fact]
    public void Reports_Fatal_CL0013_CannotSpecifyWithInterpreter() {
        var args = new string[] { "-i", "-s" };

        var diagnostics = @"
            cannot specify output path or use '-p', '-s', '-c', or '-t' with interpreter
        ";

        AssertDiagnostics(args, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Fatal_CL0014_CannotSpecifyModuleNameWithoutDotnet() {
        var args = new string[] { "--modulename=testhost" };

        var diagnostics = @"
            cannot specify module name without .NET integration
        ";

        AssertDiagnostics(args, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Fatal_CL0015_CannotSpecifyReferencesWithoutDotnet() {
        var args = new string[] { "--ref=some/fake/path" };

        var diagnostics = @"
            cannot specify references without .NET integration
        ";

        AssertDiagnostics(args, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_CL0016_NoInputFiles() {
        var args = new string[] { };

        var diagnostics = @"
            no input files
        ";

        AssertDiagnostics(args, diagnostics, _writer, noInputFiles: true);
    }

    [Fact]
    public void Reports_Error_CL0017_NoSuchFileOrDirectory() {
        var args = new string[] { "BelteTestsAssertDiagnosticCL0017.blt" };

        var diagnostics = @"
            BelteTestsAssertDiagnosticCL0017.blt: no such file or directory
        ";

        AssertDiagnostics(args, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Info_CL0018_IgnoringUnknownFileType() {
        var fileName = "BelteTestsAssertDiagnosticCL0018.ablt";
        var args = new string[] { fileName };

        var diagnostics = @"
            unknown file type of input file 'BelteTestsAssertDiagnosticCL0018.ablt'; ignoring
        ";

        AssertDiagnostics(args, diagnostics, _writer, DiagnosticSeverity.Info, false, fileName);
    }

    [Fact]
    public void Reports_Error_CL0019_InvalidErrorCode() {
        var args = new string[] { "--explain0a" };

        var diagnostics = @"
            'BU0a' is not a valid error code; must be in the format: [BU|CL|RE]<code>
            examples: BU0001, CL0001, BU54, CL012, RE0001, RE6
        ";

        AssertDiagnostics(args, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Info_CL0020_IgnoringCompiledFile() {
        var args = new string[] { "BelteTestsAssertDiagnosticCL0020.exe" };

        var diagnostics = @"
            BelteTestsAssertDiagnosticCL0020.exe: file already compiled; ignoring
        ";

        AssertDiagnostics(
            args, diagnostics, _writer, DiagnosticSeverity.Info, false, "BelteTestsAssertDiagnosticCL0020.exe"
        );
    }

    [Fact]
    public void Reports_Error_CL0021_UnusedErrorCode() {
        var args = new string[] { "--explain9999" };

        var diagnostics = @"
            'BU9999' is not a used error code
        ";

        AssertDiagnostics(args, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Fatal_CL0022_CannotInterpretWithMultipleFiles() {
        var args = new string[] { "a.blt", "b.blt", "--script" };

        var diagnostics = @"
            cannot pass multiple files when running as a script
        ";

        AssertDiagnostics(args, diagnostics, _writer, DiagnosticSeverity.Fatal, false, "a.blt", "b.blt");
    }

    [Fact]
    public void Reports_Fatal_CL0023_CannotInterpretFile() {
        var args = new string[] { "a.o", "--script" };

        var diagnostics = @"
            cannot interpret file
        ";

        AssertDiagnostics(args, diagnostics, _writer, DiagnosticSeverity.Fatal, true, "a.o");
    }
}

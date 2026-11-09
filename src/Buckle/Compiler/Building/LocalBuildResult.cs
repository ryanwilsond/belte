using Buckle.CodeAnalysis;
using Diagnostics;

namespace Buckle.Building;

public sealed class LocalBuildResult {
    private readonly Compilation _compilation;

    public bool succeeded;

    public string[] arguments;

    [Belte.Nullability([1, 1])]
    public DiagnosticQueue<Diagnostic> diagnostics;

    public LocalBuildResult(
        bool succeeded,
        string[] arguments,
        Compilation compilation,
        DiagnosticQueue<Diagnostic> diagnostics) {
        this.succeeded = succeeded;
        this.arguments = arguments;
        _compilation = compilation;
        this.diagnostics = diagnostics;
    }

    internal Compilation GetCompilation() {
        return _compilation;
    }
}

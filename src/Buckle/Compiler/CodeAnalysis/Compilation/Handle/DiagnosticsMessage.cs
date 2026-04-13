using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis;

public sealed class DiagnosticsMessage : Message {
    private readonly BelteDiagnosticQueue _diagnostics;

    internal DiagnosticsMessage(BelteDiagnosticQueue diagnostics) : base(MessageKind.Diagnostics) {
        _diagnostics = diagnostics;
    }

    public BelteDiagnosticQueue Diagnostics() {
        return _diagnostics;
    }
}

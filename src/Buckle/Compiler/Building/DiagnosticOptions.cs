using System.Collections.Generic;
using Diagnostics;

namespace Buckle.Building;

public class DiagnosticOptions {
    public DiagnosticSeverity severity = DiagnosticSeverity.Warning;

    public int warningLevel = 1;

    public List<string> wincludes = [];

    public List<string> wexcludes = [];

    public DiagnosticOptions Copy() {
        return new DiagnosticOptions() {
            severity = severity,
            warningLevel = warningLevel,
            wincludes = new List<string>(wincludes),
            wexcludes = new List<string>(wexcludes),
        };
    }
}

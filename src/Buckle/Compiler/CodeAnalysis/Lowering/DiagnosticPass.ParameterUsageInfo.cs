using System.Collections.Generic;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class DiagnosticPass {
    private struct ParameterUsageInfo {
        internal bool[] used;
        internal bool[] usedIgnoringDiscard;
        internal List<TextLocation> discardLocations;

        internal ParameterUsageInfo(int parameterCount) {
            used = new bool[parameterCount];
            usedIgnoringDiscard = new bool[parameterCount];
            discardLocations = [];
        }
    }
}

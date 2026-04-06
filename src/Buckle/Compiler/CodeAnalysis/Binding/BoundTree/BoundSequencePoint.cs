
namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundSequencePoint {
    internal static BoundStatement CreateHidden(BoundStatement statementOpt = null, bool hasErrors = false) {
        return new BoundSequencePoint(null, statementOpt, hasErrors);
    }
}

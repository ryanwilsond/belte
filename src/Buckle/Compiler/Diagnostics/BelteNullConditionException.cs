using Buckle.CodeAnalysis.Text;

namespace Buckle.Diagnostics;

internal class BelteNullConditionException : BelteEvaluatorException {
    internal new static readonly string Message = "Cannot branch on a null condition.";

    internal BelteNullConditionException(TextLocation location)
        : base(Message, location) { }
}

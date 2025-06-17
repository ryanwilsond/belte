using System;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Evaluating;

internal sealed partial class Executor {
    public class NullConditionException : Exception {
        internal NullConditionException() : base(BelteNullConditionException.Message) { }
    }
}

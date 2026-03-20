using System;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Evaluating;

internal sealed partial class Executor {
    public class NullConditionException : Exception {
        public NullConditionException() : base(BelteNullConditionException.Message) { }
    }
}

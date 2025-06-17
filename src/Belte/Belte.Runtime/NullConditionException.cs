using System;

namespace Belte.Runtime;

public class NullConditionException : Exception {
    internal NullConditionException() : base("Cannot branch on a null condition.") { }
}

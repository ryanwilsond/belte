using System;
using Buckle.Diagnostics;

namespace Buckle.Utilities;

/// <summary>
/// Utilities related to exceptions.
/// </summary>
internal static class ExceptionUtilities {
    /// <summary>
    /// An exception indicating that a certain code path should never be reached.
    /// </summary>
    internal static Exception Unreachable()
        => new InvalidOperationException("This program location is thought to be unreachable");

    internal static Exception UnexpectedValue(object value)
        => new BelteInternalException($"Unexpected value '{value}'");
}

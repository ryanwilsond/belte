using System;
using Buckle.CodeAnalysis.Text;

namespace Buckle.Diagnostics;

internal sealed class TransformedExecException : Exception {
    internal TransformedExecException(Exception inner, TextLocation location) : base(null, inner) {
        this.location = location;
    }

    internal TextLocation location { get; }
}

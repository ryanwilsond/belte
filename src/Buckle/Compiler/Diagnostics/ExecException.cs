using System;

namespace Buckle.Diagnostics;

public sealed class ExecException : Exception {
    public ExecException(Exception inner, SimpleTextLocation location) : base(null, inner) {
        this.location = location;
    }

    public SimpleTextLocation location { get; }
}

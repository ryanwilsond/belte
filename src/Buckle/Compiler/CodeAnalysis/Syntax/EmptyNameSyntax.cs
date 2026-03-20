using System;

namespace Buckle.CodeAnalysis.Syntax;

public sealed partial class EmptyNameSyntax {
    internal override string ErrorDisplayName() {
        return "";
    }

    internal override SimpleNameSyntax GetUnqualifiedName() {
        throw new InvalidOperationException();
    }
}

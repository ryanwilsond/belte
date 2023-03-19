using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Lowering;

/// <summary>
/// Expands expressions to make them simpler to handle by the <see cref="Lowerer" />.
/// </summary>
internal sealed class Expander {
    private int _tempCount;

    /// <summary>
    /// Expands all expression in a <see cref="BoundBlockStatement" />.
    /// If <param name="statement" /> is not a <see cref="BoundBlockStatement" />, not all expansion will occur.
    /// </summary>
    /// <param name="statement"><see cref="BoundBlockStatement" /> to expand expressions in.</param>
    /// <returns>Expanded <param name="statement" />.</returns>
    internal static BoundStatement Expand(BoundStatement statement) {
        return statement;
    }
}

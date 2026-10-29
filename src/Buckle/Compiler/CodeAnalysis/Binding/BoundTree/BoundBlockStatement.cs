using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundBlockStatement {
#if DEBUG
    [OverloadResolutionPriority(1)]
#else
    [OverloadResolutionPriority(-1)]
#endif
    internal BoundBlockStatement(
        SyntaxNode syntaxNode,
        ImmutableArray<BoundStatement> statements,
        ImmutableArray<DataContainerSymbol> locals,
        ImmutableArray<LocalFunctionSymbol> localFunctions,
        bool hasErrors = false,
        object _ = null)
      : this(syntax: syntaxNode, statements, locals, localFunctions, hasErrors) {
        Debug.Assert(locals.ToHashSet().Count == locals.Length);
        Debug.Assert(localFunctions.ToHashSet().Count == localFunctions.Length);
    }
}

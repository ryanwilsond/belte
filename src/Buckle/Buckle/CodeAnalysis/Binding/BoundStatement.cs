using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding;

/// Note: All bound versions of statements and expression share function with parser equivalents.
/// Thus use their xml comments for reference.

/// <summary>
/// A bound statement, bound from a parser Statement
/// </summary>
internal abstract class BoundStatement : BoundNode { }

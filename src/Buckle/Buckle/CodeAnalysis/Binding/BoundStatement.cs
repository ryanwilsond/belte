using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding;

/// Note: All bound versions of the <see cref="Statement" /> and <see cref="Expression" /> share function
/// with <see cref="Parser" /> equivalents.
/// Thus use their xml comments for reference.

/// <summary>
/// A bound statement, bound from a <see cref="Statement" />
/// </summary>
internal abstract class BoundStatement : BoundNode { }

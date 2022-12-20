using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A <see cref="Node" /> representing a source file, the root node of a <see cref="SyntaxTree" />.
/// </summary>
internal sealed partial class CompilationUnit : Node {
    /// <param name="members">The top level nodes (global).</param>
    /// <param name="endOfFile">End of file/EOF token.</param>
    internal CompilationUnit(SyntaxTree syntaxTree, ImmutableArray<Member> members, Token endOfFile)
        : base(syntaxTree) {
        this.members = members;
        this.endOfFile = endOfFile;
    }

    /// <summary>
    /// The top level Nodes (global) in the source file.
    /// </summary>
    internal ImmutableArray<Member> members { get; }

    /// <summary>
    /// EOF token.
    /// </summary>
    internal Token endOfFile { get; }

    internal override SyntaxType type => SyntaxType.CompilationUnit;
}

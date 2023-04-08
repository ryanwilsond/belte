using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A <see cref="SyntaxNode" /> representing a source file, the root node of a <see cref="SyntaxTree" />.
/// </summary>
public sealed partial class CompilationUnitSyntax : SyntaxNode {
    /// <param name="members">The top level SyntaxNodes (global).</param>
    /// <param name="endOfFile">End of file/EOF token.</param>
    internal CompilationUnitSyntax(SyntaxTree syntaxTree, ImmutableArray<MemberSyntax> members, SyntaxToken endOfFile)
        : base(syntaxTree) {
        this.members = members;
        this.endOfFile = endOfFile;
    }

    /// <summary>
    /// The top level SyntaxNodes (global) in the source file.
    /// </summary>
    public ImmutableArray<MemberSyntax> members { get; }

    /// <summary>
    /// EOF token.
    /// </summary>
    internal SyntaxToken endOfFile { get; }

    internal override SyntaxKind kind => SyntaxKind.CompilationUnit;
}

internal sealed partial class SyntaxFactory {
    internal CompilationUnitSyntax CompilationUnit(ImmutableArray<MemberSyntax> members, SyntaxToken endOfFile)
        => Create(new CompilationUnitSyntax(_syntaxTree, members, endOfFile));
}

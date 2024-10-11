using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceMemberContainerTypeSymbol {
    private protected sealed class DeclaredMembersAndInitializers {
        internal static readonly DeclaredMembersAndInitializers UninitializedSentinel = new DeclaredMembersAndInitializers();

        internal readonly ImmutableArray<Symbol> nonTypeMembers;
        internal readonly ImmutableArray<ImmutableArray<FieldInitializer>> fieldInitializers;
        internal readonly TypeDeclarationSyntax declarationWithParameters;
        internal readonly Compilation compilation;

        private DeclaredMembersAndInitializers() { }

        internal DeclaredMembersAndInitializers(
            ImmutableArray<Symbol> nonTypeMembers,
            ImmutableArray<ImmutableArray<FieldInitializer>> fieldInitializers,
            TypeDeclarationSyntax declarationWithParameters,
            Compilation compilation) {
            this.nonTypeMembers = nonTypeMembers;
            this.fieldInitializers = fieldInitializers;
            this.declarationWithParameters = declarationWithParameters;
            this.compilation = compilation;
        }
    }
}

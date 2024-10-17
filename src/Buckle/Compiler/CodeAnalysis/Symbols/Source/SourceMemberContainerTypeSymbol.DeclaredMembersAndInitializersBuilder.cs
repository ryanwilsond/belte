using Buckle.CodeAnalysis.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceMemberContainerTypeSymbol {
    private sealed class DeclaredMembersAndInitializersBuilder {
        internal readonly ArrayBuilder<ArrayBuilder<FieldInitializer>> fieldInitializers
            = ArrayBuilder<ArrayBuilder<FieldInitializer>>.GetInstance();

        internal ArrayBuilder<Symbol> nonTypeMembers = ArrayBuilder<Symbol>.GetInstance();
        internal TypeDeclarationSyntax declarationWithParameters;

        internal DeclaredMembersAndInitializers ToReadOnlyAndFree(Compilation compilation) {
            return new DeclaredMembersAndInitializers(
                nonTypeMembers.ToImmutableAndFree(),
                MembersAndInitializersBuilder.ToReadOnlyAndFree(fieldInitializers),
                declarationWithParameters,
                compilation
            );
        }

        internal void Free() {
            nonTypeMembers.Free();

            foreach (var group in fieldInitializers)
                group.Free();

            fieldInitializers.Free();
        }
    }
}

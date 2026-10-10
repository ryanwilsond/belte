using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceMemberContainerTypeSymbol {
    private sealed class ScopeInheritorInfo {
        internal DeclarationModifiers modifiers;
        internal SyntaxList<AttributeListSyntax> attributeLists;

        internal ScopeInheritorInfo(DeclarationModifiers modifiers, SyntaxList<AttributeListSyntax> attributeLists) {
            this.modifiers = modifiers;
            this.attributeLists = attributeLists;
        }
    }
}

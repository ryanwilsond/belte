using System.Collections.Generic;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceEnumConstantSymbol {
    private sealed class ExplicitValuedEnumConstantSymbol : SourceEnumConstantSymbol {
        internal ExplicitValuedEnumConstantSymbol(
            SourceMemberContainerTypeSymbol containingEnum,
            EnumMemberDeclarationSyntax syntax,
            BelteDiagnosticQueue diagnostics)
            : base(containingEnum, syntax, diagnostics) { }

        private protected override ConstantValue MakeConstantValue(
            HashSet<SourceFieldSymbolWithSyntaxReference> dependencies,
            BelteDiagnosticQueue diagnostics) {
            var syntax = syntaxNode;
            return ConstantValueHelpers.EvaluateFieldConstant(this, syntax.equalsValue, dependencies, diagnostics);
        }
    }
}

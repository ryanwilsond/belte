using System.Collections.Generic;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceEnumConstantSymbol {
    private sealed class ZeroValuedEnumConstantSymbol : SourceEnumConstantSymbol {
        internal ZeroValuedEnumConstantSymbol(
            SourceMemberContainerTypeSymbol containingEnum,
            EnumMemberDeclarationSyntax syntax,
            BelteDiagnosticQueue diagnostics)
            : base(containingEnum, syntax, diagnostics) {
            if (containingEnum.enumUnderlyingType.specialType == SpecialType.String)
                diagnostics.Push(Error.InvalidImplicitEnum(syntax.location));
        }

        private protected override ConstantValue MakeConstantValue(
            HashSet<SourceFieldSymbolWithSyntaxReference> dependencies,
            BelteDiagnosticQueue diagnostics) {
            var constantType = containingType.enumUnderlyingType.specialType;
            return new ConstantValue(LiteralUtilities.GetDefaultValue(constantType), constantType);
        }
    }
}

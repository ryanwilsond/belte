using System.Collections.Generic;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceEnumConstantSymbol {
    private sealed class ZeroValuedEnumConstantSymbol : SourceEnumConstantSymbol {
        private readonly bool _isFlagsEnum;

        internal ZeroValuedEnumConstantSymbol(
            SourceMemberContainerTypeSymbol containingEnum,
            EnumMemberDeclarationSyntax syntax,
            bool isFlagsEnum,
            BelteDiagnosticQueue diagnostics)
            : base(containingEnum, syntax, diagnostics) {
            if (containingEnum.enumUnderlyingType.specialType == SpecialType.String)
                diagnostics.Push(Error.InvalidImplicitEnum(syntax.location));

            _isFlagsEnum = isFlagsEnum;
        }

        private protected override ConstantValue MakeConstantValue(
            HashSet<SourceFieldSymbolWithSyntaxReference> dependencies,
            BelteDiagnosticQueue diagnostics) {
            var constantValue = LiteralUtilities.TryGetDefaultValue(containingType.enumUnderlyingType);

            if (_isFlagsEnum)
                // "Zero-Valued" flags enums start at 1
                EnumConstantHelper.OffsetValue(constantValue, 1, false, out constantValue);

            return constantValue;
        }
    }
}

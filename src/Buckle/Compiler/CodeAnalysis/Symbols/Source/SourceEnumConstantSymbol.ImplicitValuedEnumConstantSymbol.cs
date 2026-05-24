using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceEnumConstantSymbol {
    private sealed class ImplicitValuedEnumConstantSymbol : SourceEnumConstantSymbol {
        private readonly SourceEnumConstantSymbol _otherConstant;
        private readonly uint _otherConstantOffset;
        private readonly bool _isFlagsEnum;

        internal ImplicitValuedEnumConstantSymbol(
            SourceMemberContainerTypeSymbol containingEnum,
            EnumMemberDeclarationSyntax syntax,
            SourceEnumConstantSymbol otherConstant,
            uint otherConstantOffset,
            bool isFlagsEnum,
            BelteDiagnosticQueue diagnostics)
            : base(containingEnum, syntax, diagnostics) {
            _otherConstant = otherConstant;
            _otherConstantOffset = otherConstantOffset;
            _isFlagsEnum = isFlagsEnum;
        }

        private protected override ConstantValue MakeConstantValue(
            HashSet<SourceFieldSymbolWithSyntaxReference> dependencies,
            BelteDiagnosticQueue diagnostics) {
            var otherValue = _otherConstant.GetConstantValue(new ConstantFieldsInProgress(this, dependencies));

            if (otherValue == ConstantValue.Unset)
                return ConstantValue.Unset;

            if (otherValue is null)
                return null;

            var overflowKind = EnumConstantHelper.OffsetValue(
                otherValue,
                _otherConstantOffset,
                _isFlagsEnum,
                out var value
            );

            if (overflowKind == EnumOverflowKind.OverflowReport)
                diagnostics.Push(Error.EnumOverflow(location, this));

            return value;
        }
    }
}

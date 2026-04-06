using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class FlowLowerer {
    private abstract partial class DecisionDagRewriter {
        private sealed class CasesComparer : IComparer<(ConstantValue value, LabelSymbol label)> {
            private readonly IValueSetFactory _fac;

            internal CasesComparer(TypeSymbol type) {
                _fac = ValueSetFactory.ForType(type);
            }

            int IComparer<(ConstantValue value, LabelSymbol label)>.Compare(
                (ConstantValue value, LabelSymbol label) left,
                (ConstantValue value, LabelSymbol label) right) {
                var x = left.value;
                var y = right.value;

                return
                    IsNaN(x) ? 1 :
                    IsNaN(y) ? -1 :
                    _fac.Related(BinaryOperatorKind.LessThanOrEqual, x, y) ?
                        (_fac.Related(BinaryOperatorKind.LessThanOrEqual, y, x) ? 0 : -1) :
                    1;

                static bool IsNaN(ConstantValue value) {
                    if (value.specialType == SpecialType.Float32)
                        return float.IsNaN((float)value.value);

                    if (value.specialType == SpecialType.Float64)
                        return double.IsNaN((double)value.value);

                    return false;
                }
            }
        }
    }
}


namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class BoundCastExpression {
    internal bool ConversionHasSideEffects() {
        // TODO Discriminate numeric conversions?
        switch (conversion.kind) {
            case ConversionKind.Identity:
            // case ConversionKind.ImplicitNumeric:
            // case ConversionKind.ImplicitEnumeration:
            // implicit ref cast does not throw ...
            case ConversionKind.ImplicitReference:
            case ConversionKind.AnyBoxing:
                return false;

                // case ConversionKind.ExplicitNumeric:
                //     return this.Checked;
        }

        return true;
    }
}

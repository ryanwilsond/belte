using System;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Symbols;

internal static partial class ConstraintsHelpers {
    internal sealed partial class TemplateConstraintComparer {
        private struct Range {
            internal (IComparable, bool) lower;
            internal (IComparable, bool) upper;
            internal List<IComparable> exclusions;
        }
    }
}

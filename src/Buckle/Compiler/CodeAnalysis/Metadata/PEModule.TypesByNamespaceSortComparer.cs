using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;

namespace Buckle.CodeAnalysis;

internal sealed partial class PEModule {
    internal class TypesByNamespaceSortComparer : IComparer<IGrouping<string, TypeDefinitionHandle>> {
        private readonly StringComparer _nameComparer;

        internal TypesByNamespaceSortComparer(StringComparer nameComparer) {
            _nameComparer = nameComparer;
        }

        public int Compare(
            IGrouping<string, TypeDefinitionHandle> left,
            IGrouping<string, TypeDefinitionHandle> right) {
            if (left == right)
                return 0;

            var result = _nameComparer.Compare(left.Key, right.Key);

            if (result == 0) {
                var fLeft = left.FirstOrDefault();
                var fRight = right.FirstOrDefault();

                if (fLeft.IsNil ^ fRight.IsNil)
                    result = fLeft.IsNil ? +1 : -1;
                else
                    result = HandleComparer.Default.Compare(fLeft, fRight);

                if (result == 0)
                    result = string.CompareOrdinal(left.Key, right.Key);
            }

            return result;
        }
    }
}

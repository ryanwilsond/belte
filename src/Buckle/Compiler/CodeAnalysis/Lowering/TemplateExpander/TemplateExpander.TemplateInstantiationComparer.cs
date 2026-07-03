using System.Collections.Generic;
using System.Diagnostics;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class TemplateExpander {
    private sealed class TemplateInstantiationComparer<T> : IEqualityComparer<T> where T : ISymbolWithTemplates {
        public bool Equals(T x, T y) {
            var originalX = (x as Symbol).originalDefinition;
            var originalY = (y as Symbol).originalDefinition;

            if (!originalX.Equals(originalY))
                return false;

            for (var i = 0; i < x.templateArguments.Length; i++) {
                var argX = x.templateArguments[i];
                var argY = y.templateArguments[i];

                Debug.Assert(argX.isConstant == argY.isConstant);

                // Type template arguments are ignored
                if (argX.isConstant) {
                    if (!argX.IsSameAs(argY))
                        return false;
                }
            }

            return true;
        }

        public int GetHashCode(T obj) {
            var code = (obj as Symbol).originalDefinition.GetHashCode();

            foreach (var templateArgument in obj.templateArguments) {
                if (templateArgument.isConstant)
                    code = Hash.Combine(templateArgument, code);
            }

            return code;
        }
    }
}

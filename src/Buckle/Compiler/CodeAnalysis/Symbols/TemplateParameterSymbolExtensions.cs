using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Symbols;

internal static class TemplateParameterSymbolExtensions {
    internal static bool DependsOn(
        this TemplateParameterSymbol typeParameter1,
        TemplateParameterSymbol typeParameter2) {
        Stack<TemplateParameterSymbol> stack = null;
        HashSet<TemplateParameterSymbol> visited = null;

        while (true) {
            foreach (var constraintType in typeParameter1.constraintTypes) {
                if (constraintType.type is TemplateParameterSymbol typeParameter) {
                    if (typeParameter.Equals(typeParameter2))
                        return true;

                    visited ??= [];

                    if (visited.Add(typeParameter)) {
                        stack ??= new Stack<TemplateParameterSymbol>();
                        stack.Push(typeParameter);
                    }
                }
            }
            if (stack is null || stack.Count == 0)
                break;

            typeParameter1 = stack.Pop();
        }

        return false;
    }
}

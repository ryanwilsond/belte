using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal static partial class ConstraintsHelpers {
    private sealed class ConstraintSubstituteTemplateBinder : Binder {
        private readonly TemplateMap _substitution;

        internal ConstraintSubstituteTemplateBinder(TemplateMap substitution, Binder next) : base(next, next.flags) {
            _substitution = substitution;
        }

        internal override NamespaceOrTypeOrAliasSymbolWithAnnotations BindNamespaceOrTypeOrAliasSymbol(
            ExpressionSyntax syntax,
            BelteDiagnosticQueue diagnostics,
            ConsList<TypeSymbol> basesBeingResolved = null,
            bool rewriteBufferType = true) {
            var result = base.BindNamespaceOrTypeOrAliasSymbol(
                syntax,
                diagnostics,
                basesBeingResolved,
                rewriteBufferType
            );

            if (result.isType) {
                var symbol = _substitution.SubstituteType((TypeSymbol)result.namespaceOrTypeSymbol);

                if (symbol.isType) {
                    result = NamespaceOrTypeOrAliasSymbolWithAnnotations.CreateUnannotated(
                        result.isNullable,
                        symbol.type.type
                    );
                }
            }

            return result;
        }
    }
}

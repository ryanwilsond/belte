using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Binding;

internal abstract class WithTemplateParametersBinder : Binder {
    internal WithTemplateParametersBinder(Binder next) : base(next) { }

    private protected abstract Dictionary<string, List<TemplateParameterSymbol>> _templateParameterMap { get; }

    private protected virtual LookupOptions _lookupMask => LookupOptions.MustBeInvocableIfMember;

    internal override void LookupSymbolsInSingleBinder(
        LookupResult result,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        Binder originalBinder,
        TextLocation errorLocation,
        bool diagnose) {
        if ((options & _lookupMask) != 0)
            return;

        if (!_templateParameterMap.TryGetValue(name, out var value))
            return;

        foreach (var templateParameter in value) {
            result.MergeEqual(originalBinder.CheckViability(
                templateParameter,
                arity,
                options,
                null,
                diagnose,
                errorLocation
            ));
        }
    }

    private protected bool CanConsiderTypeParameters(LookupOptions options) {
        return (options & (_lookupMask | LookupOptions.MustBeInstance)) == 0;
    }
}

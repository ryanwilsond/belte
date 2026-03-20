using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class NameofBinder : Binder {
    private readonly SyntaxNode _nameofArgument;
    private readonly WithTemplateParametersBinder _withTemplateParametersBinder;
    private readonly Binder _withParametersBinder;

    internal NameofBinder(
        SyntaxNode nameofArgument,
        Binder next,
        WithTemplateParametersBinder withTemplateParametersBinder,
        Binder withParametersBinder)
        : base(next) {
        _nameofArgument = nameofArgument;
        _withTemplateParametersBinder = withTemplateParametersBinder;
        _withParametersBinder = withParametersBinder;
    }

    internal override bool isInsideNameof => true;

    private protected override SyntaxNode _enclosingNameofArgument => _nameofArgument;

    internal override void LookupSymbolsInSingleBinder(
        LookupResult result,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        Binder originalBinder,
        TextLocation errorLocation,
        bool diagnose) {
        var foundParameter = false;

        if (_withParametersBinder is not null) {
            _withParametersBinder.LookupSymbolsInSingleBinder(
                result,
                name,
                arity,
                basesBeingResolved,
                options,
                originalBinder,
                errorLocation,
                diagnose
            );

            if (!result.isClear) {
                if (result.isMultiViable)
                    return;

                foundParameter = true;
            }
        }

        if (_withTemplateParametersBinder is not null) {
            if (foundParameter) {
                var tmp = LookupResult.GetInstance();
                _withTemplateParametersBinder.LookupSymbolsInSingleBinder(
                    tmp,
                    name,
                    arity,
                    basesBeingResolved,
                    options,
                    originalBinder,
                    errorLocation,
                    diagnose
                );

                result.MergeEqual(tmp);
            } else {
                _withTemplateParametersBinder.LookupSymbolsInSingleBinder(
                    result,
                    name,
                    arity,
                    basesBeingResolved,
                    options,
                    originalBinder,
                    errorLocation,
                    diagnose
                );
            }
        }
    }

    internal override void AddLookupSymbolsInfoInSingleBinder(
        LookupSymbolsInfo info,
        LookupOptions options,
        Binder originalBinder) {
        _withParametersBinder?.AddLookupSymbolsInfoInSingleBinder(info, options, originalBinder);
        _withTemplateParametersBinder?.AddLookupSymbolsInfoInSingleBinder(info, options, originalBinder);
    }
}

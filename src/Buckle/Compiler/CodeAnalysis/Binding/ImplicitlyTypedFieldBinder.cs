using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class ImplicitlyTypedFieldBinder : Binder {
    private readonly ConsList<FieldSymbol> _fieldsBeingBound;

    internal ImplicitlyTypedFieldBinder(Binder next, ConsList<FieldSymbol> fieldsBeingBound)
        : base(next, next.flags) {
        _fieldsBeingBound = fieldsBeingBound;
    }

    internal override ConsList<FieldSymbol> fieldsBeingBound => _fieldsBeingBound;
}

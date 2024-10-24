using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class ConstantFieldsInProgress {
    private readonly SourceFieldSymbol _field;
    private readonly HashSet<SourceFieldSymbolWithSyntaxReference> _dependencies;

    internal static readonly ConstantFieldsInProgress Empty = new ConstantFieldsInProgress(null, null);

    internal ConstantFieldsInProgress(
        SourceFieldSymbol field,
        HashSet<SourceFieldSymbolWithSyntaxReference> dependencies) {
        _field = field;
        _dependencies = dependencies;
    }

    public bool IsEmpty => _field is null;

    internal void AddDependency(SourceFieldSymbolWithSyntaxReference field) {
        _dependencies.Add(field);
    }
}

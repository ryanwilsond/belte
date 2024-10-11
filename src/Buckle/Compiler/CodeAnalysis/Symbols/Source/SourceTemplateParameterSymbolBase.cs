using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceTemplateParameterSymbolBase : TemplateParameterSymbol {
    private TypeParameterBounds _lazyBounds = TypeParameterBounds.Unset;
    private SymbolCompletionState _state;

    private protected SourceTemplateParameterSymbolBase(
        string name,
        int ordinal,
        TypeWithAnnotations underlyingType,
        ConstantValue defaultValue,
        SyntaxReference syntaxReference) {
        this.name = name;
        this.ordinal = ordinal;
        this.underlyingType = underlyingType;
        this.defaultValue = defaultValue;
        this.syntaxReference = syntaxReference;
    }

    public sealed override string name { get; }

    internal sealed override int ordinal { get; }

    internal sealed override TypeWithAnnotations underlyingType { get; }

    internal sealed override ConstantValue defaultValue { get; }

    internal sealed override SyntaxReference syntaxReference { get; }

    private protected abstract ImmutableArray<TemplateParameterSymbol> _containerTemplateParameters { get; }

    internal sealed override NamedTypeSymbol GetEffectiveBaseClass(ConsList<TemplateParameterSymbol> inProgress) {
        var bounds = GetBounds(inProgress);
        return (bounds is not null) ? bounds.effectiveBaseClass : GetDefaultBaseType();
    }

    internal sealed override TypeSymbol GetDeducedBaseType(ConsList<TemplateParameterSymbol> inProgress) {
        var bounds = GetBounds(inProgress);
        return (bounds is not null) ? bounds.deducedBaseType : GetDefaultBaseType();
    }

    internal sealed override ImmutableArray<TypeWithAnnotations> GetConstraintTypes(
        ConsList<TemplateParameterSymbol> inProgress) {
        var bounds = GetBounds(inProgress);
        return (bounds is not null) ? bounds.constraintTypes : [];
    }

    internal sealed override void EnsureConstraintsAreResolved() {
        if (!_lazyBounds.IsSet())
            EnsureConstraintsAreResolved(_containerTemplateParameters);
    }

    internal override void ForceComplete(TextLocation location) {
        while (true) {
            var incompletePart = _state.nextIncompletePart;

            switch (incompletePart) {
                case CompletionParts.TemplateParameterConstraints:
                    var _ = constraintTypes;
                    break;
                case CompletionParts.None:
                    return;
                default:
                    _state.NotePartComplete(CompletionParts.All & ~CompletionParts.TemplateParameterSymbolAll);
                    break;
            }

            _state.SpinWaitComplete(incompletePart);
        }
    }

    private protected abstract TypeParameterBounds ResolveBounds(
        ConsList<TemplateParameterSymbol> inProgress,
        BelteDiagnosticQueue diagnostics);

    private NamedTypeSymbol GetDefaultBaseType() {
        return CorLibrary.GetSpecialType(SpecialType.Object);
    }

    private TypeParameterBounds GetBounds(ConsList<TemplateParameterSymbol> inProgress) {
        if (!_lazyBounds.IsSet()) {
            var diagnostics = BelteDiagnosticQueue.Instance;
            var bounds = ResolveBounds(inProgress, diagnostics);

            if (ReferenceEquals(Interlocked.CompareExchange(
                ref _lazyBounds,
                bounds,
                TypeParameterBounds.Unset), TypeParameterBounds.Unset)) {
                CheckConstraintTypeConstraints(diagnostics);
                AddDeclarationDiagnostics(diagnostics);
                _state.NotePartComplete(CompletionParts.TemplateParameterConstraints);
            }
        }

        return _lazyBounds;
    }

    private void CheckConstraintTypeConstraints(BelteDiagnosticQueue diagnostics) {
        if (constraintTypes.Length == 0)
            return;

        foreach (var constraintType in constraintTypes) {
            if (constraintType.type.specialType != SpecialType.Type)
                diagnostics.Push(Error.CannotExtendCheckNonType(syntaxReference.location, constraintType.type.name));
        }
    }
}

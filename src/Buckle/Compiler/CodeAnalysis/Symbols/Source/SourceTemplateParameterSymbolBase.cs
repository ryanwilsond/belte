using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceTemplateParameterSymbolBase : TemplateParameterSymbol {
    private TypeParameterBounds _lazyBounds = TypeParameterBounds.Unset;
    private SymbolCompletionState _state;
    private TypeWithAnnotations _lazyUnderlyingType;
    private ConstantValue _lazyDefaultValue;

    private protected SourceTemplateParameterSymbolBase(
        string name,
        int ordinal,
        SyntaxReference syntaxReference) {
        this.name = name;
        this.ordinal = ordinal;
        this.syntaxReference = syntaxReference;
    }

    public sealed override string name { get; }

    internal sealed override int ordinal { get; }

    internal sealed override TypeWithAnnotations underlyingType {
        get {
            if (_lazyUnderlyingType is null) {
                var diagnostics = BelteDiagnosticQueue.GetInstance();

                if (Interlocked.CompareExchange(ref _lazyUnderlyingType, MakeUnderlyingType(diagnostics), null) is null)
                    AddDeclarationDiagnostics(diagnostics);

                diagnostics.Free();
            }

            return _lazyUnderlyingType;
        }
    }

    internal sealed override ConstantValue defaultValue {
        get {
            if (_state.NotePartComplete(CompletionParts.StartDefaultSyntaxValue)) {
                var diagnostics = BelteDiagnosticQueue.GetInstance();

                if (Interlocked.CompareExchange(ref _lazyDefaultValue, MakeDefaultValue(diagnostics), null) is null)
                    AddDeclarationDiagnostics(diagnostics);

                diagnostics.Free();
            }

            _state.SpinWaitComplete(CompletionParts.EndDefaultSyntaxValue);
            return _lazyDefaultValue;
        }
    }

    internal sealed override SyntaxReference syntaxReference { get; }

    private protected abstract ImmutableArray<TemplateParameterSymbol> _containerTemplateParameters { get; }

    private Binder _withTemplateParametersBinder
        => (containingSymbol as SourceMethodSymbol).withTemplateParametersBinder;

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
        _ = underlyingType;

        while (true) {
            var incompletePart = _state.nextIncompletePart;

            switch (incompletePart) {
                case CompletionParts.TemplateParameterConstraints:
                    _ = constraintTypes;
                    break;
                case CompletionParts.StartDefaultSyntaxValue:
                    _ = defaultValue;
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

    private static NamedTypeSymbol GetDefaultBaseType() {
        return CorLibrary.GetSpecialType(SpecialType.Object);
    }

    private TypeParameterBounds GetBounds(ConsList<TemplateParameterSymbol> inProgress) {
        if (!_lazyBounds.IsSet()) {
            var diagnostics = BelteDiagnosticQueue.GetInstance();
            var bounds = ResolveBounds(inProgress, diagnostics);

            if (ReferenceEquals(Interlocked.CompareExchange(
                ref _lazyBounds,
                bounds,
                TypeParameterBounds.Unset), TypeParameterBounds.Unset)) {
                CheckConstraintTypeConstraints(diagnostics);
                AddDeclarationDiagnostics(diagnostics);
                _state.NotePartComplete(CompletionParts.TemplateParameterConstraints);
            }

            diagnostics.Free();
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

    private TypeWithAnnotations MakeUnderlyingType(BelteDiagnosticQueue diagnostics) {
        var syntax = (ParameterSyntax)syntaxReference.node;
        var binder = declaringCompilation.GetBinder(syntax);
        return binder.BindType(syntax.type, diagnostics);
    }

    private ConstantValue MakeDefaultValue(BelteDiagnosticQueue diagnostics) {
        var syntax = (ParameterSyntax)syntaxReference.node;

        if (syntax.defaultValue is null)
            return null;

        var defaultSyntax = syntax.defaultValue;
        var binder = GetDefaultValueBinder(syntax);
        binder = binder.CreateBinderForParameterDefaultValue(this, defaultSyntax);
        var equalsValue = binder.BindParameterDefaultValue(
            defaultSyntax,
            this,
            diagnostics,
            out var valueBeforeConversion
        );

        if (equalsValue is null)
            return null;

        var convertedExpression = equalsValue.value;

        if (convertedExpression is null)
            return null;

        if (convertedExpression.constantValue is null && convertedExpression.kind == BoundNodeKind.CastExpression &&
            ((BoundCastExpression)convertedExpression).conversionKind != ConversionKind.DefaultLiteral) {
            if (underlyingType.isNullable) {
                convertedExpression = binder.GenerateConversionForAssignment(
                    underlyingType,
                    valueBeforeConversion,
                    diagnostics,
                    Binder.ConversionForAssignmentFlags.DefaultParameter
                );
            }
        }

        // TODO Error checking, i.e. "must be compile-time constant"
        return convertedExpression.constantValue;
    }

    private Binder GetDefaultValueBinder(SyntaxNode syntax) {
        var binder = _withTemplateParametersBinder;

        if (binder is null) {
            var binderFactory = declaringCompilation.GetBinderFactory(syntax.syntaxTree);
            binder = binderFactory.GetBinder(syntax);
        }

        return binder;
    }
}

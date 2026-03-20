using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceComplexParameterSymbolBase : SourceParameterSymbol {
    private readonly bool _hasDefaultValue;
    private CustomAttributesBag<AttributeData> _lazyAttributesBag;

    private protected ConstantValue _lazyDefaultSyntaxValue;

    private protected SourceComplexParameterSymbolBase(
        Symbol owner,
        int ordinal,
        RefKind refKind,
        string name,
        ParameterSyntax syntax,
        ScopedKind scope)
        : base(owner, ordinal, refKind, scope, name, syntax) {
        _hasDefaultValue = syntax is not null && syntax.defaultValue is not null;
    }

    internal override bool hasDefaultArgumentSyntax => _hasDefaultValue;

    internal override bool isMetadataOptional => hasDefaultArgumentSyntax;

    internal sealed override ScopedKind effectiveScope {
        get {
            var scope = CalculateEffectiveScopeIgnoringAttributes();

            if (scope != ScopedKind.None && hasUnscopedRefAttribute)
                return ScopedKind.None;

            return scope;
        }
    }

    // internal override bool hasUnscopedRefAttribute => GetEarlyDecodedWellKnownAttributeData()?.HasUnscopedRefAttribute == true;
    internal override bool hasUnscopedRefAttribute => false;

    private Binder _withTemplateParametersBinder
        => (containingSymbol as SourceMethodSymbol).withTemplateParametersBinder;

    internal sealed override SyntaxList<AttributeListSyntax> attributeDeclarationList {
        get {
            var syntax = (ParameterSyntax)syntaxReference.node;
            return (syntax is not null) ? syntax.attributeLists : default;
        }
    }

    internal override ConstantValue explicitDefaultConstantValue {
        get {
            if (_state.NotePartComplete(CompletionParts.StartDefaultSyntaxValue)) {
                var diagnostics = BelteDiagnosticQueue.GetInstance();
                Interlocked.CompareExchange(
                    ref _lazyDefaultSyntaxValue,
                    MakeDefaultValue(diagnostics, out var binder, out var parameterEqualsValue),
                    null
                );

                _state.NotePartComplete(CompletionParts.EndDefaultSyntaxValue);

                if (parameterEqualsValue is not null) {
                    // TODO Is this needed?
                    // if (binder is not null && GetDefaultValueSyntax() is { } valueSyntax)
                    //     NullableWalker.AnalyzeIfNeeded(binder, parameterEqualsValue, valueSyntax, diagnostics);
                }

                AddDeclarationDiagnostics(diagnostics);
                diagnostics.Free();
                _state.NotePartComplete(CompletionParts.EndDefaultSyntaxValueDiagnostics);
            }

            _state.SpinWaitComplete(CompletionParts.EndDefaultSyntaxValue);
            return _lazyDefaultSyntaxValue;
        }
    }

    internal override void ForceComplete(TextLocation locationOpt) {
        GetAttributes();
        _ = explicitDefaultConstantValue;
        _state.SpinWaitComplete(CompletionParts.ComplexParameterSymbolAll);
    }

    private ConstantValue MakeDefaultValue(
        BelteDiagnosticQueue diagnostics,
        out Binder binder,
        out BoundParameterEqualsValue parameterEqualsValue) {
        binder = null;
        parameterEqualsValue = null;

        var syntax = (ParameterSyntax)syntaxReference.node;

        if (syntax is null)
            return null;

        var defaultSyntax = syntax.defaultValue;

        if (defaultSyntax is null)
            return null;

        binder = GetDefaultParameterValueBinder(defaultSyntax);
        binder = binder.CreateBinderForParameterDefaultValue(this, defaultSyntax);

        parameterEqualsValue = (BoundParameterEqualsValue)binder.BindParameterDefaultValue(
            defaultSyntax,
            this,
            diagnostics,
            out var valueBeforeConversion
        );

        if (parameterEqualsValue is null || valueBeforeConversion is null)
            return null;

        var convertedExpression = parameterEqualsValue.value;
        var hasErrors = ParameterHelpers.ReportDefaultParameterErrors(
            binder,
            containingSymbol,
            syntax,
            this,
            valueBeforeConversion,
            convertedExpression,
            diagnostics
        );

        if (hasErrors)
            return null;

        if (convertedExpression.constantValue is null && convertedExpression.kind == BoundKind.CastExpression &&
            ((BoundCastExpression)convertedExpression).conversion.kind != ConversionKind.DefaultLiteral) {
            if (type.IsNullableType()) {
                convertedExpression = binder.GenerateConversionForAssignment(
                    type.GetNullableUnderlyingType(),
                    valueBeforeConversion,
                    diagnostics,
                    Binder.ConversionForAssignmentFlags.DefaultParameter
                );
            }
        }

        return convertedExpression.constantValue;
    }

    internal sealed override CustomAttributesBag<AttributeData> GetAttributesBag() {
        var bag = _lazyAttributesBag;

        if (bag is not null && bag.isSealed)
            return bag;

        var attributeSyntax = GetAttributeDeclarations();

        if (LoadAndValidateAttributes(attributeSyntax, ref _lazyAttributesBag, binderOpt: _withTemplateParametersBinder))
            _state.NotePartComplete(CompletionParts.Attributes);

        return _lazyAttributesBag;
    }

    internal virtual OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations() {
        return OneOrMany.Create(attributeDeclarationList);
    }

    private Binder GetDefaultParameterValueBinder(SyntaxNode syntax) {
        var binder = _withTemplateParametersBinder;

        if (binder is null) {
            var binderFactory = declaringCompilation.GetBinderFactory(syntax.syntaxTree);
            binder = binderFactory.GetBinder(syntax);
        }

        return binder;
    }
}

using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceComplexParameterSymbolBase : SourceParameterSymbol {
    private readonly bool _hasDefaultValue;

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

    private Binder _withTemplateParametersBinder
        => (containingSymbol as SourceMethodSymbol).withTemplateParametersBinder;

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

        parameterEqualsValue = binder.BindParameterDefaultValue(
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

        if (convertedExpression.constantValue is null && convertedExpression.kind == BoundNodeKind.CastExpression &&
            ((BoundCastExpression)convertedExpression).conversionKind != ConversionKind.DefaultLiteral) {
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

    private Binder GetDefaultParameterValueBinder(SyntaxNode syntax) {
        var binder = _withTemplateParametersBinder;

        if (binder is null) {
            var binderFactory = declaringCompilation.GetBinderFactory(syntax.syntaxTree);
            binder = binderFactory.GetBinder(syntax);
        }

        return binder;
    }
}

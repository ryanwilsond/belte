using System.Diagnostics;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceComplexParameterSymbolBase : SourceParameterSymbol, IAttributeTargetSymbol {
    private readonly bool _hasDefaultValue;
    private CustomAttributesBag<AttributeData> _lazyAttributesBag;
    private ConstantValue _lazyOutDefaultValue;
    private BoundExpression _lazyExpressionDefaultValue;
    private ThreeState _lazyIsExpressionDefaultValue;

    private protected ConstantValue _lazyDefaultSyntaxValue;

    private protected SourceComplexParameterSymbolBase(
        Symbol owner,
        int ordinal,
        RefKind refKind,
        bool isConst,
        string name,
        ParameterSyntax syntax,
        TextLocation location,
        ScopedKind scope)
        : base(owner, ordinal, refKind, isConst, scope, name, new SyntaxReference(syntax), location) {
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

    internal override ConstantValue outDefaultValue {
        get {
            if (!_state.HasComplete(CompletionParts.EndDefaultSyntaxValue))
                _ = explicitDefaultConstantValue;

            return _lazyOutDefaultValue;
        }
    }

    internal override BoundExpression expressionDefaultValue {
        get {
            if (_lazyExpressionDefaultValue is null) {
                if (_lazyIsExpressionDefaultValue == ThreeState.Unknown)
                    _ = explicitDefaultConstantValue;

                Debug.Assert(_lazyIsExpressionDefaultValue != ThreeState.Unknown);

                if (_lazyIsExpressionDefaultValue == ThreeState.True) {
                    var diagnostics = BelteDiagnosticQueue.GetInstance();

                    if (Interlocked.CompareExchange(
                            ref _lazyExpressionDefaultValue,
                            MakeExpressionDefaultValue(diagnostics),
                            null)
                        == null) {
                        AddDeclarationDiagnostics(diagnostics);
                    }

                    diagnostics.Free();
                }
            }

            return _lazyExpressionDefaultValue;
        }
    }

    private protected virtual IAttributeTargetSymbol _attributeOwner => this;

    IAttributeTargetSymbol IAttributeTargetSymbol.attributesOwner => _attributeOwner;

    AttributeLocation IAttributeTargetSymbol.defaultAttributeLocation => AttributeLocation.Parameter;

    AttributeLocation IAttributeTargetSymbol.allowedAttributeLocations => AttributeLocation.Parameter;

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
        _ = expressionDefaultValue;
        _state.SpinWaitComplete(CompletionParts.ComplexParameterSymbolAll);
    }

    private BoundExpression MakeExpressionDefaultValue(BelteDiagnosticQueue diagnostics) {
        var syntax = (ParameterSyntax)syntaxReference.node;
        Debug.Assert(syntax is not null);
        var defaultSyntax = syntax.defaultValue;
        Debug.Assert(defaultSyntax is not null);

        var binder = GetContainingBinder(containingSymbol);

        var localsBinder = GetDefaultParameterValueBinder(defaultSyntax);
        localsBinder = localsBinder.CreateBinderForParameterDefaultValue(this, defaultSyntax);
        localsBinder = localsBinder.GetBinder(defaultSyntax);

        Debug.Assert(binder is not null);
        var parameterEqualsValue = (BoundParameterEqualsValue)binder.BindParameterDefaultValue(
            defaultSyntax,
            this,
            binder,
            localsBinder,
            diagnostics,
            out var valueBeforeConversion
        );

        Debug.Assert(parameterEqualsValue is not null);

        var tempDiagnostics = BelteDiagnosticQueue.GetInstance();

        var convertedExpression = parameterEqualsValue.value;
        var hasErrors = ParameterHelpers.ReportDefaultParameterErrors(
            binder,
            containingSymbol,
            syntax,
            this,
            valueBeforeConversion,
            convertedExpression,
            tempDiagnostics,
            out var isExpressionDefaultValue
        );

        if (!isExpressionDefaultValue || hasErrors) {
            diagnostics.PushRangeAndFree(tempDiagnostics);
            return convertedExpression;
        }

        Debug.Assert(!tempDiagnostics.AnyErrors());
        tempDiagnostics.Free();

        ParameterHelpers.ExpressionDefaultValueVisitor.ReportDiagnostics(this, convertedExpression, diagnostics);
        return convertedExpression;

        Binder GetContainingBinder(Symbol containingSymbol) {
            switch (containingSymbol) {
                case SourceMemberMethodSymbol memberMethod:
                    return memberMethod.TryGetBodyBinder();
                case LocalFunctionSymbol localFunction:
                    return GetContainingBinder(localFunction.containingSymbol).GetBinder(localFunction.syntax.body);
                default:
                    throw ExceptionUtilities.UnexpectedValue(containingSymbol);
            }
        }
    }

    private ConstantValue MakeDefaultValue(
        BelteDiagnosticQueue diagnostics,
        out Binder binder,
        out BoundParameterEqualsValue parameterEqualsValue) {
        binder = null;
        parameterEqualsValue = null;

        var syntax = (ParameterSyntax)syntaxReference.node;

        if (syntax is null) {
            Interlocked.CompareExchange(ref _lazyIsExpressionDefaultValue, ThreeState.False, ThreeState.Unknown);
            return null;
        }

        var defaultSyntax = syntax.defaultValue;

        if (defaultSyntax is null) {
            Interlocked.CompareExchange(ref _lazyIsExpressionDefaultValue, ThreeState.False, ThreeState.Unknown);
            return null;
        }

        binder = GetDefaultParameterValueBinder(defaultSyntax);
        binder = binder.CreateBinderForParameterDefaultValue(this, defaultSyntax);

        var tempDiagnostics = BelteDiagnosticQueue.GetInstance();

        parameterEqualsValue = (BoundParameterEqualsValue)binder.BindParameterDefaultValue(
            defaultSyntax,
            this,
            tempDiagnostics,
            out var valueBeforeConversion
        );

        if (parameterEqualsValue is null || valueBeforeConversion is null) {
            Interlocked.CompareExchange(ref _lazyIsExpressionDefaultValue, ThreeState.False, ThreeState.Unknown);
            return null;
        }

        var convertedExpression = parameterEqualsValue.value;
        var hasErrors = ParameterHelpers.ReportDefaultParameterErrors(
            binder,
            containingSymbol,
            syntax,
            this,
            valueBeforeConversion,
            convertedExpression,
            diagnostics,
            out var isExpressionDefaultValue
        );

        if (isExpressionDefaultValue) {
            Interlocked.CompareExchange(ref _lazyIsExpressionDefaultValue, ThreeState.True, ThreeState.Unknown);
            tempDiagnostics.Free();
            return null;
        } else {
            if (Interlocked.CompareExchange(ref _lazyIsExpressionDefaultValue, ThreeState.False, ThreeState.Unknown)
                == ThreeState.Unknown) {
                diagnostics.PushRange(tempDiagnostics);
            }
        }

        tempDiagnostics.Free();

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

        if (refKind == RefKind.Out) {
            Interlocked.CompareExchange(ref _lazyOutDefaultValue, convertedExpression.constantValue, null);
            return null;
        }

        return convertedExpression.constantValue ?? ConstantValue.Null;
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

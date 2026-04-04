using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceTemplateParameterSymbolBase : TemplateParameterSymbol, IAttributeTargetSymbol {
    private TypeParameterBounds _lazyBounds = TypeParameterBounds.Unset;
    private SymbolCompletionState _state;
    private TypeWithAnnotations _lazyUnderlyingType;
    private TypeOrConstant _lazyDefaultValue;
    private CustomAttributesBag<AttributeData> _lazyAttributesBag;

    private protected SourceTemplateParameterSymbolBase(
        string name,
        int ordinal,
        SyntaxReference syntaxReference) {
        this.name = name;
        this.ordinal = ordinal;
        this.syntaxReference = syntaxReference;
        location = (syntaxReference.node as ParameterSyntax).identifier.location;
    }

    public sealed override string name { get; }

    internal sealed override int ordinal { get; }

    internal sealed override bool isOptional => ((ParameterSyntax)syntaxReference.node).defaultValue is not null;

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

    internal sealed override TypeOrConstant defaultValue {
        get {
            if (_state.NotePartComplete(CompletionParts.StartDefaultSyntaxValue)) {
                var diagnostics = BelteDiagnosticQueue.GetInstance();

                if (Interlocked.CompareExchange(ref _lazyDefaultValue, MakeDefaultValue(diagnostics), null) is null)
                    AddDeclarationDiagnostics(diagnostics);

                diagnostics.Free();
            }

            _state.NotePartComplete(CompletionParts.EndDefaultSyntaxValue);
            return _lazyDefaultValue;
        }
    }

    IAttributeTargetSymbol IAttributeTargetSymbol.attributesOwner => this;

    AttributeLocation IAttributeTargetSymbol.defaultAttributeLocation => AttributeLocation.TemplateParameter;

    AttributeLocation IAttributeTargetSymbol.allowedAttributeLocations => AttributeLocation.TemplateParameter;

    internal sealed override SyntaxReference syntaxReference { get; }

    internal sealed override TextLocation location { get; }

    internal ImmutableArray<SyntaxList<AttributeListSyntax>> mergedAttributeDeclarationSyntaxLists
        => [((ParameterSyntax)syntaxReference.node).attributeLists];

    private protected abstract ImmutableArray<TemplateParameterSymbol> _containerTemplateParameters { get; }

    private Binder _withTemplateParametersBinder
        => (containingSymbol as SourceMethodSymbol)?.withTemplateParametersBinder;

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
                case CompletionParts.Attributes:
                    GetAttributes();
                    break;
                case CompletionParts.TemplateParameterConstraints:
                    _ = underlyingType;
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

    internal sealed override ImmutableArray<AttributeData> GetAttributes() {
        return GetAttributesBag().attributes;
    }

    internal virtual CustomAttributesBag<AttributeData> GetAttributesBag() {
        var bag = _lazyAttributesBag;

        if (bag is not null && bag.isSealed)
            return bag;

        if (LoadAndValidateAttributes(
            OneOrMany.Create(mergedAttributeDeclarationSyntaxLists),
            ref _lazyAttributesBag,
            binderOpt: (containingSymbol as LocalFunctionSymbol)?.withTemplateParametersBinder)) {
            _state.NotePartComplete(CompletionParts.Attributes);
        }

        return _lazyAttributesBag;
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
        if (underlyingType.specialType != SpecialType.Type) {
            if (hasPrimitiveTypeConstraint || hasNotNullConstraint)
                diagnostics.Push(Error.CannotIsCheckNonType(location, name));

            if (constraintTypes.Length > 0)
                diagnostics.Push(Error.CannotExtendCheckNonType(location, name));
        }
    }

    private TypeWithAnnotations MakeUnderlyingType(BelteDiagnosticQueue diagnostics) {
        var syntax = (ParameterSyntax)syntaxReference.node;
        var binder = declaringCompilation.GetBinder(syntax);
        var type = binder.BindType(syntax.type, diagnostics);
        var underlying = type.nullableUnderlyingTypeOrSelf;

        // TODO This is what we want, type arguments being null breaks their use as normal types
        // ! However this does create a weird inconsistency where this is the ONLY time types aren't nullable by default
        if (underlying.specialType == SpecialType.Type)
            return new TypeWithAnnotations(underlying);

        if (declaringCompilation.options.buildMode is BuildMode.CSharpTranspile or
                                                      BuildMode.Execute or
                                                      BuildMode.Dotnet) {
            diagnostics.Push(Error.Unsupported.NonTypeTemplate(syntax.location));
        }

        if (hasNotNullConstraint)
            return new TypeWithAnnotations(underlying);

        return type;
    }

    private TypeOrConstant MakeDefaultValue(BelteDiagnosticQueue diagnostics) {
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

        if (convertedExpression.constantValue is null && convertedExpression.kind == BoundKind.CastExpression &&
            ((BoundCastExpression)convertedExpression).conversion.kind != ConversionKind.DefaultLiteral) {
            if (underlyingType.isNullable) {
                convertedExpression = binder.GenerateConversionForAssignment(
                    underlyingType.type,
                    valueBeforeConversion,
                    diagnostics,
                    Binder.ConversionForAssignmentFlags.DefaultParameter
                );
            }
        }

        if (convertedExpression is BoundTypeExpression t)
            return new TypeOrConstant(t.type);

        var constant = convertedExpression.constantValue;

        if (constant is null && !convertedExpression.hasErrors)
            diagnostics.Push(Error.DefaultMustBeConstant(defaultSyntax.location, name));

        return new TypeOrConstant(constant);
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

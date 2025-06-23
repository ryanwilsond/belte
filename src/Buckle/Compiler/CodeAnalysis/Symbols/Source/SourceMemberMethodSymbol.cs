using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceMemberMethodSymbol : SourceMethodSymbol {
    private protected readonly DeclarationModifiers _modifiers;

    private protected Flags _flags;
    private protected SymbolCompletionState _state;
    private ParameterSymbol _lazyThisParameter;

    private OverriddenOrHiddenMembersResult _lazyOverriddenOrHiddenMembers;

    private protected SourceMemberMethodSymbol(
        NamedTypeSymbol containingType,
        SyntaxReference syntaxReference,
        (DeclarationModifiers modifiers, Flags flags) modifiersAndFlags)
        : base(syntaxReference) {
        this.containingType = containingType;
        _modifiers = modifiersAndFlags.modifiers;
        _flags = modifiersAndFlags.flags;
    }

    public sealed override ImmutableArray<TypeOrConstant> templateArguments
        => GetTemplateParametersAsTemplateArguments();

    public sealed override int arity => templateParameters.Length;

    public override bool returnsVoid => _flags.returnsVoid;

    public sealed override MethodKind methodKind => _flags.methodKind;

    public sealed override RefKind refKind => _flags.refKind;

    internal sealed override OverriddenOrHiddenMembersResult overriddenOrHiddenMembers {
        get {
            LazyMethodChecks();

            if (_lazyOverriddenOrHiddenMembers is null) {
                Interlocked.CompareExchange(
                    ref _lazyOverriddenOrHiddenMembers,
                    this.MakeOverriddenOrHiddenMembers(),
                    null
                );
            }

            return _lazyOverriddenOrHiddenMembers;
        }
    }

    internal sealed override bool requiresCompletion => true;

    internal sealed override Symbol containingSymbol => containingType;

    internal override NamedTypeSymbol containingType { get; }

    internal override Accessibility declaredAccessibility => ModifierHelpers.EffectiveAccessibility(_modifiers);

    internal sealed override bool isSealed => (_modifiers & DeclarationModifiers.Sealed) != 0;

    internal sealed override bool isAbstract => (_modifiers & DeclarationModifiers.Abstract) != 0;

    internal sealed override bool isOverride => (_modifiers & DeclarationModifiers.Override) != 0;

    internal sealed override bool isVirtual => (_modifiers & DeclarationModifiers.Virtual) != 0;

    internal sealed override bool isStatic => (_modifiers & DeclarationModifiers.Static) != 0;

    internal sealed override CallingConvention callingConvention {
        get {
            var cc = CallingConvention.Default;

            if (isTemplateMethod)
                cc |= CallingConvention.Template;

            if (!isStatic)
                cc |= CallingConvention.HasThis;

            return cc;
        }
    }

    internal override bool isDeclaredConst => (_modifiers & DeclarationModifiers.Const) != 0;

    internal bool isLowLevel => (_modifiers & DeclarationModifiers.LowLevel) != 0;

    internal bool isNew => (_modifiers & DeclarationModifiers.New) != 0;

    internal BlockStatementSyntax body => syntaxNode switch {
        BaseMethodDeclarationSyntax method => method.body,
        _ => null,
    };

    // This allows synthesized methods to also perform method checks without having a conflicting lock
    // TODO This could probably be removed because there are no synthesized event methods or anything similar
    private protected virtual object _methodChecksLockObject => syntaxReference;

    internal sealed override bool HasComplete(CompletionParts part) {
        return _state.HasComplete(part);
    }

    internal override void ForceComplete(TextLocation location) {
        while (true) {
            var incompletePart = _state.nextIncompletePart;

            switch (incompletePart) {
                case CompletionParts.Type:
                    _ = returnType;
                    _state.NotePartComplete(CompletionParts.Type);
                    break;
                case CompletionParts.Parameters:
                    foreach (var parameter in parameters)
                        parameter.ForceComplete(location);

                    _state.NotePartComplete(CompletionParts.Parameters);
                    break;
                case CompletionParts.TemplateParameters:
                    foreach (var templateParameter in templateParameters)
                        templateParameter.ForceComplete(location);

                    _state.NotePartComplete(CompletionParts.TemplateParameters);
                    break;
                case CompletionParts.StartMethodChecks:
                case CompletionParts.FinishMethodChecks:
                    LazyMethodChecks();
                    goto done;
                case CompletionParts.None:
                    return;
                default:
                    _state.NotePartComplete(CompletionParts.All & ~CompletionParts.MethodSymbolAll);
                    break;
            }

            _state.SpinWaitComplete(incompletePart);
        }

done:
        _state.SpinWaitComplete(CompletionParts.MethodSymbolAll);
    }

    internal override int CalculateLocalSyntaxOffset(int localPosition, SyntaxTree localTree) {
        return localPosition - body.span.start;
    }

    internal override bool IsMetadataVirtual(bool forceComplete = false) {
        if (forceComplete && !_flags.isMetadataVirtualLocked)
            containingSymbol.ForceComplete(null);

        return _flags.isMetadataVirtual;
    }

    internal abstract ExecutableCodeBinder TryGetBodyBinder(
        BinderFactory binderFactory = null,
        bool ignoreAccessibility = false);

    internal sealed override bool TryGetThisParameter(out ParameterSymbol thisParameter) {
        thisParameter = _lazyThisParameter;

        if (thisParameter is not null || isStatic)
            return true;

        Interlocked.CompareExchange(ref _lazyThisParameter, new ThisParameterSymbol(this), null);
        thisParameter = _lazyThisParameter;
        return true;
    }

    private protected abstract void MethodChecks(BelteDiagnosticQueue diagnostics);

    private protected void SetReturnsVoid(bool returnsVoid) {
        _flags.SetReturnsVoid(returnsVoid);
    }

    private protected void CheckEffectiveAccessibility(
        TypeWithAnnotations returnType,
        ImmutableArray<ParameterSymbol> parameters,
        BelteDiagnosticQueue diagnostics) {
        if (declaredAccessibility <= Accessibility.Private)
            return;

        var underlyingReturnType = returnType.type;

        if (!IsNoMoreVisibleThan(underlyingReturnType)) {
            if (methodKind == MethodKind.Operator) {
                diagnostics.Push(
                    Error.InconsistentAccessibilityOperatorReturn(syntaxReference.location, underlyingReturnType, this)
                );
            } else {
                diagnostics.Push(
                    Error.InconsistentAccessibilityReturn(syntaxReference.location, underlyingReturnType, this)
                );
            }
        }

        foreach (var parameter in parameters) {
            if (!parameter.typeWithAnnotations.IsAtLeastAsVisibleAs(this)) {
                if (methodKind == MethodKind.Operator) {
                    diagnostics.Push(
                        Error.InconsistentAccessibilityOperatorParameter(syntaxReference.location, parameter.type, this)
                    );
                } else {
                    diagnostics.Push(
                        Error.InconsistentAccessibilityParameter(syntaxReference.location, parameter.type, this)
                    );
                }
            }
        }
    }

    private protected ExecutableCodeBinder TryGetBodyBinderFromSyntax(
        BinderFactory binderFactory = null,
        bool ignoreAccessibility = false) {
        var inMethod = TryGetInMethodBinder(binderFactory);
        return inMethod is null
            ? null
            : new ExecutableCodeBinder(
                syntaxNode,
                this,
                inMethod.WithAdditionalFlags(ignoreAccessibility ? BinderFlags.IgnoreAccessibility : BinderFlags.None)
            );
    }

    private protected void CheckModifiersForBody(TextLocation location, BelteDiagnosticQueue diagnostics) {
        if (isAbstract)
            diagnostics.Push(Error.AbstractCannotHaveBody(location, this));
    }

    private protected static Flags MakeFlags(
        MethodKind methodKind,
        RefKind refKind,
        DeclarationModifiers declarationModifiers,
        bool returnsVoid,
        bool returnsVoidIsSet,
        bool hasAnyBody,
        bool hasThisInitializer) {
        return new Flags(
            methodKind,
            refKind,
            declarationModifiers,
            returnsVoid,
            returnsVoidIsSet,
            hasAnyBody,
            hasThisInitializer
        );
    }

    private protected void LazyMethodChecks() {
        if (!_state.HasComplete(CompletionParts.FinishMethodChecks)) {
            var lockObject = _methodChecksLockObject;

            lock (lockObject) {
                if (_state.NotePartComplete(CompletionParts.StartMethodChecks)) {
                    var diagnostics = BelteDiagnosticQueue.GetInstance();

                    try {
                        MethodChecks(diagnostics);
                        AddDeclarationDiagnostics(diagnostics);
                    } finally {
                        _state.NotePartComplete(CompletionParts.FinishMethodChecks);
                        diagnostics.Free();
                    }
                }
            }
        }
    }

    private Binder TryGetInMethodBinder(BinderFactory binderFactory = null) {
        var contextNode = GetInMethodSyntaxNode();

        if (contextNode is null)
            return null;

        return (binderFactory ?? declaringCompilation.GetBinderFactory(contextNode.syntaxTree)).GetBinder(contextNode);
    }
}

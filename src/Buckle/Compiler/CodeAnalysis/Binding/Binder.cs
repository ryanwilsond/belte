using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Lowering;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Binds a <see cref="Syntax.InternalSyntax.LanguageParser" /> output into a immutable "bound" tree. This is where most
/// error checking happens. The <see cref="Lowerer" /> is also called here to simplify the code and convert control of
/// flow into gotos and labels. Dead code is also removed here, as well as other optimizations.
/// </summary>
internal partial class Binder {

    #region Internal Model

    private protected OverloadResolution _lazyOverloadResolution;
    private protected Conversions _lazyConversions;

    internal Binder(Compilation compilation) {
        flags = compilation.options.topLevelBinderFlags;
        this.compilation = compilation;
    }

    internal Binder(Binder next) {
        this.next = next;
        flags = next.flags;
        _lazyConversions = conversions;
    }

    private protected Binder(Binder next, BinderFlags flags) {
        this.next = next;
        this.flags = flags;
        compilation = next.compilation;
    }

    internal virtual SyntaxNode scopeDesignator => null;

    internal virtual Symbol containingMember => next.containingMember;

    internal virtual SynthesizedLabelSymbol breakLabel => next.breakLabel;

    internal virtual SynthesizedLabelSymbol continueLabel => next.continueLabel;

    internal virtual bool inMethod => next.inMethod;

    internal virtual LocalVariableSymbol localInProgress => next.localInProgress;

    internal virtual ConstantFieldsInProgress constantFieldsInProgress => next.constantFieldsInProgress;

    internal virtual BoundExpression conditionalReceiverExpression => next.conditionalReceiverExpression;

    internal virtual ConsList<FieldSymbol> fieldsBeingBound => next.fieldsBeingBound;

    internal NamedTypeSymbol containingType => containingMember switch {
        null => null,
        NamedTypeSymbol namedType => namedType,
        _ => containingMember.containingType
    };

    internal Compilation compilation { get; }

    internal BinderFlags flags { get; }

    internal Conversions conversions {
        get {
            if (_lazyConversions == null)
                Interlocked.CompareExchange(ref _lazyConversions, new Conversions(this), null);

            return _lazyConversions;
        }
    }

    internal OverloadResolution overloadResolution {
        get {
            if (_lazyOverloadResolution == null)
                Interlocked.CompareExchange(ref _lazyOverloadResolution, new OverloadResolution(this), null);

            return _lazyOverloadResolution;
        }
    }

    internal Binder next { get; }

    internal virtual Binder GetBinder(SyntaxNode node) {
        return next.GetBinder(node);
    }

    internal virtual ImmutableArray<LocalVariableSymbol> GetDeclaredLocalsForScope(SyntaxNode scopeDesignator) {
        return next.GetDeclaredLocalsForScope(scopeDesignator);
    }

    internal virtual ImmutableArray<LocalFunctionSymbol> GetDeclaredLocalFunctionsForScope(
        BelteSyntaxNode scopeDesignator) {
        return next.GetDeclaredLocalFunctionsForScope(scopeDesignator);
    }

    internal virtual BoundForStatement BindForParts(BelteDiagnosticQueue diagnostics, Binder originalBinder) {
        return next.BindForParts(diagnostics, originalBinder);
    }

    internal virtual BoundWhileStatement BindWhileParts(BelteDiagnosticQueue diagnostics, Binder originalBinder) {
        return next.BindWhileParts(diagnostics, originalBinder);
    }

    internal virtual BoundDoWhileStatement BindDoWhileParts(BelteDiagnosticQueue diagnostics, Binder originalBinder) {
        return next.BindDoWhileParts(diagnostics, originalBinder);
    }

    private protected virtual SourceLocalSymbol LookupLocal(SyntaxToken identifier) {
        return next.LookupLocal(identifier);
    }

    private protected virtual LocalFunctionSymbol LookupLocalFunction(SyntaxToken identifier) {
        return next.LookupLocalFunction(identifier);
    }

    private protected virtual bool IsUnboundTypeAllowed(TemplateNameSyntax syntax) {
        return next.IsUnboundTypeAllowed(syntax);
    }

    private bool IsSymbolAccessible(Symbol symbol, NamedTypeSymbol within, TypeSymbol throughType = null) {
        return IsSymbolAccessible(symbol, within, throughType, out _);
    }

    private bool IsSymbolAccessible(Symbol symbol, NamedTypeSymbol within, TypeSymbol throughType, out bool failedThroughTypeCheck) {
        failedThroughTypeCheck = false;

        if (flags.Includes(BinderFlags.IgnoreAccessibility))
            return true;

        return IsSymbolAccessibleInternal(symbol, within, throughType, out failedThroughTypeCheck);
    }

    private static bool IsSymbolAccessibleInternal(Symbol symbol, NamedTypeSymbol within, TypeSymbol throughType, out bool failedThroughTypeCheck) {
        failedThroughTypeCheck = false;

        switch (symbol.kind) {
            case SymbolKind.NamedType:
            case SymbolKind.Local:
            case SymbolKind.Global:
            case SymbolKind.TemplateParameter:
            case SymbolKind.Parameter:
            case SymbolKind.Method when ((MethodSymbol)symbol).methodKind == MethodKind.LocalFunction:
                return true;
            case SymbolKind.Field:
            case SymbolKind.Method:
                if (!symbol.isStatic)
                    throughType = null;

                return IsMemberAccessible(symbol.containingType, symbol.accessibility, within, throughType, out failedThroughTypeCheck);
            default:
                throw ExceptionUtilities.UnexpectedValue(symbol.kind);
        }
    }

    private static bool IsMemberAccessible(
        NamedTypeSymbol containingType,
        Accessibility accessibility,
        Symbol within,
        TypeSymbol throughType,
        out bool failedThroughTypeCheck) {
        failedThroughTypeCheck = false;

        if ((object)containingType == within)
            return true;

        if (!IsNamedTypeAccessible(containingType, within))
            return false;

        if (accessibility == Accessibility.Public)
            return true;

        return IsNonPublicMemberAccessible(containingType, accessibility, within, throughType, out failedThroughTypeCheck);
    }

    private static bool IsNonPublicMemberAccessible(
        NamedTypeSymbol containingType,
        Accessibility accessibility,
        Symbol within,
        TypeSymbol throughType,
        out bool failedThroughTypeCheck) {
        failedThroughTypeCheck = false;

        var originalContainingType = containingType.originalDefinition;
        var withinType = within as NamedTypeSymbol;

        switch (accessibility) {
            case Accessibility.NotApplicable:
                return true;
            case Accessibility.Private:
                return (object)withinType is not null && IsPrivateSymbolAccessible(withinType, originalContainingType);
            case Accessibility.Protected:
                return IsProtectedSymbolAccessible(
                    withinType,
                    originalContainingType,
                    throughType,
                    out failedThroughTypeCheck
                );
            default:
                throw ExceptionUtilities.UnexpectedValue(accessibility);
        }
    }

    private static bool IsPrivateSymbolAccessible(Symbol within, NamedTypeSymbol originalContainingType) {
        if (within is not NamedTypeSymbol withinType)
            return false;

        return IsNestedWithinOriginalContainingType(withinType, originalContainingType);
    }

    private static bool IsProtectedSymbolAccessible(
        NamedTypeSymbol withinType,
        NamedTypeSymbol originalContainingType,
        TypeSymbol throughType,
        out bool failedThroughTypeCheck) {
        failedThroughTypeCheck = false;

        if (withinType is null)
            return false;

        if (IsNestedWithinOriginalContainingType(withinType, originalContainingType))
            return true;

        var current = withinType.originalDefinition;
        var originalThroughType = throughType?.originalDefinition;

        while (current is not null) {
            if (current.InheritsFromIgnoringConstruction(originalContainingType)) {
                if (originalThroughType is null || originalThroughType.InheritsFromIgnoringConstruction(current))
                    return true;
                else
                    failedThroughTypeCheck = true;
            }

            current = current.containingType;
        }

        return false;
    }

    private static bool IsNestedWithinOriginalContainingType(
        NamedTypeSymbol withinType,
        NamedTypeSymbol originalContainingType) {
        var current = withinType.originalDefinition;

        while (current is not null) {
            if (current == (object)originalContainingType)
                return true;

            current = current.containingType;
        }

        return false;
    }

    private static bool IsNamedTypeAccessible(NamedTypeSymbol type, Symbol within) {
        return type.containingType is null
            || IsMemberAccessible(type.containingType, type.accessibility, within, null, out _);
    }

    #endregion

    #region Flags

    internal Binder WithAdditionalFlagsAndContainingMember(BinderFlags flags, Symbol containing) {
        return this.flags.Includes(flags)
            ? new BinderWithContainingMember(this, containing)
            : new BinderWithContainingMember(this, this.flags | flags, containing);
    }

    #endregion

    #region Constraints

    internal ImmutableArray<TypeParameterConstraintClause> GetDefaultTypeParameterConstraintClauses(
        TemplateParameterListSyntax templateParameterList) {
        var builder = ArrayBuilder<TypeParameterConstraintClause>.GetInstance(
            templateParameterList.parameters.Count,
            GetDefaultTypeParameterConstraintClause()
        );

        return builder.ToImmutable();
    }

    internal TypeParameterConstraintClause GetDefaultTypeParameterConstraintClause() {
        return TypeParameterConstraintClause.Empty;
    }

    internal ImmutableArray<TypeParameterConstraintClause> BindTypeParameterConstraintClauses(
        Symbol containingSymbol,
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        TemplateParameterListSyntax templateParameterList,
        SyntaxList<TemplateConstraintClauseSyntax> clauses,
        BelteDiagnosticQueue diagnostics) {
        var n = templateParameters.Length;
        var names = new Dictionary<string, int>(n, StringOrdinalComparer.Instance);

        foreach (var templateParameter in templateParameters) {
            var name = templateParameter.name;

            if (!names.ContainsKey(name))
                names.Add(name, names.Count);
        }

        var results = ArrayBuilder<TypeParameterConstraintClause>.GetInstance(n, fillWithValue: null);
        var syntaxNodes = ArrayBuilder<ArrayBuilder<TemplateConstraintClauseSyntax>>
            .GetInstance(n, fillWithValue: null);

        foreach (var clause in clauses) {
            if (clause.expressionConstraint is not null)
                continue;

            var name = clause.extendConstraint is null
                ? clause.isConstraint.name.identifier
                : clause.extendConstraint.name.identifier;

            if (names.TryGetValue(name.text, out var ordinal)) {
                if (syntaxNodes[ordinal] is null)
                    syntaxNodes[ordinal] = ArrayBuilder<TemplateConstraintClauseSyntax>.GetInstance();

                syntaxNodes[ordinal].Add(clause);
            } else {
                diagnostics.Push(Error.UnknownTemplate(name.location, containingSymbol.name, name.text));
            }
        }

        foreach (var parameter in templateParameters) {
            names.TryGetValue(parameter.name, out var ordinal);

            if (syntaxNodes[ordinal] is not null) {
                var constraintClause = BindTypeParameterConstraints(templateParameterList.parameters[ordinal], syntaxNodes[ordinal], diagnostics);
                results[ordinal] = constraintClause;
            }
        }

        for (var i = 0; i < n; i++) {
            if (results[i] is null)
                results[i] = GetDefaultTypeParameterConstraintClause();
        }

        foreach (var typeConstraintsSyntaxes in syntaxNodes)
            typeConstraintsSyntaxes?.Free();

        syntaxNodes.Free();

        return results.ToImmutableAndFree();
    }

    private TypeParameterConstraintClause BindTypeParameterConstraints(
        ParameterSyntax templateParameter,
        ArrayBuilder<TemplateConstraintClauseSyntax> constraints,
        BelteDiagnosticQueue diagnostics) {
        // TODO
        // Need to bind type first to make sure this template is a type parameter
        return TypeParameterConstraintClause.Empty;
    }

    #endregion
}

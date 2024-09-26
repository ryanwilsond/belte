using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using Buckle.CodeAnalysis.FlowAnalysis;
using Buckle.CodeAnalysis.Lowering;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries.Standard;
using Buckle.Utilities;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Binds a <see cref="Syntax.InternalSyntax.LanguageParser" /> output into a immutable "bound" tree. This is where most
/// error checking happens. The <see cref="Lowerer" /> is also called here to simplify the code and convert control of
/// flow into gotos and labels. Dead code is also removed here, as well as other optimizations.
/// </summary>
internal class Binder {
    protected readonly OverloadResolution _overloadResolution;
    protected readonly Conversions _conversions;
    protected readonly Compilation _compilation;
    protected readonly BinderFlags _flags;

    private Binder() {
        _conversions = new Conversions(this);
        _overloadResolution = new OverloadResolution(this);
        diagnostics = new BelteDiagnosticQueue();
    }

    protected Binder(Compilation compilation, BinderFlags flags) : this() {
        _compilation = compilation;
        _flags = flags;
    }

    protected virtual SyntaxNode _scopeDesignator => null;

    protected virtual Symbol _containingMember => null;

    protected virtual LabelSymbol _breakLabel => null;

    protected virtual LabelSymbol _continueLabel => null;

    protected virtual bool _inMethod => false;

    protected virtual LocalVariableSymbol _localInProgress => null;

    protected NamedTypeSymbol _containingType => _containingMember switch {
        null => null,
        NamedTypeSymbol namedType => namedType,
        _ => _containingMember.containingType
    };

    internal BelteDiagnosticQueue diagnostics { get; }

    internal static BoundGlobalScope BindGlobalScope(
        Compilation compilation,
        BoundGlobalScope previous,
        ImmutableArray<SyntaxTree> syntaxTrees) {

    }

    protected virtual ImmutableArray<LocalVariableSymbol> GetDeclaredLocalsForScope(SyntaxNode scopeDesignator) {
        return default;
    }

    private bool IsSymbolAccessible(Symbol symbol, NamedTypeSymbol within, TypeSymbol throughType = null) {
        return IsSymbolAccessible(symbol, within, throughType, out _);
    }

    private bool IsSymbolAccessible(Symbol symbol, NamedTypeSymbol within, TypeSymbol throughType, out bool failedThroughTypeCheck) {
        failedThroughTypeCheck = false;

        if (_flags.Includes(BinderFlags.IgnoreAccessibility))
            return true;

        return IsSymbolAccessibleInternal(symbol, within, throughType, out failedThroughTypeCheck);
    }

    private static bool IsSymbolAccessibleInternal(Symbol symbol, NamedTypeSymbol within, TypeSymbol throughType, out bool failedThroughTypeCheck) {
        failedThroughTypeCheck = false;

        switch (symbol.kind) {
            case SymbolKind.Type:
            case SymbolKind.LocalVariable:
            case SymbolKind.GlobalVariable:
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
            if (current.InheritsFrom(originalContainingType)) {
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
}

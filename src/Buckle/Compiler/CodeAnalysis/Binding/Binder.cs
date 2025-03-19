using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Lowering;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Binds a <see cref="Syntax.InternalSyntax.LanguageParser" /> output into a immutable "bound" tree. This is where most
/// error checking happens. The <see cref="Lowerer" /> is also called here to simplify the code and convert control of
/// flow into gotos and labels. Dead code is also removed here, as well as other optimizations.
/// </summary>
internal partial class Binder {

    #region Internal Model

    [ThreadStatic] private static PooledDictionary<SyntaxNode, int>? LambdaBindings;

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
        compilation = next.compilation;
    }

    private protected Binder(Binder next, BinderFlags flags) {
        this.next = next;
        this.flags = flags;
        compilation = next.compilation;
    }

    internal virtual SyntaxNode scopeDesignator => null;

    internal virtual bool isLocalFunctionsScopeBinder => false;

    internal virtual bool isLabelsScopeBinder => false;

    internal virtual Symbol containingMember => next.containingMember;

    internal virtual SynthesizedLabelSymbol breakLabel => next.breakLabel;

    internal virtual SynthesizedLabelSymbol continueLabel => next.continueLabel;

    internal virtual bool inMethod => next.inMethod;

    internal virtual DataContainerSymbol localInProgress => next.localInProgress;

    internal virtual ConstantFieldsInProgress constantFieldsInProgress => next.constantFieldsInProgress;

    internal virtual BoundExpression conditionalReceiverExpression => next.conditionalReceiverExpression;

    internal virtual ConsList<FieldSymbol> fieldsBeingBound => next.fieldsBeingBound;

    internal virtual ImmutableArray<DataContainerSymbol> locals => [];

    internal virtual ImmutableArray<LocalFunctionSymbol> localFunctions => [];

    internal virtual ImmutableArray<LabelSymbol> labels => [];

    internal virtual bool isInMethodBody => next.isInMethodBody;

    internal virtual bool isNestedFunctionBinder => false;

    internal virtual bool isInsideNameof => next.isInsideNameof;

    internal NamedTypeSymbol containingType => containingMember switch {
        null => null,
        NamedTypeSymbol namedType => namedType,
        _ => containingMember.containingType
    };

    internal Compilation compilation { get; }

    internal BinderFlags flags { get; }

    internal Conversions conversions {
        get {
            if (_lazyConversions is null)
                Interlocked.CompareExchange(ref _lazyConversions, new Conversions(this), null);

            return _lazyConversions;
        }
    }

    internal OverloadResolution overloadResolution {
        get {
            if (_lazyOverloadResolution is null)
                Interlocked.CompareExchange(ref _lazyOverloadResolution, new OverloadResolution(this), null);

            return _lazyOverloadResolution;
        }
    }

    internal Binder next { get; }

    private protected virtual SyntaxNode _enclosingNameofArgument => next._enclosingNameofArgument;

    private protected virtual bool _inExecutableBinder => false;

    private protected bool _inConstructorInitializer => flags.Includes(BinderFlags.ConstructorInitializer);

    internal bool inFieldInitializer => flags.Includes(BinderFlags.FieldInitializer);

    internal bool inParameterDefaultValue => flags.Includes(BinderFlags.ParameterDefaultValue);

    internal virtual Binder GetBinder(SyntaxNode node) {
        return next.GetBinder(node);
    }

    internal virtual ImmutableArray<DataContainerSymbol> GetDeclaredLocalsForScope(SyntaxNode scopeDesignator) {
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

    private protected virtual SourceDataContainerSymbol LookupLocal(SyntaxToken identifier) {
        return next.LookupLocal(identifier);
    }

    private protected virtual LocalFunctionSymbol LookupLocalFunction(SyntaxToken identifier) {
        return next.LookupLocalFunction(identifier);
    }

    private protected virtual bool IsUnboundTypeAllowed(TemplateNameSyntax syntax) {
        return next.IsUnboundTypeAllowed(syntax);
    }

    private bool IsSymbolAccessible(Symbol symbol, NamedTypeSymbol within, TypeSymbol throughType = null) {
        return flags.Includes(BinderFlags.IgnoreAccessibility) ||
            AccessCheck.IsSymbolAccessible(symbol, within, throughType);
    }

    private bool IsSymbolAccessible(
        Symbol symbol,
        NamedTypeSymbol within,
        TypeSymbol throughType,
        out bool failedThroughTypeCheck) {
        if (flags.Includes(BinderFlags.IgnoreAccessibility)) {
            failedThroughTypeCheck = false;
            return true;
        }

        return AccessCheck.IsSymbolAccessible(
            symbol,
            within,
            throughType,
            out failedThroughTypeCheck
        );
    }

    internal Binder WithAdditionalFlags(BinderFlags flags) {
        return this.flags.Includes(flags) ? this : new Binder(this, this.flags | flags);
    }

    internal Binder WithContainingMember(Symbol containing) {
        return new BinderWithContainingMember(this, containing);
    }

    internal Binder WithAdditionalFlagsAndContainingMember(BinderFlags flags, Symbol containing) {
        return this.flags.Includes(flags)
            ? new BinderWithContainingMember(this, containing)
            : new BinderWithContainingMember(this, this.flags | flags, containing);
    }

    internal BoundExpression WrapWithVariablesIfAny(BelteSyntaxNode scopeDesignator, BoundExpression expression) {
        var locals = GetDeclaredLocalsForScope(scopeDesignator);

        // TODO What is BoundSequence
        // return locals.IsEmpty
        //     ? expression
        //     : new BoundSequence(scopeDesignator, locals, ImmutableArray<BoundExpression>.Empty, expression, getType()) { WasCompilerGenerated = true };
        return expression;
    }

    internal BoundStatement WrapWithVariablesIfAny(BelteSyntaxNode scopeDesignator, BoundStatement statement) {
        var locals = GetDeclaredLocalsForScope(scopeDesignator);

        if (locals.IsEmpty)
            return statement;

        return new BoundBlockStatement(statement.syntax, [statement], locals, []);
    }

    internal BoundStatement WrapWithVariablesAndLocalFunctionsIfAny(
        BelteSyntaxNode scopeDesignator,
        BoundStatement statement) {
        var locals = GetDeclaredLocalsForScope(scopeDesignator);
        var localFunctions = GetDeclaredLocalFunctionsForScope(scopeDesignator);

        if (locals.IsEmpty && localFunctions.IsEmpty)
            return statement;

        return new BoundBlockStatement(statement.syntax, [statement], locals, localFunctions);
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
                var constraintClause = BindTypeParameterConstraints(
                    templateParameterList.parameters[ordinal],
                    syntaxNodes[ordinal],
                    diagnostics
                );

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
        // TODO templates
        // Need to bind type first to make sure this template is a type parameter
        return null;
    }

    #endregion

    #region Symbols

    internal TypeWithAnnotations BindType(
        ExpressionSyntax syntax,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved = null) {
        TypeWithAnnotations nonNullableType;

        switch (syntax.kind) {
            case SyntaxKind.NonNullableType:
                return BindNonNullable();
            case SyntaxKind.IdentifierName:
                nonNullableType = BindNonTemplateSimpleType(
                    (IdentifierNameSyntax)syntax,
                    diagnostics,
                    basesBeingResolved,
                    null
                );

                break;
            case SyntaxKind.TemplateName:
                nonNullableType = BindTemplateSimpleType(
                    (TemplateNameSyntax)syntax,
                    diagnostics,
                    basesBeingResolved,
                    null
                );

                break;
            case SyntaxKind.QualifiedName: {
                    var node = (QualifiedNameSyntax)syntax;
                    nonNullableType = BindQualifiedName(node.left, node.right, diagnostics, basesBeingResolved);
                    break;
                }
            case SyntaxKind.MemberAccessExpression: {
                    var node = (MemberAccessExpressionSyntax)syntax;
                    nonNullableType = BindQualifiedName(node.expression, node.name, diagnostics, basesBeingResolved);
                    break;
                }
            case SyntaxKind.ArrayType:
                nonNullableType = BindArrayType(
                    (ArrayTypeSyntax)syntax,
                    diagnostics,
                    false,
                    basesBeingResolved
                );

                break;
            case SyntaxKind.ReferenceType: {
                    var referenceTypeSyntax = (ReferenceTypeSyntax)syntax;
                    var refToken = referenceTypeSyntax.refKeyword;
                    diagnostics.Push(Error.UnexpectedToken(refToken.location, refToken.kind));
                    return BindType(referenceTypeSyntax.type, diagnostics, basesBeingResolved);
                }
            default:
                return new TypeWithAnnotations(CreateErrorType());
        }

        if (nonNullableType.specialType == SpecialType.Void)
            return nonNullableType;

        return nonNullableType.SetIsAnnotated();

        TypeWithAnnotations BindNonNullable() {
            var nonNullableSyntax = (NonNullableTypeSyntax)syntax;
            var nullableType = BindType(nonNullableSyntax.type, diagnostics, basesBeingResolved);
            return new TypeWithAnnotations(nullableType.type.GetNullableUnderlyingType(), false);
        }
    }

    private NamespaceOrTypeSymbol GetContainingNamespaceOrType(Symbol symbol) {
        return symbol.ContainingNamespaceOrType() ?? compilation.globalNamespaceInternal;
    }

    private BestSymbolInfo GetBestSymbolInfo(ArrayBuilder<Symbol> symbols, out BestSymbolInfo secondBest) {
        var first = default(BestSymbolInfo);
        var second = default(BestSymbolInfo);

        for (var i = 0; i < symbols.Count; i++) {
            var symbol = symbols[i];
            BestSymbolLocation location;

            if (symbol.kind == SymbolKind.Namespace) {
                location = BestSymbolLocation.None;
                var ns = (NamespaceSymbol)symbol;

                var current = GetLocation(compilation, ns);

                if (BestSymbolInfo.IsSecondLocationBetter(location, current)) {
                    location = current;

                    if (location == BestSymbolLocation.FromSourceModule)
                        break;
                }
            } else {
                location = GetLocation(compilation, symbol);
            }

            var third = new BestSymbolInfo(location, i);

            if (BestSymbolInfo.Sort(ref second, ref third))
                BestSymbolInfo.Sort(ref first, ref second);
        }

        secondBest = second;

        return first;
    }

    private static BestSymbolLocation GetLocation(Compilation compilation, Symbol symbol) {
        if (symbol.declaringCompilation == compilation)
            return BestSymbolLocation.FromSourceModule;
        else if (symbol.declaringCompilation is not null)
            return BestSymbolLocation.FromAddedModule;
        else
            return BestSymbolLocation.FromCorLibrary;
    }

    private TypeWithAnnotations BindArrayType(
        ArrayTypeSyntax node,
        BelteDiagnosticQueue diagnostics,
        bool permitDimensions,
        ConsList<TypeSymbol> basesBeingResolved) {
        var type = BindType(node.elementType, diagnostics, basesBeingResolved);

        if (type.isStatic)
            diagnostics.Push(Error.ArrayOfStaticType(node.elementType.location, type.type));

        for (var i = node.rankSpecifiers.Count - 1; i >= 0; i--) {
            var rankSpecifier = node.rankSpecifiers[i];
            var dimension = rankSpecifier.size;

            if (!permitDimensions && dimension is not null)
                diagnostics.Push(Error.ArraySizeInDeclaration(rankSpecifier.size.location));

            // TODO need to error check the size, should allow dynamic?
            // var size = (int)BindExpression(dimension, diagnostics).constantValue.value;
            var array = ArrayTypeSymbol.CreateArray(type);
            type = new TypeWithAnnotations(array);
        }

        return type;
    }

    private protected TypeWithAnnotations BindNonTemplateSimpleType(
        IdentifierNameSyntax node,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved,
        NamespaceOrTypeSymbol qualifier) {
        var name = node.identifier.text;

        if (string.IsNullOrWhiteSpace(name)) {
            var error = Error.UndefinedSymbol(node.location, name);

            return new TypeWithAnnotations(
                new ExtendedErrorTypeSymbol(compilation.globalNamespaceInternal, name, 0, error)
            );
        }

        // TODO template
        // var errorResult = CreateErrorIfLookupOnTypeParameter(node.Parent, qualifierOpt, identifierValueText, 0, diagnostics);
        // if (errorResult is not null) {
        //     return TypeWithAnnotations.Create(errorResult);
        // }

        if (qualifier is null) {
            var specialType = SpecialTypes.GetTypeFromMetadataName(string.Concat("global::", name));

            if (specialType != SpecialType.None)
                return new TypeWithAnnotations(CorLibrary.GetSpecialType(specialType));
        }

        var result = LookupResult.GetInstance();
        var options = LookupOptions.NamespacesOrTypesOnly;

        LookupSymbolsSimpleName(result, qualifier, name, 0, basesBeingResolved, options, true);

        var bindingResult = ResultSymbol(result, name, 0, node, diagnostics, out _, qualifier, options);

        result.Free();
        return new TypeWithAnnotations((TypeSymbol)bindingResult);
    }

    private TypeWithAnnotations BindQualifiedName(
        ExpressionSyntax leftName,
        SimpleNameSyntax rightName,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved) {
        var left = BindType(leftName, diagnostics, basesBeingResolved).type;

        var isLeftUnboundTemplateType = left.kind == SymbolKind.NamedType &&
            ((NamedTypeSymbol)left).isUnboundTemplateType;

        if (isLeftUnboundTemplateType)
            left = ((NamedTypeSymbol)left).originalDefinition;

        var right = BindSimpleType(rightName, diagnostics, basesBeingResolved, left);

        if (isLeftUnboundTemplateType)
            return ConvertToUnbound();

        return right;

        TypeWithAnnotations ConvertToUnbound() {
            var namedTypeRight = right.type as NamedTypeSymbol;

            if (namedTypeRight is not null && namedTypeRight.isTemplateType)
                return new TypeWithAnnotations(namedTypeRight.AsUnboundTemplateType(), right.isNullable);

            return right;
        }
    }

    private TypeWithAnnotations BindSimpleType(
        SimpleNameSyntax node,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved,
        NamespaceOrTypeSymbol qualifier = null) {
        return node.kind switch {
            SyntaxKind.IdentifierName
                => BindNonTemplateSimpleType((IdentifierNameSyntax)node, diagnostics, basesBeingResolved, qualifier),
            SyntaxKind.TemplateName
                => BindTemplateSimpleType((TemplateNameSyntax)node, diagnostics, basesBeingResolved, qualifier),
            _ => new TypeWithAnnotations(
                new ExtendedErrorTypeSymbol(qualifier ?? compilation.globalNamespaceInternal, "", 0, null)
            )
        };
    }

    private TypeWithAnnotations BindTemplateSimpleType(
        TemplateNameSyntax node,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved,
        NamespaceOrTypeSymbol qualifier) {
        // TODO template
        return new TypeWithAnnotations(CreateErrorType());
    }

    internal TypeWithAnnotations BindTypeOrImplicitType(
        TypeSyntax syntax,
        BelteDiagnosticQueue diagnostics,
        out bool isImplicitlyTyped) {
        if (syntax.isImplicitlyTyped) {
            isImplicitlyTyped = true;
            return new TypeWithAnnotations(null, true);
        } else {
            isImplicitlyTyped = false;
            return BindType(syntax, diagnostics);
        }
    }

    private ImmutableArray<(string, TypeOrConstant)> BindTemplateArguments(
        SeparatedSyntaxList<BaseArgumentSyntax> templateArguments,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved = null) {
        var arguments = ArrayBuilder<(string, TypeOrConstant)>.GetInstance(templateArguments.Count);

        foreach (var argumentSyntax in templateArguments)
            arguments.Add(BindTemplateArgument(argumentSyntax, diagnostics, basesBeingResolved));

        return arguments.ToImmutableAndFree();
    }

    private (string, TypeOrConstant) BindTemplateArgument(
        BaseArgumentSyntax templateArgument,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved = null) {
        // TODO Implement normal arguments first
        // var name = templateArgument.identifier.text;
        // var value = BindExpression(templateArgument.expression, diagnostics);

        // return (name, value);
        return (null, null);
    }

    internal NamedTypeSymbol CreateErrorType(string name = "") {
        return new ExtendedErrorTypeSymbol(compilation, name, 0, null);
    }

    internal void ValidateParameterNameConflicts(
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        ImmutableArray<ParameterSymbol> parameters,
        BelteDiagnosticQueue diagnostics) {
        PooledHashSet<string>? tpNames = null;

        if (!templateParameters.IsDefaultOrEmpty) {
            tpNames = PooledHashSet<string>.GetInstance();

            foreach (var tp in templateParameters) {
                var name = tp.name;

                if (string.IsNullOrEmpty(name))
                    continue;

                if (!tpNames.Add(name)) {
                    // Type parameter declaration name conflicts are detected elsewhere
                }
            }
        }

        PooledHashSet<string>? pNames = null;

        if (!parameters.IsDefaultOrEmpty) {
            pNames = PooledHashSet<string>.GetInstance();

            foreach (var p in parameters) {
                var name = p.name;

                if (string.IsNullOrEmpty(name))
                    continue;

                if (tpNames is not null && tpNames.Contains(name)) {
                    // TODO error
                    // CS0412: 'X': a parameter or local variable cannot have the same name as a method type parameter
                    // diagnostics.Add(ErrorCode.ERR_LocalSameNameAsTypeParam, GetLocation(p), name);
                }

                if (!pNames.Add(name)) {
                    // TODO error
                    // The parameter name '{0}' is a duplicate
                    // diagnostics.Add(ErrorCode.ERR_DuplicateParamName, GetLocation(p), name);
                }
            }
        }

        tpNames?.Free();
        pNames?.Free();
    }

    #endregion

    #region Expressions

    internal static bool WasImplicitReceiver(BoundExpression receiver) {
        if (receiver is null)
            return true;

        return receiver.kind switch {
            BoundKind.ThisExpression => true,
            _ => false,
        };
    }

    internal static bool IsMemberAccessedThroughType(BoundExpression receiver) {
        if (receiver is null)
            return false;

        return receiver.kind == BoundKind.TypeExpression;
    }

    internal BoundExpression BindExpression(ExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        return BindExpressionInternal(node, diagnostics, false, false);
    }

    internal BoundExpression BindToNaturalType(
        BoundExpression expression,
        BelteDiagnosticQueue diagnostics,
        bool reportNoTargetType = true) {
        if (!expression.NeedsToBeConverted())
            return expression;

        BoundExpression result;

        switch (expression) {
            case BoundUnconvertedInitializerList list:
                if (reportNoTargetType && !expression.hasErrors)
                    diagnostics.Push(Error.ListNoTargetType(expression.syntax.location));

                result = BindListForErrorRecovery(list, CreateErrorType(), diagnostics);
                break;
            default:
                result = expression;
                break;
        }

        return result;
    }

    internal BoundExpression BindToTypeForErrorRecovery(BoundExpression expression, TypeSymbol type = null) {
        if (expression is null)
            return null;

        var result = !expression.NeedsToBeConverted()
            ? expression
            : type is null
                ? BindToNaturalType(expression, BelteDiagnosticQueue.Discarded, reportNoTargetType: false)
                : GenerateConversionForAssignment(type, expression, BelteDiagnosticQueue.Discarded);

        return result;
    }

    internal BoundExpression BindRValueWithoutTargetType(
        ExpressionSyntax node,
        BelteDiagnosticQueue diagnostics,
        bool reportNoTargetType = true) {
        return BindToNaturalType(BindValue(node, diagnostics, BindValueKind.RValue), diagnostics, reportNoTargetType);
    }

    private BoundInitializerList BindListForErrorRecovery(
        BoundUnconvertedInitializerList node,
        TypeSymbol targetType,
        BelteDiagnosticQueue diagnostics) {
        var syntax = node.syntax;
        var builder = ArrayBuilder<BoundExpression>.GetInstance(node.items.Length);

        foreach (var item in node.items) {
            var result = item is BoundExpression expression
                ? BindToNaturalType(expression, diagnostics, reportNoTargetType: !targetType.IsErrorType())
                : item;

            builder.Add(result);
        }

        return new BoundInitializerList(
            syntax,
            builder.ToImmutableAndFree(),
            targetType,
            true
        );
    }

    internal BoundExpression BindValue(
        ExpressionSyntax node,
        BelteDiagnosticQueue diagnostics,
        BindValueKind valueKind) {
        var result = BindExpressionInternal(node, diagnostics, false, false);
        return CheckValue(result, valueKind, diagnostics);
    }

    internal BoundExpression BindDataContainerInitializerValue(
        EqualsValueClauseSyntax initializer,
        RefKind refKind,
        TypeSymbol varType,
        BelteDiagnosticQueue diagnostics) {
        if (initializer is null)
            return null;

        IsInitializerRefKindValid(initializer, initializer, refKind, diagnostics, out var valueKind, out var value);
        var boundInitializer = BindPossibleArrayInitializer(value, varType, valueKind, diagnostics);
        boundInitializer = GenerateConversionForAssignment(varType, boundInitializer, diagnostics);
        return boundInitializer;
    }

    internal BoundExpression BindInferredDataContainerInitializer(
        BelteDiagnosticQueue diagnostics,
        RefKind refKind,
        EqualsValueClauseSyntax initializer,
        BelteSyntaxNode errorSyntax) {
        IsInitializerRefKindValid(initializer, initializer, refKind, diagnostics, out var valueKind, out var value);
        return BindInferredVariableInitializer(diagnostics, value, valueKind, errorSyntax);
    }

    internal Binder CreateBinderForParameterDefaultValue(Symbol parameter, EqualsValueClauseSyntax defaultValueSyntax) {
        var binder = new LocalScopeBinder(
            WithAdditionalFlagsAndContainingMember(BinderFlags.ParameterDefaultValue, parameter.containingSymbol)
        );

        return new ExecutableCodeBinder(defaultValueSyntax, parameter.containingSymbol, binder);
    }

    internal BoundExpression BindConstructorInitializer(
        ArgumentListSyntax initializerArgumentList,
        MethodSymbol constructor,
        BelteDiagnosticQueue diagnostics) {
        Binder argumentListBinder = null;

        if (initializerArgumentList is not null)
            argumentListBinder = GetBinder(initializerArgumentList);

        var result = (argumentListBinder ?? this)
            .BindConstructorInitializerCore(initializerArgumentList, constructor, diagnostics);

        if (argumentListBinder is not null) {
            result = argumentListBinder.WrapWithVariablesIfAny(initializerArgumentList, result);
        }

        return result;
    }

    internal BoundEqualsValue BindParameterDefaultValue(
        EqualsValueClauseSyntax defaultValueSyntax,
        Symbol parameter,
        BelteDiagnosticQueue diagnostics,
        out BoundExpression valueBeforeConversion) {
        var defaultValueBinder = GetBinder(defaultValueSyntax);
        valueBeforeConversion = defaultValueBinder.BindValue(
            defaultValueSyntax.value,
            diagnostics,
            BindValueKind.RValue
        );

        var isTemplate = parameter is TemplateParameterSymbol;

        var parameterType = parameter is ParameterSymbol p
            ? p.type
            : (parameter as TemplateParameterSymbol).underlyingType.type;

        var locals = defaultValueBinder.GetDeclaredLocalsForScope(defaultValueSyntax);
        var value = defaultValueBinder.GenerateConversionForAssignment(
            parameterType,
            valueBeforeConversion,
            diagnostics,
            ConversionForAssignmentFlags.DefaultParameter
        );

        if (isTemplate) {
            return new BoundTemplateParameterEqualsValue(
                defaultValueSyntax,
                (TemplateParameterSymbol)parameter,
                locals,
                value
            );
        } else {
            return new BoundParameterEqualsValue(
                defaultValueSyntax,
                (ParameterSymbol)parameter,
                locals,
                value
            );
        }
    }

    internal BoundFieldEqualsValue BindFieldInitializer(
        FieldSymbol field,
        EqualsValueClauseSyntax initializer,
        BelteDiagnosticQueue diagnostics) {
        if (initializer is null)
            return null;

        var initializerBinder = GetBinder(initializer);
        var result = initializerBinder.BindVariableOrAutoPropInitializerValue(
            initializer,
            field.refKind,
            field.GetFieldType(initializerBinder.fieldsBeingBound).type,
            diagnostics
        );

        return new BoundFieldEqualsValue(
            initializer,
            field,
            initializerBinder.GetDeclaredLocalsForScope(initializer),
            result
        );
    }

    internal BoundExpression BindVariableOrAutoPropInitializerValue(
        EqualsValueClauseSyntax initializerOpt,
        RefKind refKind,
        TypeSymbol varType,
        BelteDiagnosticQueue diagnostics) {
        if (initializerOpt is null)
            return null;

        IsInitializerRefKindValid(
            initializerOpt,
            initializerOpt,
            refKind,
            diagnostics,
            out var valueKind,
            out var value
        );

        var initializer = BindPossibleArrayInitializer(value, varType, valueKind, diagnostics);
        initializer = GenerateConversionForAssignment(varType, initializer, diagnostics);
        return initializer;
    }

    internal static BoundCallExpression GenerateBaseParameterlessConstructorInitializer(
        MethodSymbol constructor,
        BelteDiagnosticQueue diagnostics) {
        var containingType = constructor.containingType;
        var baseType = containingType.baseType;
        MethodSymbol baseConstructor = null;
        var resultKind = LookupResultKind.Viable;

        foreach (var ctor in baseType.constructors) {
            if (ctor.parameterCount == 0) {
                baseConstructor = ctor;
                break;
            }
        }

        var hasErrors = false;

        if (!AccessCheck.IsSymbolAccessible(baseConstructor, containingType)) {
            diagnostics.Push(Error.MemberIsInaccessible(constructor.location, baseConstructor));
            resultKind = LookupResultKind.Inaccessible;
            hasErrors = true;
        }

        var syntax = constructor.GetNonNullSyntaxNode();
        var receiver = new BoundThisExpression(syntax, containingType);
        return new BoundCallExpression(
            syntax,
            receiver,
            baseConstructor,
            [],
            [],
            BitVector.Empty,
            resultKind,
            baseConstructor.returnType,
            hasErrors
        );
    }

    private static bool IsInitializerRefKindValid(
        EqualsValueClauseSyntax initializer,
        BelteSyntaxNode node,
        RefKind variableRefKind,
        BelteDiagnosticQueue diagnostics,
        out BindValueKind valueKind,
        out ExpressionSyntax value) {
        var expressionRefKind = RefKind.None;
        value = initializer?.value.UnwrapRefExpression(out expressionRefKind);

        if (variableRefKind == RefKind.None) {
            valueKind = BindValueKind.RValue;
            if (expressionRefKind == RefKind.Ref) {
                diagnostics.Push(Error.InitializeByValueWithByReference(node.location));
                return false;
            }
        } else {
            valueKind = variableRefKind == RefKind.RefConst
                ? BindValueKind.RefConst
                : BindValueKind.RefOrOut;

            if (initializer is null) {
                // Error(diagnostics, ErrorCode.ERR_ByReferenceVariableMustBeInitialized, node);
                // TODO should we error here?
                return false;
            } else if (expressionRefKind != RefKind.Ref) {
                diagnostics.Push(Error.InitializeByReferenceWithByValue(node.location));
                return false;
            }
        }

        return true;
    }

    private BoundExpression BindPossibleArrayInitializer(
        ExpressionSyntax node,
        TypeSymbol destinationType,
        BindValueKind valueKind,
        BelteDiagnosticQueue diagnostics) {
        return BindValue(node, diagnostics, valueKind);
        // TODO Do we want to distinguish collection expressions from array initializers?

        if (node.kind != SyntaxKind.InitializerListExpression)
            return BindValue(node, diagnostics, valueKind);

        BoundExpression result;
        if (destinationType.kind == SymbolKind.ArrayType) {
            result = BindArrayCreationWithInitializer(
                diagnostics,
                null,
                (InitializerListExpressionSyntax)node,
                (ArrayTypeSymbol)destinationType,
                []
            );
        } else {
            // TODO Pretty sure we can just do nothing and a conversion error will be produced somewhere
            result = BindValue(node, diagnostics, valueKind);
            // result = BindUnexpectedArrayInitializer((InitializerExpressionSyntax)node, diagnostics, ErrorCode.ERR_ArrayInitToNonArrayType);
            // result = null;
        }

        return CheckValue(result, valueKind, diagnostics);
    }

    private BoundExpression CheckValue(
        BoundExpression expression,
        BindValueKind kind,
        BelteDiagnosticQueue diagnostics) {
        switch (expression.kind) {
            case BoundKind.UnconvertedInitializerList:
                if (kind == BindValueKind.RValue)
                    return expression;

                break;
        }

        var hasResolutionErrors = false;

        if (expression.kind == BoundKind.MethodGroup && kind != BindValueKind.RValueOrMethodGroup) {
            var methodGroup = (BoundMethodGroup)expression;
            var resolution = ResolveMethodGroup(methodGroup, analyzedArguments: null);
            Symbol otherSymbol = null;
            var resolvedToMethodGroup = resolution.methodGroup is not null;

            if (!expression.hasErrors) diagnostics.PushRange(resolution.diagnostics);

            hasResolutionErrors = resolution.hasAnyErrors;

            if (hasResolutionErrors)
                otherSymbol = resolution.otherSymbol;

            resolution.Free();

            if (!resolvedToMethodGroup) {
                var receiver = methodGroup.receiver;

                return new BoundErrorExpression(
                    expression.syntax,
                    methodGroup.resultKind,
                    otherSymbol is null ? [] : [otherSymbol],
                    receiver == null ? [] : [receiver],
                    GetNonMethodMemberType(otherSymbol)
                );
            }
        }

        if (!hasResolutionErrors && CheckValueKind(expression.syntax, expression, kind, false, diagnostics) ||
            expression.hasErrors && kind == BindValueKind.RValueOrMethodGroup) {
            return expression;
        }

        var resultKind = (kind == BindValueKind.RValue || kind == BindValueKind.RValueOrMethodGroup)
            ? LookupResultKind.NotAValue
            : LookupResultKind.NotADataContainer;

        return ToErrorExpression(expression, resultKind);
    }

    internal bool CheckValueKind(
        SyntaxNode node,
        BoundExpression expression,
        BindValueKind valueKind,
        bool checkingReceiver,
        BelteDiagnosticQueue diagnostics) {
        if (expression.hasErrors)
            return false;

        // TODO
        return true;

        // if (RequiresRValueOnly(valueKind))
        //     return CheckNotNamespaceOrType(expression, diagnostics);

        // if ((expression.constantValue is not null) || (expression.type.GetSpecialTypeSafe() == SpecialType.Void)) {
        //     Error(diagnostics, GetStandardLvalueError(valueKind), node);
        //     return false;
        // }

        // switch (expression.kind) {
        //     case BoundKind.TypeExpression:
        //         var type = (BoundTypeExpression)expression;
        //         Error(diagnostics, ErrorCode.ERR_BadSKknown, node, type.Type, MessageID.IDS_SK_TYPE.Localize(), MessageID.IDS_SK_VARIABLE.Localize());
        //         return false;
        //     case BoundKind.MethodGroup:
        //         // method groups can only be used as RValues except when taking the address of one
        //         var methodGroup = (BoundMethodGroup)expression;
        //         Error(diagnostics, GetMethodGroupOrFunctionPointerLvalueError(valueKind), node, methodGroup.Name, MessageID.IDS_MethodGroup.Localize());
        //         return false;
        //     case BoundKind.Conversion:
        //         var conversion = (BoundConversion)expression;
        //         // conversions are strict RValues, but unboxing has a specific error
        //         if (conversion.ConversionKind == ConversionKind.Unboxing) {
        //             Error(diagnostics, ErrorCode.ERR_UnboxNotLValue, node);
        //             return false;
        //         }
        //         break;
        //     case BoundKind.Parameter:
        //         var parameter = (BoundParameter)expression;
        //         return CheckParameterValueKind(node, parameter, valueKind, checkingReceiver, diagnostics);
        //     case BoundKind.Local:
        //         var local = (BoundLocal)expression;
        //         return CheckLocalValueKind(node, local, valueKind, checkingReceiver, diagnostics);
        //     case BoundKind.ThisReference:
        //         // `this` is never ref assignable
        //         if (RequiresRefAssignableVariable(valueKind)) {
        //             Error(diagnostics, ErrorCode.ERR_RefLocalOrParamExpected, node);
        //             return false;
        //         }

        //         // We will already have given an error for "this" used outside of a constructor,
        //         // instance method, or instance accessor. Assume that "this" is a variable if it is in a struct.

        //         // SPEC: when this is used in a primary-expression within an instance constructor of a struct,
        //         // SPEC: it is classified as a variable.

        //         // SPEC: When this is used in a primary-expression within an instance method or instance accessor
        //         // SPEC: of a struct, it is classified as a variable.

        //         // Note: RValueOnly is checked at the beginning of this method. Since we are here we need more than readable.
        //         // "this" is readonly in members marked "readonly" and in members of readonly structs, unless we are in a constructor.
        //         var isValueType = ((BoundThisReference)expression).Type.IsValueType;
        //         if (!isValueType || (RequiresAssignableVariable(valueKind) && (this.ContainingMemberOrLambda as MethodSymbol)?.IsEffectivelyReadOnly == true)) {
        //             ReportThisLvalueError(node, valueKind, isValueType, isPrimaryConstructorParameter: false, diagnostics);
        //             return false;
        //         }

        //         return true;

        //     case BoundKind.Call:
        //         var call = (BoundCall)expression;
        //         return CheckMethodReturnValueKind(call.Method, call.Syntax, node, valueKind, checkingReceiver, diagnostics);

        //     case BoundKind.ConditionalOperator:
        //         if (RequiresRefAssignableVariable(valueKind)) {
        //             Error(diagnostics, ErrorCode.ERR_RefLocalOrParamExpected, node);
        //             return false;
        //         }

        //         var conditional = (BoundConditionalOperator)expression;

        //         // byref conditional defers to its operands
        //         if (conditional.IsRef &&
        //             (CheckValueKind(conditional.Consequence.Syntax, conditional.Consequence, valueKind, checkingReceiver: false, diagnostics: diagnostics) &
        //             CheckValueKind(conditional.Alternative.Syntax, conditional.Alternative, valueKind, checkingReceiver: false, diagnostics: diagnostics))) {
        //             return true;
        //         }

        //         // report standard lvalue error
        //         break;

        //     case BoundKind.FieldAccess: {
        //             var fieldAccess = (BoundFieldAccess)expression;
        //             return CheckFieldValueKind(node, fieldAccess, valueKind, checkingReceiver, diagnostics);
        //         }

        //     case BoundKind.AssignmentOperator:
        //         // Cannot ref-assign to a ref assignment.
        //         if (RequiresRefAssignableVariable(valueKind)) {
        //             Error(diagnostics, ErrorCode.ERR_RefLocalOrParamExpected, node);
        //             return false;
        //         }

        //         var assignment = (BoundAssignmentOperator)expression;
        //         return CheckSimpleAssignmentValueKind(node, assignment, valueKind, diagnostics);

        //     case BoundKind.ValuePlaceholder:
        //         // Strict RValue
        //         break;
        // }

        // // At this point we should have covered all the possible cases for anything that is not a strict RValue.
        // Error(diagnostics, GetStandardLvalueError(valueKind), node);
        // return false;
    }

    private BoundExpression BindConstructorInitializerCore(
        ArgumentListSyntax initializerArgumentListOpt,
        MethodSymbol constructor,
        BelteDiagnosticQueue diagnostics) {
        // TODO constructors
        return null;
    }

    private BoundExpression BindExpressionInternal(
        ExpressionSyntax node,
        BelteDiagnosticQueue diagnostics,
        bool called,
        bool indexed) {
        return node.kind switch {
            SyntaxKind.LiteralExpression => BindLiteralExpression((LiteralExpressionSyntax)node, diagnostics),
            SyntaxKind.ThisExpression => BindThisExpression((ThisExpressionSyntax)node, diagnostics),
            SyntaxKind.BaseExpression => BindBaseExpression((BaseExpressionSyntax)node, diagnostics),
            SyntaxKind.CallExpression => BindCallExpression((CallExpressionSyntax)node, diagnostics),
            SyntaxKind.QualifiedName => BindQualifiedName((QualifiedNameSyntax)node, diagnostics),
            SyntaxKind.ReferenceType => BindReferenceType((ReferenceTypeSyntax)node, diagnostics),
            SyntaxKind.NonNullableType => ErrorExpression(node), // TODO Confirm this is not reachable without err
            SyntaxKind.ParenthesizedExpression => BindParenthesisExpression((ParenthesisExpressionSyntax)node, diagnostics),
            SyntaxKind.MemberAccessExpression => BindMemberAccess((MemberAccessExpressionSyntax)node, called, indexed, diagnostics),
            SyntaxKind.IdentifierName or SyntaxKind.TemplateName => BindIdentifier((SimpleNameSyntax)node, called, indexed, diagnostics),
            SyntaxKind.BinaryExpression => BindBinaryExpression((BinaryExpressionSyntax)node, diagnostics),
            SyntaxKind.UnaryExpression => BindUnaryExpression((UnaryExpressionSyntax)node, diagnostics),
            SyntaxKind.PrefixExpression => BindIncrementOperator(node, ((PrefixExpressionSyntax)node).operand, ((PrefixExpressionSyntax)node).operatorToken, diagnostics),
            SyntaxKind.PostfixExpression => BindIncrementOrNullAssertOperator((PostfixExpressionSyntax)node, diagnostics),
            SyntaxKind.TernaryExpression => BindTernaryExpression((TernaryExpressionSyntax)node, diagnostics),
            SyntaxKind.AssignmentExpression => BindAssignmentOperator((AssignmentExpressionSyntax)node, diagnostics),
            SyntaxKind.ObjectCreationExpression => BindObjectCreationExpression((ObjectCreationExpressionSyntax)node, diagnostics),
            SyntaxKind.NameOfExpression => BindNameOfExpression((NameOfExpressionSyntax)node, diagnostics),
            SyntaxKind.CastExpression => BindCastExpression((CastExpressionSyntax)node, diagnostics),
            SyntaxKind.InitializerListExpression => BindInitializerListExpression((InitializerListExpressionSyntax)node, diagnostics),
            SyntaxKind.ReferenceExpression => BindReferenceExpression((ReferenceExpressionSyntax)node, diagnostics),
            SyntaxKind.IndexExpression => BindIndexExpression((IndexExpressionSyntax)node, diagnostics),
            /*
            case SyntaxKind.InitializerDictionaryExpression:
                return BindInitializerDictionaryExpression((InitializerDictionaryExpressionSyntax)expression);
            case SyntaxKind.TypeOfExpression:
                return BindTypeOfExpression((TypeOfExpressionSyntax)expression);
            case SyntaxKind.ThrowExpression:
                return BindThrowExpression((ThrowExpressionSyntax)expression);
            */
            _ => throw ExceptionUtilities.UnexpectedValue(node.kind),
        };
    }

    private BoundErrorExpression ErrorExpression(SyntaxNode syntax, BoundExpression expression) {
        return new BoundErrorExpression(syntax, LookupResultKind.Empty, [], [expression], CreateErrorType(), true);
    }

    private BoundErrorExpression ErrorExpression(
        SyntaxNode syntax,
        LookupResultKind lookupResultKind,
        BoundExpression expression) {
        return new BoundErrorExpression(syntax, lookupResultKind, [], [expression], CreateErrorType(), true);
    }

    private BoundErrorExpression ErrorExpression(SyntaxNode syntax) {
        return new BoundErrorExpression(syntax, LookupResultKind.Empty, [], [], CreateErrorType(), true);
    }

    private BoundExpression ToErrorExpression(
        BoundExpression expression,
        LookupResultKind resultKind = LookupResultKind.Empty) {
        var resultType = expression.type;
        var expressionKind = expression.kind;

        if (expression.hasErrors && resultType is not null)
            return expression;

        if (expressionKind == BoundKind.ErrorExpression) {
            var errorExpression = (BoundErrorExpression)expression;

            return errorExpression.Update(
                resultKind,
                errorExpression.symbols,
                errorExpression.childBoundNodes,
                resultType
            );
        } else {
            var symbols = ArrayBuilder<Symbol>.GetInstance();
            expression.GetExpressionSymbols(symbols);

            return new BoundErrorExpression(
                expression.syntax,
                resultKind,
                symbols.ToImmutableAndFree(),
                [BindToTypeForErrorRecovery(expression)],
                resultType ?? CreateErrorType()
            );
        }
    }

    private BoundExpression BindNameOfExpression(NameOfExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var binder = GetBinder(node);
        return binder.BindNameOfInternal(node, diagnostics);
    }

    private BoundExpression BindNameOfInternal(NameOfExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var nameSyntax = node.name;
        var name = GetNameFromSyntax(nameSyntax);
        // This is just to collect diagnostics
        BindExpression(nameSyntax, diagnostics);

        return new BoundLiteralExpression(
            node,
            new ConstantValue(name, SpecialType.String),
            CorLibrary.GetSpecialType(SpecialType.String)
        );
    }

    private string GetNameFromSyntax(NameSyntax name) {
        return name switch {
            IdentifierNameSyntax identifier => identifier.identifier.text,
            TemplateNameSyntax template => template.identifier.text,
            QualifiedNameSyntax qualified => qualified.right.identifier.text,
            _ => throw ExceptionUtilities.UnexpectedValue(name.kind),
        };
    }

    private BoundExpression BindIndexExpression(IndexExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var boundExpression = BindValue(node.expression, diagnostics, BindValueKind.RValue);
        var boundOperand = BindValue(node.index, diagnostics, BindValueKind.RValue);
        var intType = CorLibrary.GetSpecialType(SpecialType.Int);

        if (boundOperand.type is not null && boundOperand.type.IsNullableType())
            intType = CorLibrary.GetNullableType(SpecialType.Int);

        var conversion = conversions.ClassifyImplicitConversionFromExpression(boundOperand, intType);

        if (!conversion.exists)
            GenerateImplicitConversionError(diagnostics, node, conversion, boundOperand, intType);

        var boundConversion = CreateConversion(boundOperand, conversion, intType, diagnostics);
        var hasErrors = false;
        var type = boundExpression.type.StrippedType();

        if (type is not ArrayTypeSymbol arrayType) {
            diagnostics.Push(Error.CannotApplyIndexing(node.location, boundExpression.type));
            hasErrors = true;
        } else {
            type = arrayType.elementType;
        }

        var constantValue = ConstantFolding.FoldIndex(boundExpression, boundConversion, type);

        return new BoundArrayAccessExpression(node, boundExpression, boundConversion, constantValue, type, hasErrors);
    }

    private BoundUnconvertedInitializerList BindInitializerListExpression(
        InitializerListExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var items = node.items;
        var builder = ArrayBuilder<BoundExpression>.GetInstance(items.Count);

        foreach (var element in items)
            builder.Add(BindElement(element, diagnostics, this));

        return new BoundUnconvertedInitializerList(node, builder.ToImmutableAndFree());

        static BoundExpression BindElement(ExpressionSyntax syntax, BelteDiagnosticQueue diagnostics, Binder @this) {
            return syntax switch {
                InitializerListExpressionSyntax nestedList
                    => @this.BindInitializerListExpression(nestedList, diagnostics),
                ExpressionSyntax expression => @this.BindValue(expression, diagnostics, BindValueKind.RValue),
                _ => throw ExceptionUtilities.UnexpectedValue(syntax.kind)
            };
        }
    }

    private BoundExpression BindCastExpression(CastExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var operand = BindValue(node.expression, diagnostics, BindValueKind.RValue);
        var targetTypeWithAnnotations = BindType(node.type, diagnostics);
        var targetType = targetTypeWithAnnotations.type;

        if (targetType.IsNullableType() &&
            !operand.hasErrors &&
            operand.type is not null &&
            !operand.type.IsNullableType() &&
            !TypeSymbol.Equals(
                targetType.GetNullableUnderlyingType(),
                operand.type,
                TypeCompareKind.ConsiderEverything)) {
            return BindExplicitNullableCastFromNonNullable(node, operand, targetTypeWithAnnotations, diagnostics);
        }

        return BindCastCore(node, operand, targetTypeWithAnnotations, diagnostics);
    }

    private BoundExpression BindExplicitNullableCastFromNonNullable(
        ExpressionSyntax node,
        BoundExpression operand,
        TypeWithAnnotations targetTypeWithAnnotations,
        BelteDiagnosticQueue diagnostics) {
        var underlyingTargetTypeWithAnnotations = targetTypeWithAnnotations.type
            .GetNullableUnderlyingTypeWithAnnotations();

        var underlyingConversion = conversions.ClassifyBuiltInConversion(
            operand.type,
            underlyingTargetTypeWithAnnotations.type
        );

        if (!underlyingConversion.exists)
            return BindCastCore(node, operand, targetTypeWithAnnotations, diagnostics);

        var queue1 = BelteDiagnosticQueue.GetInstance();

        try {
            var underlyingExpression = BindCastCore(node, operand, underlyingTargetTypeWithAnnotations, queue1);

            if (underlyingExpression.constantValue is not null &&
                !underlyingExpression.hasErrors && !queue1.AnyErrors()) {
                diagnostics.PushRange(queue1);
                return BindCastCore(node, underlyingExpression, targetTypeWithAnnotations, diagnostics);
            }

            var queue2 = BelteDiagnosticQueue.GetInstance();

            var result = BindCastCore(node, operand, targetTypeWithAnnotations, queue2);

            if (queue1.AnyErrors() && !queue2.AnyErrors())
                diagnostics.PushRange(queue1);

            diagnostics.PushRangeAndFree(queue2);
            return result;
        } finally {
            queue1.Free();
        }
    }

    private BoundExpression BindCastCore(
        ExpressionSyntax node,
        BoundExpression operand,
        TypeWithAnnotations targetTypeWithAnnotations,
        BelteDiagnosticQueue diagnostics) {
        var targetType = targetTypeWithAnnotations.type;
        var conversion = conversions.ClassifyConversionFromExpression(operand, targetType);
        // var conversionGroup = new ConversionGroup(conversion, targetTypeWithAnnotations);
        var suppressErrors = operand.hasErrors || targetType.IsErrorType();
        var hasErrors = !conversion.exists || targetType.isStatic;

        if (hasErrors && !suppressErrors)
            GenerateExplicitConversionErrors(diagnostics, node, conversion, operand, targetType);

        return CreateConversion(
            node,
            operand,
            conversion,
            isCast: true,
            destination: targetType,
            diagnostics: diagnostics,
            hasErrors: hasErrors | suppressErrors
        );
    }

    private void GenerateExplicitConversionErrors(
        BelteDiagnosticQueue diagnostics,
        SyntaxNode syntax,
        Conversion conversion,
        BoundExpression operand,
        TypeSymbol targetType) {
        if (operand.hasErrors || targetType.IsErrorType())
            return;

        if (targetType.isStatic) {
            diagnostics.Push(Error.CannotConvertToStatic(syntax.location, targetType));
            return;
        }

        if (!targetType.IsNullableType() && operand.IsLiteralNull()) {
            diagnostics.Push(Error.ValueCannotBeNull(syntax.location, targetType));
            return;
        }

        switch (operand.kind) {
            case BoundKind.UnconvertedInitializerList:
                GenerateImplicitConversionErrorForList(
                    (BoundUnconvertedInitializerList)operand,
                    targetType,
                    diagnostics
                );

                break;
        }

        diagnostics.Push(Error.CannotConvert(syntax.location, operand.type, targetType));
    }

    private BoundArrayCreationExpression BindArrayCreationWithInitializer(
        BelteDiagnosticQueue diagnostics,
        ExpressionSyntax creationSyntax,
        InitializerListExpressionSyntax initSyntax,
        ArrayTypeSymbol type,
        ImmutableArray<BoundExpression> sizes,
        ImmutableArray<BoundExpression> boundInitializerExpression = default,
        bool hasErrors = false) {
        // TODO Double check this method thoroughly
        BoundExpression initializerList = BindInitializerListExpression(initSyntax, diagnostics);
        initializerList = GenerateConversionForAssignment(type, initializerList, diagnostics);

        hasErrors = hasErrors || initializerList.hasErrors;
        var nonNullSyntax = (SyntaxNode)creationSyntax ?? initSyntax;

        return new BoundArrayCreationExpression(nonNullSyntax, sizes, initializerList, type, hasErrors);
    }

    private protected BoundExpression BindObjectCreationExpression(
        ObjectCreationExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var typeWithAnnotations = BindType(node.type, diagnostics);
        var type = typeWithAnnotations.type.StrippedType();
        var originalType = type;

        // TODO what is this error
        // if (typeWithAnnotations.NullableAnnotation.IsAnnotated() && !type.IsNullableType()) {
        //     diagnostics.Add(ErrorCode.ERR_AnnotationDisallowedInObjectCreation, node.Location);
        // }

        switch (type.typeKind) {
            case TypeKind.Struct:
            case TypeKind.Class:
            case TypeKind.Error:
                return BindClassCreationExpression(
                    node,
                    (NamedTypeSymbol)type,
                    GetName(node.type),
                    diagnostics,
                    originalType
                );
            case TypeKind.TemplateParameter:
                return BindTemplateParameterCreationExpression(node, (TemplateParameterSymbol)type, diagnostics);
            case TypeKind.Array:
                type = new ExtendedErrorTypeSymbol(
                    type,
                    LookupResultKind.NotCreatable,
                    Error.InvalidObjectCreation(node.type.location)
                );

                goto case TypeKind.Class;
            default:
                throw ExceptionUtilities.UnexpectedValue(type.typeKind);
        }
    }

    private BoundExpression BindTemplateParameterCreationExpression(
        ObjectCreationExpressionSyntax node,
        TemplateParameterSymbol templateParameter,
        BelteDiagnosticQueue diagnostics) {
        var analyzedArguments = AnalyzedArguments.GetInstance();
        BindArgumentsAndNames(node.argumentList, diagnostics, analyzedArguments);
        var result = BindTemplateParameterCreationExpression(
            node,
            templateParameter,
            analyzedArguments,
            node.type,
            false,
            diagnostics
        );

        analyzedArguments.Free();
        return result;
    }

    private BoundExpression BindTemplateParameterCreationExpression(
        SyntaxNode node,
        TemplateParameterSymbol templateParameter,
        AnalyzedArguments analyzedArguments,
        SyntaxNode typeSyntax,
        bool wasTargetTyped,
        BelteDiagnosticQueue diagnostics) {
        if (TemplateParameterHasParameterlessConstructor(node, templateParameter, diagnostics)) {
            if (analyzedArguments.arguments.Count > 0) {
                // TODO error
                // diagnostics.Add(ErrorCode.ERR_NewTyvarWithArgs, node.Location, templateParameter);
            } else {
                return new BoundNewT(node, wasTargetTyped, templateParameter);
            }
        }

        return MakeErrorExpressionForObjectCreation(
            node,
            templateParameter,
            analyzedArguments,
            typeSyntax,
            diagnostics
        );
    }

    private static bool TemplateParameterHasParameterlessConstructor(
        SyntaxNode node,
        TemplateParameterSymbol templateParameter,
        BelteDiagnosticQueue diagnostics) {
        if (/*!templateParameter.hasConstructorConstraint &&*/ !templateParameter.isPrimitiveType) {
            // TODO error and first condition
            // diagnostics.Add(ErrorCode.ERR_NoNewTyvar, node.Location, templateParameter);
            return false;
        }

        return true;
    }

    private BoundExpression BindClassCreationExpression(
        ObjectCreationExpressionSyntax node,
        NamedTypeSymbol type,
        string typeName,
        BelteDiagnosticQueue diagnostics,
        TypeSymbol initializerType = null) {
        var analyzedArguments = AnalyzedArguments.GetInstance();

        try {
            BindArgumentsAndNames(node.argumentList, diagnostics, analyzedArguments);

            if (type.isStatic) {
                diagnostics.Push(Error.CannotCreateStatic(node.location, type));
                return MakeErrorExpressionForObjectCreation(node, type, analyzedArguments, node.type, diagnostics);
            }

            return BindClassCreationExpression(node, typeName, node.type, type, analyzedArguments, diagnostics);
        } finally {
            analyzedArguments.Free();
        }
    }

    private BoundExpression MakeErrorExpressionForObjectCreation(
        SyntaxNode node,
        TypeSymbol type,
        AnalyzedArguments analyzedArguments,
        SyntaxNode typeSyntax,
        BelteDiagnosticQueue diagnostics) {
        return new BoundErrorExpression(
            node,
            LookupResultKind.NotCreatable,
            [type],
            BuildArgumentsForErrorRecovery(analyzedArguments),
            type
        );
    }

    private protected BoundExpression BindClassCreationExpression(
        SyntaxNode node,
        string typeName,
        SyntaxNode typeNode,
        NamedTypeSymbol type,
        AnalyzedArguments analyzedArguments,
        BelteDiagnosticQueue diagnostics) {
        var hasErrors = type.IsErrorType();

        if (type.isAbstract) {
            diagnostics.Push(Error.CannotCreateAbstract(node.location, type));
            hasErrors = true;
        }

        if (TryPerformConstructorOverloadResolution(
                type,
                analyzedArguments,
                typeName,
                typeNode.location,
                hasErrors,
                diagnostics,
                out var memberResolutionResult,
                out var candidateConstructors,
                allowProtectedConstructorsOfBaseType: false) &&
            !type.isAbstract) {
            return BindClassCreationExpressionContinued(
                node,
                typeNode,
                type,
                analyzedArguments,
                memberResolutionResult,
                candidateConstructors,
                diagnostics
            );
        }

        return CreateErrorClassCreationExpression(
            node,
            typeNode,
            type,
            analyzedArguments,
            memberResolutionResult,
            candidateConstructors,
            diagnostics
        );
    }

    internal bool TryPerformConstructorOverloadResolution(
        NamedTypeSymbol typeContainingConstructors,
        AnalyzedArguments analyzedArguments,
        string errorName,
        TextLocation errorLocation,
        bool suppressResultDiagnostics,
        BelteDiagnosticQueue diagnostics,
        out MemberResolutionResult<MethodSymbol> memberResolutionResult,
        out ImmutableArray<MethodSymbol> candidateConstructors,
        bool allowProtectedConstructorsOfBaseType,
        bool isParamsModifierValidation = false) {
        candidateConstructors = GetAccessibleConstructorsForOverloadResolution(
            typeContainingConstructors,
            allowProtectedConstructorsOfBaseType,
            out var allInstanceConstructors
        );

        var result = OverloadResolutionResult<MethodSymbol>.GetInstance();
        var succeededConsideringAccessibility = false;

        if (candidateConstructors.Any()) {
            overloadResolution.ObjectCreationOverloadResolution(
                candidateConstructors,
                analyzedArguments,
                result
            );

            if (result.succeeded)
                succeededConsideringAccessibility = true;
        }

        if (!succeededConsideringAccessibility && allInstanceConstructors.Length > candidateConstructors.Length) {
            var inaccessibleResult = OverloadResolutionResult<MethodSymbol>.GetInstance();
            overloadResolution.ObjectCreationOverloadResolution(
                allInstanceConstructors,
                analyzedArguments,
                inaccessibleResult
            );

            if (inaccessibleResult.succeeded) {
                candidateConstructors = allInstanceConstructors;
                result.Free();
                result = inaccessibleResult;
            } else {
                inaccessibleResult.Free();
            }
        }

        memberResolutionResult = result.succeeded ? result.bestResult : default;

        if (!succeededConsideringAccessibility && !suppressResultDiagnostics) {
            if (result.succeeded) {
                diagnostics.Push(Error.MemberIsInaccessible(errorLocation, result.bestResult.member));
            } else {
                result.ReportDiagnostics(
                    binder: this,
                    location: errorLocation,
                    node: null,
                    diagnostics,
                    name: errorName,
                    receiver: null,
                    invokedExpression: null,
                    analyzedArguments,
                    memberGroup: candidateConstructors,
                    typeContainingConstructors
                );
            }
        }

        result.Free();
        return succeededConsideringAccessibility;
    }

    private ImmutableArray<MethodSymbol> GetAccessibleConstructorsForOverloadResolution(NamedTypeSymbol type) {
        return GetAccessibleConstructorsForOverloadResolution(type, false, out _);
    }

    private ImmutableArray<MethodSymbol> GetAccessibleConstructorsForOverloadResolution(
        NamedTypeSymbol type,
        bool allowProtectedConstructorsOfBaseType,
        out ImmutableArray<MethodSymbol> allInstanceConstructors) {
        if (type.IsErrorType())
            type = type.GetNonErrorGuess() as NamedTypeSymbol ?? type;

        allInstanceConstructors = type.constructors;
        return FilterInaccessibleConstructors(allInstanceConstructors, allowProtectedConstructorsOfBaseType);
    }

    internal ImmutableArray<MethodSymbol> FilterInaccessibleConstructors(
        ImmutableArray<MethodSymbol> constructors,
        bool allowProtectedConstructorsOfBaseType) {
        ArrayBuilder<MethodSymbol> builder = null;

        for (var i = 0; i < constructors.Length; i++) {
            var constructor = constructors[i];

            if (!IsConstructorAccessible(constructor, allowProtectedConstructorsOfBaseType)) {
                if (builder is null) {
                    builder = ArrayBuilder<MethodSymbol>.GetInstance();
                    builder.AddRange(constructors, i);
                }
            } else {
                builder?.Add(constructor);
            }
        }

        return builder is null ? constructors : builder.ToImmutableAndFree();
    }

    private BoundObjectCreationExpression BindClassCreationExpressionContinued(
        SyntaxNode node,
        SyntaxNode typeNode,
        NamedTypeSymbol type,
        AnalyzedArguments analyzedArguments,
        MemberResolutionResult<MethodSymbol> memberResolutionResult,
        ImmutableArray<MethodSymbol> candidateConstructors,
        BelteDiagnosticQueue diagnostics,
        bool wasTargetTyped = false) {
        ImmutableArray<int> argToParams;

        if (memberResolutionResult.isNotNull) {
            CheckAndCoerceArguments(
                node,
                memberResolutionResult,
                analyzedArguments,
                diagnostics,
                receiver: null,
                out argToParams
            );
        } else {
            argToParams = memberResolutionResult.result.argsToParams;
        }

        var method = memberResolutionResult.member;
        var hasError = false;

        BindDefaultArguments(
            node,
            method.parameters,
            analyzedArguments.arguments,
            analyzedArguments.refKinds,
            analyzedArguments.names,
            ref argToParams,
            out var defaultArguments,
            enableCallerInfo: true,
            diagnostics
        );

        var arguments = analyzedArguments.arguments.ToImmutable();
        var refKinds = analyzedArguments.refKinds.ToImmutableOrNull();
        var creation = new BoundObjectCreationExpression(
            node,
            method,
            // candidateConstructors,
            arguments,
            // analyzedArguments.GetNames(),
            refKinds,
            argToParams,
            defaultArguments,
            // constantValueOpt,
            // boundInitializerOpt,
            wasTargetTyped,
            type,
            hasError
        );

        return creation;
    }

    private BoundExpression CreateErrorClassCreationExpression(
        SyntaxNode node,
        SyntaxNode typeNode,
        NamedTypeSymbol type,
        AnalyzedArguments analyzedArguments,
        MemberResolutionResult<MethodSymbol> memberResolutionResult,
        ImmutableArray<MethodSymbol> candidateConstructors,
        BelteDiagnosticQueue diagnostics) {
        if (memberResolutionResult.isNotNull) {
            CheckAndCoerceArguments(
                node,
                memberResolutionResult,
                analyzedArguments,
                diagnostics,
                receiver: null,
                argsToParams: out _
            );
        }

        LookupResultKind resultKind;

        if (type.isAbstract)
            resultKind = LookupResultKind.NotCreatable;
        else if (memberResolutionResult.isValid && !IsConstructorAccessible(memberResolutionResult.member))
            resultKind = LookupResultKind.Inaccessible;
        else
            resultKind = LookupResultKind.OverloadResolutionFailure;

        return new BoundErrorExpression(
            node,
            resultKind,
            [.. candidateConstructors],
            BuildArgumentsForErrorRecovery(analyzedArguments),
            type
        );
    }

    private bool IsConstructorAccessible(MethodSymbol constructor, bool allowProtectedConstructorsOfBaseType = false) {
        var containingType = this.containingType;

        if (containingType is not null) {
            return allowProtectedConstructorsOfBaseType
                ? IsAccessible(constructor, null)
                : IsSymbolAccessibleConditional(constructor, containingType, constructor.containingType);
        } else {
            return IsSymbolAccessibleConditional(constructor, compilation.globalNamespaceInternal);
        }
    }

    internal BoundExpression BindBooleanExpression(ExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var expression = BindValue(node, diagnostics, BindValueKind.RValue);
        var boolean = CorLibrary.GetSpecialType(SpecialType.Bool);

        if (expression.hasErrors) {
            return new BoundCastExpression(
                node,
                BindToTypeForErrorRecovery(expression),
                Conversion.None,
                null,
                boolean,
                true
            );
        }

        var conversion = conversions.ClassifyConversionFromExpression(expression, boolean);

        if (conversion.isImplicit) {
            if (conversion.kind == ConversionKind.Identity) {
                if (expression.kind == BoundKind.AssignmentOperator) {
                    var assignment = (BoundAssignmentOperator)expression;

                    if (assignment.right.kind == BoundKind.LiteralExpression &&
                        assignment.right.constantValue.specialType == SpecialType.Bool) {
                        // TODO error
                        // Error(diagnostics, ErrorCode.WRN_IncorrectBooleanAssg, assignment.Syntax);
                    }
                }
            }

            return CreateConversion(
                node: expression.syntax,
                source: expression,
                conversion: conversion,
                isCast: false,
                destination: boolean,
                diagnostics: diagnostics
            );
        }

        expression = BindToNaturalType(expression, diagnostics);
        var best = UnaryOperatorOverloadResolution(
            UnaryOperatorKind.True,
            expression,
            node,
            diagnostics,
            out var resultKind,
            out var originalUserDefinedOperators
        );

        if (!best.hasValue) {
            GenerateImplicitConversionError(diagnostics, node, conversion, expression, boolean);
            return new BoundCastExpression(node, expression, Conversion.None, null, boolean, true);
        }

        var signature = best.signature;
        var resultOperand = CreateConversion(
            node,
            expression,
            best.conversion,
            isCast: false,
            destination: best.signature.operandType,
            diagnostics: diagnostics
        );

        // TODO this is necessarily an operator overload, so this isn't correct because we just drop the method
        // TODO probably what we should do is store the method information in the Operator nodes (Binary, Unary, Increment, etc.)
        // TODO and then convert them to calls in the lowerer
        // return new BoundUnaryOperator(node, signature.kind, resultOperand, null, signature.Method, signature.ConstrainedToTypeOpt, resultKind, originalUserDefinedOperators, signature.ReturnType) {
        //     WasCompilerGenerated = true
        // };
        return new BoundUnaryOperator(node, resultOperand, signature.kind, null, signature.returnType, false);
    }

    private BoundExpression BindIdentifier(
        SimpleNameSyntax node,
        bool called,
        bool indexed,
        BelteDiagnosticQueue diagnostics) {
        BoundExpression expression;
        var hasTemplateArguments = node.arity > 0;
        var templateArgumentList = node is TemplateNameSyntax t ? t.templateArgumentList.arguments : default;
        // TODO templates
        var templateArguments = hasTemplateArguments ? /*BindTemplateArguments(templateArgumentList, diagnostics)*/ ImmutableArray<TypeOrConstant>.Empty : [];

        var lookupResult = LookupResult.GetInstance();
        var name = node.identifier.text;
        LookupIdentifier(lookupResult, node, called);

        if (lookupResult.kind != LookupResultKind.Empty) {
            var members = ArrayBuilder<Symbol>.GetInstance();
            var symbol = GetSymbolOrMethodGroup(
                lookupResult,
                node,
                name,
                node.arity,
                members,
                diagnostics,
                out var isError,
                null
            );

            if (symbol is null) {
                var receiver = SynthesizeMethodGroupReceiver(node, members);
                expression = ConstructBoundMemberGroupAndReportOmittedTypeArguments(
                    node,
                    templateArgumentList,
                    templateArguments,
                    receiver,
                    name,
                    members,
                    lookupResult,
                    receiver is not null ? BoundMethodGroupFlags.HasImplicitReceiver : BoundMethodGroupFlags.None,
                    isError,
                    diagnostics
                );

                ReportSimpleProgramLocalReferencedOutsideOfTopLevelStatement(node, members[0], diagnostics);
            } else {
                var isNamedType = symbol.kind is SymbolKind.NamedType or SymbolKind.ErrorType;

                if (hasTemplateArguments && isNamedType) {
                    // symbol = ConstructNamedTypeUnlessTemplateArgumentOmitted(node, (NamedTypeSymbol)symbol, typeArgumentList, typeArgumentsWithAnnotations, diagnostics);
                    // TODO template
                }

                expression = BindNonMethod(node, symbol, diagnostics, lookupResult.kind, indexed, isError);

                if (!isNamedType && (hasTemplateArguments || node.kind == SyntaxKind.TemplateName)) {
                    expression = new BoundErrorExpression(
                        node,
                        LookupResultKind.WrongTemplate,
                        [symbol],
                        [BindToTypeForErrorRecovery(expression)],
                        expression.type,
                        isError
                    );
                }
            }

            members.Free();
        } else {
            expression = ErrorExpression(node);

            if (lookupResult.error is not null)
                diagnostics.Push(lookupResult.error);
            else
                diagnostics.Push(Error.UndefinedSymbol(node.location, name));
        }

        lookupResult.Free();
        return expression;
    }

    private bool ReportSimpleProgramLocalReferencedOutsideOfTopLevelStatement(
        SimpleNameSyntax node,
        Symbol symbol,
        BelteDiagnosticQueue diagnostics) {
        if (symbol.containingSymbol is SynthesizedProgram && containingMember is not SynthesizedEntryPoint) {
            diagnostics.Push(Error.ProgramLocalReferencedOutsideOfTopLevelStatement(node.location, node));
            return true;
        }

        return false;
    }

    private BoundMethodGroup ConstructBoundMemberGroupAndReportOmittedTypeArguments(
        SyntaxNode syntax,
        SeparatedSyntaxList<BaseArgumentSyntax> templateArgumentsSyntax,
        ImmutableArray<TypeOrConstant> templateArguments,
        BoundExpression receiver,
        string plainName,
        ArrayBuilder<Symbol> members,
        LookupResult lookupResult,
        BoundMethodGroupFlags methodGroupFlags,
        bool hasErrors,
        BelteDiagnosticQueue diagnostics) {
        // TODO templates
        // if (!hasErrors && lookupResult.isMultiViable && typeArgumentsSyntax.Any(SyntaxKind.OmittedTypeArgument)) {
        //     // Note: lookup won't have reported this, since the arity was correct.
        //     // CONSIDER: the text of this error message makes sense, but we might want to add a separate code.
        //     Error(diagnostics, ErrorCode.ERR_BadArity, syntax, plainName, MessageID.IDS_MethodGroup.Localize(), typeArgumentsSyntax.Count);
        //     hasErrors = true;
        // }

        switch (members[0].kind) {
            case SymbolKind.Method:
                return new BoundMethodGroup(
                    syntax,
                    plainName,
                    members.SelectAsArray(s => (MethodSymbol)s),
                    templateArguments,
                    lookupResult.singleSymbolOrDefault,
                    lookupResult.error,
                    methodGroupFlags,
                    receiver,
                    lookupResult.kind,
                    null,
                    hasErrors
                );
            default:
                throw ExceptionUtilities.UnexpectedValue(members[0].kind);
        }
    }

    private BoundExpression SynthesizeMethodGroupReceiver(BelteSyntaxNode syntax, ArrayBuilder<Symbol> members) {
        var currentType = containingType;

        if (currentType is null)
            return null;

        var declaringType = members[0].containingType;

        if (currentType.IsEqualToOrDerivedFrom(declaringType, TypeCompareKind.ConsiderEverything))
            return new BoundThisExpression(syntax, currentType);

        return null;
    }

    private BoundExpression BindNonMethod(
        SimpleNameSyntax node,
        Symbol symbol,
        BelteDiagnosticQueue diagnostics,
        LookupResultKind resultKind,
        bool indexed,
        bool isError) {

        switch (symbol.kind) {
            case SymbolKind.Local: {
                    var localSymbol = (DataContainerSymbol)symbol;
                    TypeSymbol type;
                    bool isNullableUnknown;

                    if (IsUsedBeforeDeclaration(node, localSymbol)) {
                        FieldSymbol possibleField;
                        var lookupResult = LookupResult.GetInstance();

                        LookupMembersInType(
                            lookupResult,
                            containingType,
                            localSymbol.name,
                            arity: 0,
                            basesBeingResolved: null,
                            options: LookupOptions.Default,
                            originalBinder: this,
                            diagnose: false
                        );

                        possibleField = lookupResult.singleSymbolOrDefault as FieldSymbol;
                        lookupResult.Free();

                        if (possibleField is not null) {
                            diagnostics.Push(Error.LocalUsedBeforeDeclarationAndHidesField(
                                node.location,
                                localSymbol,
                                possibleField
                            ));
                        } else {
                            diagnostics.Push(Error.LocalUsedBeforeDeclaration(node.location, localSymbol));
                        }

                        type = new ExtendedErrorTypeSymbol(
                            compilation,
                            "var",
                            0,
                            error: null,
                            variableUsedBeforeDeclaration: true
                        );

                        isNullableUnknown = true;
                    } else if (localSymbol is SourceDataContainerSymbol { isImplicitlyTyped: true } &&
                        localSymbol.forbiddenZone?.Contains(node) == true) {
                        diagnostics.Push(localSymbol.forbiddenDiagnostic);

                        type = new ExtendedErrorTypeSymbol(
                            compilation,
                            "var",
                            0,
                            error: null,
                            variableUsedBeforeDeclaration: true
                        );

                        isNullableUnknown = true;
                    } else {
                        type = localSymbol.type;
                        isNullableUnknown = false;

                        if (IsBadLocalOrParameterCapture(localSymbol, type, localSymbol.refKind)) {
                            isError = true;
                            // TODO is this a reachable error?
                        }
                    }

                    var constantValue = localSymbol.isConst && !isInsideNameof && !type.IsErrorType()
                        ? localSymbol.GetConstantValue(node, localInProgress, diagnostics)
                        : null;

                    return new BoundDataContainerExpression(
                        node,
                        localSymbol,
                        constantValue,
                        localSymbol.type,
                        isError
                    );
                }
            case SymbolKind.Parameter: {
                    var parameter = (ParameterSymbol)symbol;

                    if (IsBadLocalOrParameterCapture(parameter, parameter.type, parameter.refKind)) {
                        isError = true;
                        // TODO is this a reachable error?
                    }

                    return new BoundParameterExpression(
                        node,
                        parameter,
                        null,
                        parameter.type,
                        isError
                    );
                }
            case SymbolKind.NamedType:
            case SymbolKind.ErrorType:
            case SymbolKind.TemplateParameter:
                return new BoundTypeExpression(node, null, (TypeSymbol)symbol, isError);
            case SymbolKind.Field: {
                    var receiver = SynthesizeReceiver(node, symbol, diagnostics);
                    return BindFieldAccess(
                        node,
                        receiver,
                        (FieldSymbol)symbol,
                        diagnostics,
                        resultKind,
                        indexed,
                        isError
                    );
                }
            default:
                throw ExceptionUtilities.UnexpectedValue(symbol.kind);
        }

        static bool IsUsedBeforeDeclaration(SimpleNameSyntax node, DataContainerSymbol localSymbol) {
            if (!localSymbol.hasSourceLocation)
                return false;

            var declaration = localSymbol.syntaxReference.node;

            if (node.span.start >= declaration.span.start)
                return false;

            return node.syntaxTree == declaration.syntaxTree;
        }
    }

    private bool IsBadLocalOrParameterCapture(Symbol symbol, TypeSymbol type, RefKind refKind) {
        if (refKind != RefKind.None) {
            if (containingMember is MethodSymbol containingMethod &&
                (object)symbol.containingSymbol != containingMethod) {
                return (containingMethod.methodKind == MethodKind.LocalFunction) && !isInsideNameof;
            }
        }

        return false;
    }

    private BoundExpression SynthesizeReceiver(SyntaxNode node, Symbol member, BelteDiagnosticQueue diagnostics) {
        if (!member.RequiresInstanceReceiver())
            return null;

        var currentType = containingType;
        var declaringType = member.containingType;

        if (currentType.IsEqualToOrDerivedFrom(declaringType, TypeCompareKind.ConsiderEverything)) {
            var hasErrors = false;

            if (!isInsideNameof) {
                BelteDiagnostic diagnosticInfoOpt = null;

                if (inFieldInitializer) {
                    diagnostics.Push(Error.CannotUseThis(node.location));
                } else if (_inConstructorInitializer) {
                    diagnostics.Push(Error.InstanceRequired(node.location, member));
                } else {
                    var containingMember = this.containingMember;

                    var locationIsInstanceMember = !containingMember.isStatic &&
                        (containingMember.kind != SymbolKind.NamedType);

                    if (!locationIsInstanceMember)
                        diagnostics.Push(Error.InstanceRequired(node.location, member));
                }

                diagnosticInfoOpt ??= GetDiagnosticIfRefOrOutThisParameterCaptured(node.location);
                hasErrors = diagnosticInfoOpt is not null;

                if (hasErrors && !isInsideNameof)
                    diagnostics.Push(diagnosticInfoOpt);
            }

            return new BoundThisExpression(node, currentType ?? CreateErrorType(), hasErrors);
        } else {
            return null;
        }
    }

    private void LookupIdentifier(LookupResult lookupResult, SimpleNameSyntax node, bool called) {
        LookupIdentifier(lookupResult, node.identifier.text, node.arity, called);
    }

    private void LookupIdentifier(LookupResult lookupResult, string name, int arity, bool called) {
        var options = LookupOptions.AllMethodsOnArityZero;

        if (called)
            options |= LookupOptions.MustBeInvocableIfMember;

        if (!isInMethodBody && !isInsideNameof)
            options |= LookupOptions.MustNotBeMethodTemplateParameter;

        LookupSymbolsWithFallback(lookupResult, name, arity, options: options);
    }

    private BoundExpression BindMemberAccess(
        MemberAccessExpressionSyntax node,
        bool called,
        bool indexed,
        BelteDiagnosticQueue diagnostics) {
        var boundLeft = BindExpression(node.expression, diagnostics);
        return BindMemberAccessWithBoundLeft(
            node,
            boundLeft,
            node.name,
            node.operatorToken,
            called,
            indexed,
            diagnostics
        );
    }

    private BoundExpression BindReferenceType(ReferenceTypeSyntax node, BelteDiagnosticQueue diagnostics) {
        diagnostics.Push(Error.UnexpectedToken(node.refKeyword.location, node.refKeyword.kind));
        return new BoundTypeExpression(node, null, CreateErrorType("ref"));
    }

    private BoundExpression BindReferenceExpression(ReferenceExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var firstToken = node.GetFirstToken();
        diagnostics.Push(Error.UnexpectedToken(firstToken.location, firstToken.kind));
        return new BoundErrorExpression(
            node,
            LookupResultKind.Empty,
            [],
            [BindToTypeForErrorRecovery(
                BindValue(node.expression, BelteDiagnosticQueue.Discarded, BindValueKind.RefersToLocation)
            )],
            CreateErrorType("ref")
        );
    }

    private BoundExpression BindQualifiedName(QualifiedNameSyntax node, BelteDiagnosticQueue diagnostics) {
        // TODO Some languages allow "Color Color" member access where the instance name is the same as the type name
        // In which case we would need a special handler for this "BindLeftOfPotentialColorColorMemberAccess"
        // however, currently we disallow that naming convention
        var left = BindExpression(node.left, diagnostics);
        return BindMemberAccessWithBoundLeft(node, left, node.right, node.period, false, false, diagnostics);
    }

    private BoundExpression BindMemberAccessWithBoundLeft(
        ExpressionSyntax node,
        BoundExpression boundLeft,
        SimpleNameSyntax right,
        SyntaxToken operatorToken,
        bool called,
        bool indexed,
        BelteDiagnosticQueue diagnostics) {
        boundLeft = MakeMemberAccessValue(boundLeft, diagnostics);
        var leftType = boundLeft.type;

        if (leftType is not null && leftType.IsVoidType()) {
            // TODO error cannot access void, is this a reachable error?
            return ErrorExpression(node, boundLeft);
        }

        boundLeft = BindToNaturalType(boundLeft, diagnostics);
        leftType = boundLeft.type;
        var lookupResult = LookupResult.GetInstance();

        try {
            var options = LookupOptions.AllMethodsOnArityZero;

            if (called)
                options |= LookupOptions.MustBeInvocableIfMember;

            var templateArgumentsSyntax = right.kind == SyntaxKind.TemplateName
                ? ((TemplateNameSyntax)right).templateArgumentList.arguments
                : default;

            var templateArguments = templateArgumentsSyntax?.Count > 0
                ? BindTemplateArguments(templateArgumentsSyntax, diagnostics)
                : default;

            var rightName = right.identifier.text;
            var rightArity = right.arity;
            BoundExpression result;

            switch (boundLeft.kind) {
                case BoundKind.TypeExpression:
                    if (leftType.typeKind == TypeKind.TemplateParameter) {
                        LookupMembersWithFallback(
                            lookupResult,
                            leftType,
                            rightName,
                            rightArity,
                            null,
                            options | LookupOptions.MustNotBeInstance | LookupOptions.MustBeAbstractOrVirtual
                        );

                        if (lookupResult.isMultiViable) {
                            return BindMemberOfType(
                                node,
                                right,
                                rightName,
                                rightArity,
                                indexed,
                                boundLeft,
                                templateArgumentsSyntax,
                                templateArguments,
                                lookupResult,
                                BoundMethodGroupFlags.None,
                                diagnostics
                            );
                        } else if (lookupResult.isClear) {
                            // Error(diagnostics, ErrorCode.ERR_LookupInTypeVariable, boundLeft.Syntax, leftType);
                            // return ErrorExpression(node, LookupResultKind.NotAValue, boundLeft);
                            // TODO error, is this a reachable error?
                            return ErrorExpression(node, LookupResultKind.NotAValue, boundLeft);
                        }
                    } else if (_enclosingNameofArgument == node) {
                        return BindInstanceMemberAccess(
                            node,
                            right,
                            boundLeft,
                            rightName,
                            rightArity,
                            templateArgumentsSyntax,
                            templateArguments,
                            called,
                            indexed,
                            diagnostics
                        );
                    } else {
                        LookupMembersWithFallback(lookupResult, leftType, rightName, rightArity, null, options);

                        if (lookupResult.isMultiViable) {
                            return BindMemberOfType(
                                node,
                                right,
                                rightName,
                                rightArity,
                                indexed,
                                boundLeft,
                                templateArgumentsSyntax,
                                templateArguments,
                                lookupResult,
                                BoundMethodGroupFlags.None,
                                diagnostics
                            );
                        }
                    }

                    break;
                default:
                    if (boundLeft.IsLiteralNull()) {
                        diagnostics.Push(Error.InvalidUnaryOperatorUse(
                            node.location,
                            operatorToken.text,
                            CreateErrorType("<null>")
                        ));

                        return ErrorExpression(node, boundLeft);
                    } else if (leftType is not null) {
                        boundLeft = CheckValue(boundLeft, BindValueKind.RValue, diagnostics);
                        boundLeft = BindToNaturalType(boundLeft, diagnostics);

                        return BindInstanceMemberAccess(
                            node,
                            right,
                            boundLeft,
                            rightName,
                            rightArity,
                            templateArgumentsSyntax,
                            templateArguments,
                            called,
                            indexed,
                            diagnostics
                        );
                    }

                    break;
            }

            BindMemberAccessReportError(node, right, rightName, boundLeft, lookupResult.error, diagnostics);

            return BindMemberAccessBadResult(
                node,
                rightName,
                boundLeft,
                lookupResult.error,
                lookupResult.symbols.ToImmutable(),
                lookupResult.kind
            );
        } finally {
            lookupResult.Free();
        }
    }

    private BoundExpression BindMemberAccessBadResult(
        SyntaxNode node,
        string nameString,
        BoundExpression boundLeft,
        BelteDiagnostic lookupError,
        ImmutableArray<Symbol> symbols,
        LookupResultKind lookupKind) {
        if (symbols.Length > 0 && symbols[0].kind == SymbolKind.Method) {
            var builder = ArrayBuilder<MethodSymbol>.GetInstance();

            foreach (var s in symbols)
                if (s is MethodSymbol m) builder.Add(m);

            var methods = builder.ToImmutableAndFree();

            return new BoundMethodGroup(
                node,
                nameString,
                methods,
                [],
                methods.Length == 1 ? methods[0] : null,
                lookupError,
                BoundMethodGroupFlags.None,
                boundLeft,
                lookupKind,
                null,
                true
            );
        }

        var symbol = symbols.Length == 1 ? symbols[0] : null;
        return new BoundErrorExpression(
            node,
            lookupKind,
            symbol is null ? [] : [symbol],
            boundLeft is null ? [] : [BindToTypeForErrorRecovery(boundLeft)],
            GetNonMethodMemberType(symbol)
        );
    }

    private TypeSymbol GetNonMethodMemberType(Symbol symbol) {
        TypeSymbol resultType = null;

        if (symbol is not null) {
            switch (symbol.kind) {
                case SymbolKind.Field:
                    resultType = ((FieldSymbol)symbol).GetFieldType(fieldsBeingBound).type;
                    break;
            }
        }

        return resultType ?? CreateErrorType();
    }

    private void BindMemberAccessReportError(
        SyntaxNode node,
        SyntaxNode name,
        string plainName,
        BoundExpression boundLeft,
        BelteDiagnostic lookupError,
        BelteDiagnosticQueue diagnostics) {
        if (boundLeft.hasErrors)
            return;

        if (lookupError is not null) {
            diagnostics.Push(lookupError);
        } else {
            if (boundLeft.type is null)
                diagnostics.Push(Error.NoSuchMember(name.location, boundLeft, plainName));
            else
                diagnostics.Push(Error.NoSuchMember(name.location, boundLeft.type, plainName));
        }
    }

    private BoundExpression BindInstanceMemberAccess(
        SyntaxNode node,
        SyntaxNode right,
        BoundExpression boundLeft,
        string rightName,
        int rightArity,
        SeparatedSyntaxList<BaseArgumentSyntax> templateArgumentsSyntax,
        ImmutableArray<(string, TypeOrConstant)> templateArguments,
        bool called,
        bool indexed,
        BelteDiagnosticQueue diagnostics) {
        var leftType = boundLeft.type;
        var lookupResult = LookupResult.GetInstance();

        try {
            var leftIsBaseReference = boundLeft.kind == BoundKind.BaseExpression;
            LookupInstanceMember(lookupResult, leftType, leftIsBaseReference, rightName, rightArity, called);

            BoundMethodGroupFlags flags = 0;

            if (lookupResult.isMultiViable) {
                return BindMemberOfType(
                    node,
                    right,
                    rightName,
                    rightArity,
                    indexed,
                    boundLeft,
                    templateArgumentsSyntax,
                    templateArguments,
                    lookupResult,
                    flags,
                    diagnostics
                );
            }

            BindMemberAccessReportError(node, right, rightName, boundLeft, lookupResult.error, diagnostics);
            return BindMemberAccessBadResult(
                node,
                rightName,
                boundLeft,
                lookupResult.error,
                lookupResult.symbols.ToImmutable(),
                lookupResult.kind
            );
        } finally {
            lookupResult.Free();
        }
    }

    private void LookupInstanceMember(
        LookupResult lookupResult,
        TypeSymbol leftType,
        bool leftIsBaseReference,
        string rightName,
        int rightArity,
        bool called) {
        var options = LookupOptions.AllMethodsOnArityZero;

        if (called)
            options |= LookupOptions.MustBeInvocableIfMember;

        if (leftIsBaseReference)
            options |= LookupOptions.UseBaseReferenceAccessibility;

        LookupMembersWithFallback(
            lookupResult,
            leftType,
            rightName,
            rightArity,
            basesBeingResolved: null,
            options: options
        );
    }

    private BoundExpression BindMemberOfType(
        SyntaxNode node,
        SyntaxNode right,
        string plainName,
        int arity,
        bool indexed,
        BoundExpression left,
        SeparatedSyntaxList<BaseArgumentSyntax> templateArgumentsSyntax,
        ImmutableArray<(string, TypeOrConstant)> templateArguments,
        LookupResult lookupResult,
        BoundMethodGroupFlags methodGroupFlags,
        BelteDiagnosticQueue diagnostics) {
        var members = ArrayBuilder<Symbol>.GetInstance();
        BoundExpression result;
        var symbol = GetSymbolOrMethodGroup(
            lookupResult,
            right,
            plainName,
            arity,
            members,
            diagnostics,
            out var wasError,
            qualifier: left is BoundTypeExpression typeExpr ? typeExpr.type : null
        );

        if (symbol is null) {
            result = ConstructBoundMemberGroupAndReportOmittedTypeArguments(
                node,
                templateArgumentsSyntax,
                templateArguments.IsDefaultOrEmpty ? [] : templateArguments.Select(p => p.Item2).ToImmutableArray(),
                left,
                plainName,
                members,
                lookupResult,
                methodGroupFlags,
                wasError,
                diagnostics
            );
        } else {
            if (left is not null)
                left = BindToNaturalType(left, diagnostics);

            switch (symbol.kind) {
                case SymbolKind.NamedType:
                case SymbolKind.ErrorType:
                    if (IsInstanceReceiver(left) && !wasError) {
                        diagnostics.Push(Error.NoInstanceRequired(right.location, plainName, symbol));
                        wasError = true;
                    }

                    var type = (NamedTypeSymbol)symbol;
                    if (!templateArguments.IsDefault) {
                        // type = ConstructNamedTypeUnlessTypeArgumentOmitted(right, type, typeArgumentsSyntax, typeArgumentsWithAnnotations, diagnostics);
                        // TODO templates
                    }

                    result = new BoundTypeExpression(node, new TypeWithAnnotations(type), type);
                    break;
                case SymbolKind.Field:
                    result = BindFieldAccess(
                        node,
                        left,
                        (FieldSymbol)symbol,
                        diagnostics,
                        lookupResult.kind,
                        indexed,
                        wasError
                    );

                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(symbol.kind);
            }
        }

        members.Free();
        return result;
    }

    private Symbol GetSymbolOrMethodGroup(
        LookupResult result,
        SyntaxNode node,
        string plainName,
        int arity,
        ArrayBuilder<Symbol> methodGroup,
        BelteDiagnosticQueue diagnostics,
        out bool wasError,
        NamespaceOrTypeSymbol qualifier) {
        node = GetNameSyntax(node) ?? node;
        wasError = false;
        Symbol other = null;

        foreach (var symbol in result.symbols) {
            var kind = symbol.kind;

            if (methodGroup.Count > 0) {
                var existingKind = methodGroup[0].kind;

                if (existingKind != kind) {
                    if (existingKind == SymbolKind.Method) {
                        other = symbol;
                        continue;
                    }

                    other = methodGroup[0];
                    methodGroup.Clear();
                }
            }

            if (kind == SymbolKind.Method)
                methodGroup.Add(symbol);
            else
                other = symbol;
        }

        if ((methodGroup.Count > 0) && methodGroup[0].kind == SymbolKind.Method) {
            if ((methodGroup[0].kind == SymbolKind.Method) || (other is null)) {
                if (result.error is not null) {
                    diagnostics.Push(result.error);
                    wasError = result.error.info.severity == DiagnosticSeverity.Error;
                }

                return null;
            }
        }

        methodGroup.Clear();
        return ResultSymbol(result, plainName, arity, node, diagnostics, out wasError, qualifier);
    }

    private static NameSyntax GetNameSyntax(SyntaxNode syntax) {
        return GetNameSyntax(syntax, out _);
    }

    internal static NameSyntax GetNameSyntax(SyntaxNode syntax, out string nameString) {
        nameString = "";

        while (true) {
            switch (syntax.kind) {
                case SyntaxKind.ParenthesizedExpression:
                    syntax = ((ParenthesisExpressionSyntax)syntax).expression;
                    continue;
                case SyntaxKind.CastExpression:
                    syntax = ((CastExpressionSyntax)syntax).expression;
                    continue;
                case SyntaxKind.MemberAccessExpression:
                    return ((MemberAccessExpressionSyntax)syntax).name;
                default:
                    return syntax as NameSyntax;
            }
        }
    }

    private protected BoundExpression BindFieldAccess(
        SyntaxNode node,
        BoundExpression receiver,
        FieldSymbol fieldSymbol,
        BelteDiagnosticQueue diagnostics,
        LookupResultKind resultKind,
        bool indexed,
        bool hasErrors) {
        var hasError = false;

        if (!hasError)
            hasError = CheckInstanceOrStatic(node, receiver, fieldSymbol, ref resultKind, diagnostics);

        ConstantValue constantValueOpt = null;

        if (fieldSymbol.isConst && !isInsideNameof) {
            constantValueOpt = fieldSymbol.GetConstantValue(constantFieldsInProgress);

            if (constantValueOpt == ConstantValue.Unset)
                constantValueOpt = null;
        }

        if (!fieldSymbol.isStatic) {
            // WarnOnAccessOfOffDefault(node, receiver, diagnostics);
            // TODO warning?
        }

        // TODO error
        // if (!IsBadBaseAccess(node, receiver, fieldSymbol, diagnostics)) {
        //     CheckReceiverAndRuntimeSupportForSymbolAccess(node, receiver, fieldSymbol, diagnostics);
        // }

        var fieldType = fieldSymbol.GetFieldType(fieldsBeingBound).type;

        return new BoundFieldAccessExpression(
            node,
            receiver,
            fieldSymbol,
            constantValueOpt,
            fieldType,
            hasError
        );
    }

    private bool CheckInstanceOrStatic(
        SyntaxNode node,
        BoundExpression receiver,
        Symbol symbol,
        ref LookupResultKind resultKind,
        BelteDiagnosticQueue diagnostics) {
        var instanceReceiver = IsInstanceReceiver(receiver);

        if (!symbol.RequiresInstanceReceiver()) {
            if (instanceReceiver) {
                if (!isInsideNameof) {
                    // TODO error
                    // var error = flags.Includes(BinderFlags.ObjectInitializerMember) ?
                    //     ErrorCode.ERR_StaticMemberInObjectInitializer :
                    //     ErrorCode.ERR_ObjectProhibited;
                    // Error(diagnostics, errorCode, node, symbol);
                } else {
                    return false;
                }

                resultKind = LookupResultKind.StaticInstanceMismatch;
                return true;
            }
        } else {
            if (!instanceReceiver && !isInsideNameof) {
                diagnostics.Push(Error.InstanceRequired(node.location, symbol));
                resultKind = LookupResultKind.StaticInstanceMismatch;
                return true;
            }
        }

        return false;
    }

    private static bool IsInstanceReceiver(BoundExpression receiver) {
        return receiver is not null && receiver.kind != BoundKind.TypeExpression;
    }

    private BoundExpression MakeMemberAccessValue(BoundExpression expression, BelteDiagnosticQueue diagnostics) {
        switch (expression.kind) {
            case BoundKind.MethodGroup: {
                    /*
                        var methodGroup = (BoundMethodGroup)expression;
                        var resolution = ResolveMethodGroup(methodGroup, null);
                        diagnostics.PushRange(resolution.Diagnostics);

                        if (resolution.methodGroup is not null && !resolution.hasAnyErrors) {
                            var method = resolution.methodGroup.methods[0];
                            // Error(diagnostics, ErrorCode.ERR_BadSKunknown, methodGroup.NameSyntax, method, MessageID.IDS_SK_METHOD.Localize());
                            // TODO error
                        }

                        // expression = this.BindMemberAccessBadResult(methodGroup);
                        expression = new BoundErrorExpression(expression.type);
                        resolution.Free();
                        return expression;
                        */
                    // TODO do we even need a special case here?
                    return expression;
                }
            default:
                return BindToNaturalType(expression, diagnostics);
        }
    }

    private BoundExpression BindMethodGroup(
        ExpressionSyntax node,
        bool called,
        bool indexed,
        BelteDiagnosticQueue diagnostics) {
        switch (node.kind) {
            case SyntaxKind.IdentifierName:
            case SyntaxKind.TemplateName:
                return BindIdentifier((SimpleNameSyntax)node, called, indexed, diagnostics);
            case SyntaxKind.MemberAccessExpression:
                return BindMemberAccess((MemberAccessExpressionSyntax)node, called, indexed, diagnostics);
            case SyntaxKind.ParenthesizedExpression:
                return BindMethodGroup(((ParenthesisExpressionSyntax)node).expression, false, false, diagnostics);
            default:
                return BindExpressionInternal(node, diagnostics, called, indexed);
        }
    }

    private BoundExpression BindCallExpression(CallExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        BoundExpression result;
        var analyzedArguments = AnalyzedArguments.GetInstance();

        if (ReceiverIsInvocation(node, out var nested)) {
            var invocations = ArrayBuilder<CallExpressionSyntax>.GetInstance();

            invocations.Push(node);
            node = nested;

            while (ReceiverIsInvocation(node, out nested)) {
                invocations.Push(node);
                node = nested;
            }

            var boundExpression = BindMethodGroup(node.expression, true, false, diagnostics);

            while (true) {
                result = BindArgumentsAndInvocation(node, boundExpression, analyzedArguments, diagnostics);

                if (!invocations.TryPop(out node))
                    break;

                var memberAccess = (MemberAccessExpressionSyntax)node.expression;
                analyzedArguments.Clear();
                boundExpression = BindMemberAccessWithBoundLeft(
                    memberAccess,
                    result,
                    memberAccess.name,
                    memberAccess.operatorToken,
                    true,
                    false,
                    diagnostics
                );
            }

            invocations.Free();
        } else {
            var boundExpression = BindMethodGroup(node.expression, true, false, diagnostics);
            result = BindArgumentsAndInvocation(node, boundExpression, analyzedArguments, diagnostics);
        }

        analyzedArguments.Free();
        return result;

        BoundExpression BindArgumentsAndInvocation(
            CallExpressionSyntax node,
            BoundExpression boundExpression,
            AnalyzedArguments analyzedArguments,
            BelteDiagnosticQueue diagnostics) {
            boundExpression = CheckValue(boundExpression, BindValueKind.RValueOrMethodGroup, diagnostics);
            var name = boundExpression.kind == BoundKind.MethodGroup ? GetName(node.expression) : null;
            BindArgumentsAndNames(node.argumentList, diagnostics, analyzedArguments);
            return BindCallExpression(node, node.expression, name, boundExpression, analyzedArguments, diagnostics);
        }

        static bool ReceiverIsInvocation(CallExpressionSyntax node, out CallExpressionSyntax nested) {
            if (node.expression is MemberAccessExpressionSyntax {
                expression: CallExpressionSyntax receiver,
                kind: SyntaxKind.MemberAccessExpression
            }) {
                nested = receiver;
                return true;
            }

            nested = null;
            return false;
        }
    }

    private BoundExpression BindCallExpression(
        SyntaxNode node,
        SyntaxNode expression,
        string methodName,
        BoundExpression boundExpression,
        AnalyzedArguments analyzedArguments,
        BelteDiagnosticQueue diagnostics) {
        BoundExpression result;

        if (boundExpression.kind == BoundKind.MethodGroup) {
            result = BindMethodGroupInvocation(
                node,
                expression,
                methodName,
                (BoundMethodGroup)boundExpression,
                analyzedArguments,
                diagnostics
            );
        } else {
            if (!boundExpression.hasErrors)
                diagnostics.Push(Error.CannotCallNonMethod(expression.location, methodName));

            result = CreateErrorCall(node, boundExpression, LookupResultKind.NotInvocable, analyzedArguments);
        }

        return result;
    }

    internal MethodGroupResolution ResolveMethodGroup(
        BoundMethodGroup node,
        AnalyzedArguments analyzedArguments,
        RefKind returnRefKind = default,
        TypeSymbol returnType = null) {
        var methodResolution = ResolveDefaultMethodGroup(node, analyzedArguments, returnRefKind, returnType);

        if (methodResolution.isEmpty && !methodResolution.hasAnyErrors) {
            var diagnostics = BelteDiagnosticQueue.GetInstance();
            diagnostics.PushRange(methodResolution.diagnostics);

            BindMemberAccessReportError(
                node.memberAccessExpressionSyntax ?? node.nameSyntax,
                node.nameSyntax,
                node.name,
                node.receiver,
                node.lookupError,
                diagnostics
            );

            return new MethodGroupResolution(
                methodResolution.methodGroup,
                methodResolution.otherSymbol,
                methodResolution.overloadResolutionResult,
                methodResolution.analyzedArguments,
                methodResolution.resultKind,
                diagnostics
            );
        }

        return methodResolution;
    }

    private MethodGroupResolution ResolveDefaultMethodGroup(
        BoundMethodGroup node,
        AnalyzedArguments analyzedArguments,
        RefKind returnRefKind = default,
        TypeSymbol returnType = null) {
        var methods = node.methods;

        if (methods.Length == 0) {
            if (node.lookupSymbol is MethodSymbol method)
                methods = [method];
        }

        var sealedDiagnostics = BelteDiagnosticQueue.Discarded;

        if (node.lookupError is not null) {
            sealedDiagnostics = BelteDiagnosticQueue.GetInstance();
            sealedDiagnostics.Push(node.lookupError);
        }

        if (methods.Length == 0)
            return new MethodGroupResolution(node.lookupSymbol, node.resultKind, sealedDiagnostics);

        var methodGroup = MethodGroup.GetInstance();
        methodGroup.PopulateWithNonExtensionMethods(
            node.receiver,
            methods,
            node.templateArguments,
            node.resultKind,
            node.lookupError
        );

        if (node.lookupError is not null)
            return new MethodGroupResolution(methodGroup, sealedDiagnostics);

        if (analyzedArguments is null) {
            return new MethodGroupResolution(methodGroup, sealedDiagnostics);
        } else {
            var result = OverloadResolutionResult<MethodSymbol>.GetInstance();

            overloadResolution.MethodOverloadResolution(
                methodGroup.methods,
                methodGroup.templateArguments,
                methodGroup.receiver,
                analyzedArguments,
                result,
                returnRefKind,
                returnType
            );

            return new MethodGroupResolution(
                methodGroup,
                null,
                result,
                AnalyzedArguments.GetInstance(analyzedArguments),
                methodGroup.resultKind,
                sealedDiagnostics
            );
        }
    }

    private BoundExpression BindMethodGroupInvocation(
        SyntaxNode syntax,
        SyntaxNode expression,
        string methodName,
        BoundMethodGroup methodGroup,
        AnalyzedArguments analyzedArguments,
        BelteDiagnosticQueue diagnostics) {
        var resolution = ResolveMethodGroup(methodGroup, analyzedArguments);
        diagnostics.PushRange(resolution.diagnostics);

        BoundExpression result;
        if (resolution.hasAnyErrors) {
            LookupResultKind resultKind;

            if (resolution.overloadResolutionResult is not null) {
                // TODO Also find originalMethods and typeArguments to add to the bad call?
                resultKind = resolution.methodGroup.resultKind;
            } else {
                resultKind = methodGroup.resultKind;
            }

            result = CreateErrorCall(syntax, methodGroup.receiver, resultKind, analyzedArguments);
        } else if (!resolution.isEmpty) {
            if (resolution.resultKind != LookupResultKind.Viable) {
                if (resolution.methodGroup is not null) {
                    BindCallExpressionContinued(
                        syntax,
                        expression,
                        methodName,
                        resolution.overloadResolutionResult,
                        resolution.analyzedArguments,
                        resolution.methodGroup,
                        BelteDiagnosticQueue.Discarded
                    );
                }

                result = CreateErrorCall(syntax, methodGroup, methodGroup.resultKind, analyzedArguments);
            } else {
                result = BindCallExpressionContinued(
                    syntax,
                    expression,
                    methodName,
                    resolution.overloadResolutionResult,
                    resolution.analyzedArguments,
                    resolution.methodGroup,
                    diagnostics
                );
            }
        } else {
            result = CreateErrorCall(syntax, methodGroup, methodGroup.resultKind, analyzedArguments);
        }

        resolution.Free();
        return result;
    }

    private BoundCallExpression BindCallExpressionContinued(
        SyntaxNode node,
        SyntaxNode expression,
        string methodName,
        OverloadResolutionResult<MethodSymbol> result,
        AnalyzedArguments analyzedArguments,
        MethodGroup methodGroup,
        BelteDiagnosticQueue diagnostics) {
        if (!result.succeeded) {
            if (analyzedArguments.hasErrors) {
                // TODO should this do something eventually
                // foreach (var argument in analyzedArguments.arguments) {
                //     switch (argument) {
                //         case UnboundLambda unboundLambda:
                //             var boundWithErrors = unboundLambda.BindForErrorRecovery();
                //             diagnostics.AddRange(boundWithErrors.Diagnostics);
                //             break;
                //         case BoundUnconvertedObjectCreationExpression _:
                //         case BoundTupleLiteral _:
                //             // Tuple literals can contain unbound lambdas or switch expressions.
                //             _ = BindToNaturalType(argument, diagnostics);
                //             break;
                //         case BoundUnconvertedSwitchExpression { Type: { } naturalType } switchExpr:
                //             _ = ConvertSwitchExpression(switchExpr, naturalType, conversionIfTargetTyped: null, diagnostics);
                //             break;
                //         case BoundUnconvertedConditionalOperator { Type: { } naturalType } conditionalExpr:
                //             _ = ConvertConditionalExpression(conditionalExpr, naturalType, conversionIfTargetTyped: null, diagnostics);
                //             break;
                //     }
                // }
            } else {
                result.ReportDiagnostics(
                    this,
                    GetLocationForOverloadResolutionDiagnostic(node, expression),
                    node,
                    diagnostics,
                    methodName,
                    methodGroup.receiver,
                    expression,
                    analyzedArguments,
                    methodGroup.methods.ToImmutable(),
                    null
                );
            }

            return CreateErrorCall(node, methodGroup.receiver, methodGroup.resultKind, analyzedArguments);
        }

        var methodResult = result.bestResult;
        var returnType = methodResult.member.returnType;
        var method = methodResult.member;
        var receiver = methodGroup.receiver;

        CheckAndCoerceArguments(node, methodResult, analyzedArguments, diagnostics, receiver, out var argsToParams);
        BindDefaultArguments(
            node,
            method.parameters,
            analyzedArguments.arguments,
            analyzedArguments.refKinds,
            analyzedArguments.names,
            ref argsToParams,
            out var defaultArguments,
            true,
            diagnostics
        );

        var gotError = MemberGroupFinalValidation(receiver, method, expression, diagnostics);

        // TODO what is this error
        // CheckImplicitThisCopyInReadOnlyMember(receiver, method, diagnostics);

        // This will be the receiver of the BoundCall node that we create.
        // For extension methods, there is no receiver because the receiver in source was actually the first argument.
        // For instance methods, we may have synthesized an implicit this node.  We'll keep it for the emitter.
        // For static methods, we may have synthesized a type expression.  It serves no purpose, so we'll drop it.
        // TODO how to check for compiler generation?
        if (!method.requiresInstanceReceiver && receiver is not null /*&& receiver.WasCompilerGenerated*/)
            receiver = null;

        // TODO how to check for compiler generation?
        if (!gotError && method.requiresInstanceReceiver && receiver is not null && receiver.kind == BoundKind.ThisExpression /*&& receiver.WasCompilerGenerated*/) {
            gotError = IsRefOrOutThisParameterCaptured(node, diagnostics);
        }

        ImmutableArray<BoundExpression> args;
        ImmutableArray<RefKind> argRefKinds;

        if (argsToParams.IsDefault) {
            args = analyzedArguments.arguments.ToImmutable();
            argRefKinds = analyzedArguments.refKinds.ToImmutableOrNull();
        } else {
            // Could rearrange the arguments during lowering,
            // but this prevents any issues with walking the lowerer multiple times
            var arguments = analyzedArguments.arguments;
            var refKinds = analyzedArguments.refKinds;
            var argCount = arguments.Count;
            var argRefKindCount = refKinds.Count;

            var argsBuilder = new BoundExpression[argCount];
            var argRefKindsBuilder = new RefKind[argCount];

            for (var i = 0; i < argsToParams.Length; i++) {
                var target = argsToParams[i];
                argsBuilder[target] = arguments[i];

                if (i < argRefKindCount)
                    argRefKindsBuilder[target] = refKinds[i];
            }

            args = argsBuilder.ToImmutableArray();
            argRefKinds = argRefKindCount == 0 ? default : argRefKindsBuilder.ToImmutableArray();
        }

        return new BoundCallExpression(
            node,
            receiver,
            method,
            args,
            argRefKinds,
            defaultArguments,
            LookupResultKind.Viable,
            returnType,
            gotError
        );
    }

    private bool MemberGroupFinalValidation(
        BoundExpression receiver,
        MethodSymbol methodSymbol,
        SyntaxNode node,
        BelteDiagnosticQueue diagnostics) {
        // TODO
        // if (!IsBadBaseAccess(node, receiver, methodSymbol, diagnostics)) {
        //     CheckReceiverAndRuntimeSupportForSymbolAccess(node, receiver, methodSymbol, diagnostics);
        // }

        // if (MemberGroupFinalValidationAccessibilityChecks(receiver, methodSymbol, node, diagnostics, invokedAsExtensionMethod)) {
        //     return true;
        // }

        // return !methodSymbol.CheckConstraints(new ConstraintsHelper.CheckConstraintsArgs(this.Compilation, this.Conversions, includeNullability: false, node.Location, diagnostics));
        return false;
    }

    private void CheckAndCoerceArguments<TMember>(
        SyntaxNode node,
        MemberResolutionResult<TMember> methodResult,
        AnalyzedArguments analyzedArguments,
        BelteDiagnosticQueue diagnostics,
        BoundExpression receiver,
        out ImmutableArray<int> argsToParams)
        where TMember : Symbol {
        var result = methodResult.result;
        var arguments = analyzedArguments.arguments;
        var parameters = methodResult.leastOverriddenMember.GetParameters();

        for (var arg = 0; arg < arguments.Count; arg++) {
            var argument = arguments[arg];

            if (!argument.hasErrors) {
                var argRefKind = analyzedArguments.RefKind(arg);
                var argNumber = arg + 1;

                // Warn for `ref`/`in` or None/`ref readonly` mismatch.
                if (argRefKind == RefKind.Ref) {
                } else if (argRefKind == RefKind.None &&
                    GetCorrespondingParameter(in result, parameters, arg).refKind == RefKind.RefConst) {
                    if (!CheckValueKind(
                        argument.syntax,
                        argument,
                        BindValueKind.RefersToLocation,
                        checkingReceiver: false,
                        BelteDiagnosticQueue.Discarded)) {
                        // TODO Check this
                        // diagnostics.Add(
                        //     ErrorCode.WRN_RefReadonlyNotVariable,
                        //     argument.Syntax,
                        //     argNumber);
                    } else if (arg != 0) {
                        if (CheckValueKind(
                            argument.syntax,
                            argument,
                            BindValueKind.Assignable,
                            checkingReceiver: false,
                            BelteDiagnosticQueue.Discarded)) {
                            // TODO Check this
                            // Argument {0} should be passed with 'ref' or 'in' keyword
                            // diagnostics.Add(
                            //     ErrorCode.WRN_ArgExpectedRefOrIn,
                            //     argument.Syntax,
                            //     argNumber);
                        } else {
                            // TODO Check this
                            // Argument {0} should be passed with the 'in' keyword
                            // diagnostics.Add(
                            //     ErrorCode.WRN_ArgExpectedIn,
                            //     argument.Syntax,
                            //     argNumber);
                        }
                    }
                }
            }

            var paramNum = result.ParameterFromArgument(arg);
            arguments[arg] = CoerceArgument(
                in methodResult,
                receiver,
                parameters,
                argument,
                arg,
                parameters[paramNum].typeWithAnnotations,
                diagnostics
            );
        }

        argsToParams = result.argsToParams;
        return;

        BoundExpression CoerceArgument(
            in MemberResolutionResult<TMember> methodResult,
            BoundExpression receiver,
            ImmutableArray<ParameterSymbol> parameters,
            BoundExpression argument,
            int arg,
            TypeWithAnnotations parameterTypeWithAnnotations,
            BelteDiagnosticQueue diagnostics) {
            var result = methodResult.result;
            var kind = result.ConversionForArg(arg);
            var coercedArgument = argument;

            if (!kind.isIdentity) {
                coercedArgument = CreateConversion(
                    argument.syntax,
                    argument,
                    kind,
                    isCast: false,
                    parameterTypeWithAnnotations.type,
                    diagnostics
                );
            } else if (argument.NeedsToBeConverted()) {
                coercedArgument = BindToNaturalType(argument, diagnostics);
            }

            return coercedArgument;
        }

        static ParameterSymbol GetCorrespondingParameter(
            in MemberAnalysisResult result,
            ImmutableArray<ParameterSymbol> parameters,
            int arg) {
            var paramNum = result.ParameterFromArgument(arg);
            return parameters[paramNum];
        }
    }

    internal static ParameterSymbol? GetCorrespondingParameter(
        int argumentOrdinal,
        ImmutableArray<ParameterSymbol> parameters,
        ImmutableArray<int> argsToParamsOpt) {
        var n = parameters.Length;
        ParameterSymbol parameter;

        if (argsToParamsOpt.IsDefault) {
            if (argumentOrdinal < n)
                parameter = parameters[argumentOrdinal];
            else
                parameter = null;
        } else {
            var parameterOrdinal = argsToParamsOpt[argumentOrdinal];

            if (parameterOrdinal < n)
                parameter = parameters[parameterOrdinal];
            else
                parameter = null;
        }

        return parameter;
    }

    internal void BindDefaultArguments(
        SyntaxNode node,
        ImmutableArray<ParameterSymbol> parameters,
        ArrayBuilder<BoundExpression> argumentsBuilder,
        ArrayBuilder<RefKind>? argumentRefKindsBuilder,
        ArrayBuilder<(string Name, TextLocation Location)?>? namesBuilder,
        ref ImmutableArray<int> argsToParams,
        out BitVector defaultArguments,
        bool enableCallerInfo,
        BelteDiagnosticQueue diagnostics) {
        var paramsIndex = parameters.Length - 1;
        var visitedParameters = BitVector.Create(parameters.Length);

        for (var i = 0; i < argumentsBuilder.Count; i++) {
            var parameter = GetCorrespondingParameter(i, parameters, argsToParams);

            if (parameter is not null)
                visitedParameters[parameter.ordinal] = true;
        }

        var haveDefaultArguments = !parameters.All(
            static (param, visitedParameters) => visitedParameters[param.ordinal], visitedParameters
        );

        if (!haveDefaultArguments) {
            defaultArguments = default;
            return;
        }

        ArrayBuilder<int>? argsToParamsBuilder = null;
        if (!argsToParams.IsDefault) {
            argsToParamsBuilder = ArrayBuilder<int>.GetInstance(argsToParams.Length);
            argsToParamsBuilder.AddRange(argsToParams);
        }

        if (haveDefaultArguments) {
            var containingMember = this.containingMember;
            defaultArguments = BitVector.Create(parameters.Length);
            var lastIndex = ^0;
            var argumentsCount = argumentsBuilder.Count;

            foreach (var parameter in parameters.AsSpan()[..lastIndex]) {
                if (!visitedParameters[parameter.ordinal]) {
                    defaultArguments[argumentsBuilder.Count] = true;
                    argumentsBuilder.Add(BindDefaultArgument(
                        node,
                        parameter,
                        containingMember,
                        enableCallerInfo,
                        diagnostics,
                        argumentsBuilder,
                        argumentsCount,
                        argsToParams
                    ));

                    if (argumentRefKindsBuilder is { Count: > 0 })
                        argumentRefKindsBuilder.Add(RefKind.None);

                    argsToParamsBuilder?.Add(parameter.ordinal);

                    if (namesBuilder?.Count > 0)
                        namesBuilder.Add(null);
                }
            }
        } else {
            defaultArguments = default;
        }

        if (argsToParamsBuilder is not null) {
            argsToParams = argsToParamsBuilder.ToImmutableOrNull();
            argsToParamsBuilder.Free();
        }

        BoundExpression BindDefaultArgument(
            SyntaxNode syntax,
            ParameterSymbol parameter,
            Symbol containingMember,
            bool enableCallerInfo,
            BelteDiagnosticQueue diagnostics,
            ArrayBuilder<BoundExpression> argumentsBuilder,
            int argumentsCount,
            ImmutableArray<int> argsToParamsOpt) {
            var parameterType = parameter.type;

            if (flags.Includes(BinderFlags.ParameterDefaultValue)) {
                // TODO What to replace this with
                // return new BoundDefaultExpression(syntax, parameterType) { WasCompilerGenerated = true };
            }

            var parameterDefaultValue = parameter.explicitDefaultConstantValue;
            var defaultConstantValue = parameterDefaultValue.value;
            // var callerSourceLocation = enableCallerInfo ? GetCallerLocation(syntax) : null;
            BoundExpression defaultValue;

            // TODO The [CallerLineNumber] attribute is neat
            // if (callerSourceLocation is object && parameter.IsCallerLineNumber) {
            //     int line = callerSourceLocation.SourceTree.GetDisplayLineNumber(callerSourceLocation.SourceSpan);
            //     defaultValue = new BoundLiteral(syntax, ConstantValue.Create(line), Compilation.GetSpecialType(SpecialType.System_Int32)) { WasCompilerGenerated = true };
            // } else if (callerSourceLocation is object && parameter.IsCallerFilePath) {
            //     string path = callerSourceLocation.SourceTree.GetDisplayPath(callerSourceLocation.SourceSpan, Compilation.Options.SourceReferenceResolver);
            //     defaultValue = new BoundLiteral(syntax, ConstantValue.Create(path), Compilation.GetSpecialType(SpecialType.System_String)) { WasCompilerGenerated = true };
            // } else if (callerSourceLocation is object && parameter.IsCallerMemberName && containingMember is not null) {
            //     var memberName = containingMember.GetMemberCallerName();
            //     defaultValue = new BoundLiteral(syntax, ConstantValue.Create(memberName), Compilation.GetSpecialType(SpecialType.System_String)) { WasCompilerGenerated = true };
            // } else if (callerSourceLocation is object
            //       && !parameter.IsCallerMemberName
            //       && Conversions.ClassifyBuiltInConversion(Compilation.GetSpecialType(SpecialType.System_String), parameterType, isChecked: false, ref discardedUseSiteInfo).Exists
            //       && getArgumentIndex(parameter.CallerArgumentExpressionParameterIndex, argsToParamsOpt) is int argumentIndex
            //       && argumentIndex > -1 && argumentIndex < argumentsCount) {
            //     var argument = argumentsBuilder[argumentIndex];
            //     defaultValue = new BoundLiteral(syntax, ConstantValue.Create(argument.Syntax.ToString()), Compilation.GetSpecialType(SpecialType.System_String)) { WasCompilerGenerated = true };

            // TODO Any issue with just creating a literal null instead of default expression?
            // if (defaultConstantValue is null) {
            //     defaultValue = new BoundDefaultExpression(syntax, parameterType) { WasCompilerGenerated = true };
            // } else {
            TypeSymbol constantType = CorLibrary.GetSpecialType(parameterDefaultValue.specialType);
            defaultValue = new BoundLiteralExpression(syntax, parameterDefaultValue, constantType);
            // }

            var conversion = conversions.ClassifyConversionFromExpression(defaultValue, parameterType);

            if (!conversion.exists)
                GenerateImplicitConversionError(diagnostics, syntax, conversion, defaultValue, parameterType);

            var isCast = conversion.isExplicit;
            defaultValue = CreateConversion(
                defaultValue.syntax,
                defaultValue,
                conversion,
                isCast,
                parameterType,
                diagnostics
            );

            return defaultValue;

            // static int GetArgumentIndex(int parameterIndex, ImmutableArray<int> argsToParamsOpt)
            //     => argsToParamsOpt.IsDefault
            //         ? parameterIndex
            //         : argsToParamsOpt.IndexOf(parameterIndex);
        }
    }

    private static TextLocation GetLocationForOverloadResolutionDiagnostic(SyntaxNode node, SyntaxNode expression) {
        if (node != expression) {
            switch (expression.kind) {
                case SyntaxKind.QualifiedName:
                    return ((QualifiedNameSyntax)expression).right.location;
                case SyntaxKind.MemberAccessExpression:
                    return ((MemberAccessExpressionSyntax)expression).name.location;
            }
        }

        return expression.location;
    }

    private static ImmutableArray<MethodSymbol> GetOriginalMethods(
        OverloadResolutionResult<MethodSymbol> overloadResolutionResult) {
        if (overloadResolutionResult is null)
            return [];

        var builder = ArrayBuilder<MethodSymbol>.GetInstance();

        foreach (var result in overloadResolutionResult.results)
            builder.Add(result.member);

        return builder.ToImmutableAndFree();
    }

    private BoundCallExpression CreateErrorCall(
        SyntaxNode node,
        BoundExpression receiver,
        LookupResultKind resultKind,
        AnalyzedArguments analyzedArguments) {
        var returnType = new ExtendedErrorTypeSymbol(compilation, "", 0, null);
        var methodContainer = receiver.type ?? containingType;
        var method = new ErrorMethodSymbol(methodContainer, returnType, "");

        var args = BuildArgumentsForErrorRecovery(analyzedArguments);
        var argRefKinds = analyzedArguments.refKinds.ToImmutable();

        return new BoundCallExpression(
            node,
            BindToTypeForErrorRecovery(receiver),
            method,
            args,
            argRefKinds,
            default,
            resultKind,
            method.returnType,
            true
        );
    }

    private ImmutableArray<BoundExpression> BuildArgumentsForErrorRecovery(AnalyzedArguments analyzedArguments) {
        return BuildArgumentsForErrorRecovery(analyzedArguments, Enumerable.Empty<ImmutableArray<ParameterSymbol>>());
    }

    private ImmutableArray<BoundExpression> BuildArgumentsForErrorRecovery(
        AnalyzedArguments analyzedArguments,
        IEnumerable<ImmutableArray<ParameterSymbol>> parameterListList) {
        var argumentCount = analyzedArguments.arguments.Count;
        var newArguments = ArrayBuilder<BoundExpression>.GetInstance(argumentCount);
        newArguments.AddRange(analyzedArguments.arguments);

        for (var i = 0; i < argumentCount; i++) {
            var argument = newArguments[i];

            switch (argument.kind) {
                case BoundKind.ParameterExpression:
                case BoundKind.DataContainerExpression:
                    newArguments[i] = BindToTypeForErrorRecovery(argument);
                    break;
                default:
                    newArguments[i] = BindToTypeForErrorRecovery(argument, GetCorrespondingParameterTypeLocal(i));
                    break;
            }
        }

        return newArguments.ToImmutableAndFree();

        TypeSymbol GetCorrespondingParameterTypeLocal(int i) {
            TypeSymbol candidateType = null;

            foreach (var parameterList in parameterListList) {
                var parameterType = GetCorrespondingParameterType(analyzedArguments, i, parameterList);

                if (parameterType is not null) {
                    if (candidateType is null) {
                        candidateType = parameterType;
                    } else if (!candidateType.Equals(
                        parameterType,
                        TypeCompareKind.IgnoreArraySizesAndLowerBounds | TypeCompareKind.IgnoreNullability)) {
                        candidateType = null;
                        break;
                    }
                }
            }

            return candidateType;
        }
    }

    private static TypeSymbol GetCorrespondingParameterType(
        AnalyzedArguments analyzedArguments,
        int i,
        ImmutableArray<ParameterSymbol> parameterList) {
        var name = analyzedArguments.Name(i);

        if (name is not null) {
            foreach (var parameter in parameterList) {
                if (parameter.name == name)
                    return parameter.type;
            }

            return null;
        }

        return (i < parameterList.Length) ? parameterList[i].type : null;
    }

    private void BindArgumentsAndNames(
        ArgumentListSyntax argumentList,
        BelteDiagnosticQueue diagnostics,
        AnalyzedArguments result) {
        if (argumentList is null)
            return;

        var hadError = false;

        foreach (var argumentSyntax in argumentList.arguments)
            BindArgumentAndName(result, diagnostics, ref hadError, argumentSyntax);
    }

    private void BindArgumentAndName(
        AnalyzedArguments result,
        BelteDiagnosticQueue diagnostics,
        ref bool hadError,
        BaseArgumentSyntax argumentSyntax) {
        RefKind refKind;
        BoundExpression boundArgument;
        SyntaxToken identifier;

        if (argumentSyntax is OmittedArgumentSyntax omitted) {
            refKind = RefKind.None;
            identifier = null;
            boundArgument = new BoundLiteralExpression(omitted, new ConstantValue(null), null);
        } else if (argumentSyntax is ArgumentSyntax normal) {
            refKind = normal.refKeyword is null ? RefKind.None : RefKind.Ref;
            identifier = normal.identifier;
            boundArgument = BindValue(
                normal.expression,
                diagnostics,
                refKind == RefKind.None ? BindValueKind.RValue : BindValueKind.RefOrOut
            );
        } else {
            throw ExceptionUtilities.Unreachable();
        }

        BindArgumentAndName(
            result,
            diagnostics,
            argumentSyntax,
            boundArgument,
            identifier,
            refKind
        );
    }

    private void BindArgumentAndName(
        AnalyzedArguments result,
        BelteDiagnosticQueue diagnostics,
        BelteSyntaxNode argumentSyntax,
        BoundExpression boundArgumentExpression,
        SyntaxToken identifier,
        RefKind refKind) {
        var hasRefKinds = result.refKinds.Any();

        if (refKind != RefKind.None) {
            if (!hasRefKinds) {
                hasRefKinds = true;
                var argCount = result.arguments.Count;

                for (var i = 0; i < argCount; i++)
                    result.refKinds.Add(RefKind.None);
            }
        }

        if (hasRefKinds)
            result.refKinds.Add(refKind);

        var hasNames = result.names.Any();

        if (identifier is not null) {
            if (!hasNames) {
                var argCount = result.arguments.Count;

                for (var i = 0; i < argCount; i++)
                    result.names.Add(null);
            }

            result.AddName(identifier);
        } else if (hasNames) {
            diagnostics.Push(Error.NamedBeforeUnnamed(argumentSyntax.location));
            result.names.Add(null);
        }

        result.arguments.Add(boundArgumentExpression);
    }

    private static string GetName(ExpressionSyntax syntax) {
        var nameSyntax = GetNameSyntax(syntax, out var nameString);

        if (nameSyntax is not null)
            return nameSyntax.GetUnqualifiedName().identifier.text;

        return nameString;
    }

    private BoundExpression BindParenthesisExpression(
        ParenthesisExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var value = BindExpression(node.expression, diagnostics);
        CheckNotType(value, node.expression.location, diagnostics);
        return value;
    }

    private static bool CheckNotType(
        BoundExpression expression,
        TextLocation location,
        BelteDiagnosticQueue diagnostics) {
        switch (expression.kind) {
            case BoundKind.TypeExpression:
                diagnostics.Push(
                    Error.CannotUseType(location, ((BoundTypeExpression)expression).type)
                );

                return false;
            default:
                return true;
        }
    }

    private BoundStatement BindEmptyStatement(EmptyStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        return new BoundNopStatement(node);
    }

    private BoundLiteralExpression BindLiteralExpression(
        LiteralExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var value = node.token.value;

        if (value is null)
            return new BoundLiteralExpression(node, new ConstantValue(null, SpecialType.None), null);

        var specialType = SpecialTypeExtensions.SpecialTypeFromLiteralValue(value);
        var constantValue = new ConstantValue(value, specialType);
        var type = CorLibrary.GetSpecialType(specialType);
        return new BoundLiteralExpression(node, constantValue, type);
    }

    private BoundThisExpression BindThisExpression(ThisExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var hasErrors = true;

        if (!HasThis(true, out var inStaticContext)) {
            if (inStaticContext)
                diagnostics.Push(Error.CannotUseThisInStaticMethod(node.location));
            // TODO Just having this here doesn't seem right, but..
            // else
            //     diagnostics.Push(Error.CannotUseThis(node.location));
        } else {
            hasErrors = IsRefOrOutThisParameterCaptured(node.keyword, diagnostics);
        }

        return new BoundThisExpression(node, containingType, hasErrors);
    }

    private BoundBaseExpression BindBaseExpression(BaseExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var hasErrors = false;
        TypeSymbol baseType = containingType?.baseType;

        if (!HasThis(true, out var inStaticContext)) {
            if (inStaticContext)
                diagnostics.Push(Error.CannotUseBaseInStaticMethod(node.location));
            else
                diagnostics.Push(Error.CannotUseBase(node.location));

            hasErrors = true;
        } else if (baseType is null) {
            diagnostics.Push(Error.NoBaseClass(node.location, containingType));
            hasErrors = true;
        } else if (containingType is null || node.parent is null ||
            (node.parent.kind != SyntaxKind.MemberAccessExpression && node.parent.kind != SyntaxKind.IndexExpression)) {
            diagnostics.Push(Error.CannotUseBase(node.location));
            hasErrors = true;
        } else if (IsRefOrOutThisParameterCaptured(node.keyword, diagnostics)) {
            hasErrors = true;
        }

        return new BoundBaseExpression(node, baseType, hasErrors);
    }

    internal bool HasThis(bool isExplicit, out bool inStaticContext) {
        var member = containingMember;

        if (member?.isStatic == true) {
            inStaticContext = member.kind == SymbolKind.Field || member.kind == SymbolKind.Method;
            return false;
        }

        inStaticContext = false;

        if (_inConstructorInitializer)
            return false;

        if (inFieldInitializer)
            return false;

        return !isExplicit;
    }

    private bool IsRefOrOutThisParameterCaptured(SyntaxNodeOrToken thisOrBaseToken, BelteDiagnosticQueue diagnostics) {
        if (GetDiagnosticIfRefOrOutThisParameterCaptured(thisOrBaseToken.location) is { } diagnostic) {
            diagnostics.Push(diagnostic);
            return true;
        }

        return false;
    }

    private BelteDiagnostic GetDiagnosticIfRefOrOutThisParameterCaptured(TextLocation location) {
        var thisSymbol = containingMember.EnclosingThisSymbol();

        if (thisSymbol is not null &&
            thisSymbol.containingSymbol != containingMember &&
            thisSymbol.refKind != RefKind.None) {
            // TODO error, confirm this is the right one
            return Error.CannotUseThis(location);
        }

        return null;
    }

    #endregion

    #region Operators

    private BoundExpression BindBinaryExpression(BinaryExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        if (IsSimpleBinaryOperator(node))
            return BindSimpleBinaryOperator(node, diagnostics);
        else if (node.operatorToken.kind is SyntaxKind.IsKeyword or SyntaxKind.IsntKeyword)
            return BindIsOperator(node, diagnostics);
        else if (node.operatorToken.kind == SyntaxKind.AsKeyword)
            return BindAsOperator(node, diagnostics);
        else if (node.operatorToken.kind is SyntaxKind.PipePipeToken or SyntaxKind.AmpersandAmpersandToken)
            return BindConditionalLogicalOperator(node, diagnostics);
        else if (node.operatorToken.kind == SyntaxKind.QuestionQuestionToken)
            return BindNullCoalescingOperator(node, diagnostics);

        throw ExceptionUtilities.Unreachable();
    }

    private BoundExpression BindIsOperator(BinaryExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var isIsntOperator = node.operatorToken.kind == SyntaxKind.IsntKeyword;
        var resultType = (TypeSymbol)CorLibrary.GetSpecialType(SpecialType.Bool);
        var operand = BindRValueWithoutTargetType(node.left, diagnostics);

        if (node.right is LiteralExpressionSyntax l && l.token.value is null) {
            if (ConstantValue.IsNotNull(operand.constantValue)) {
                diagnostics.Push(Warning.AlwaysValue(node.location, isIsntOperator));
                return new BoundLiteralExpression(
                    node,
                    new ConstantValue(isIsntOperator, SpecialType.Bool),
                    resultType
                );
            }

            var boundRight = BindLiteralExpression(l, diagnostics);
            var constantValue = ConstantFolding.FoldIs(operand, boundRight, isIsntOperator);
            return new BoundIsOperator(node, operand, boundRight, isIsntOperator, constantValue, resultType);
        }

        var targetTypeWithAnnotations = BindType(node.right, diagnostics);
        var targetType = targetTypeWithAnnotations.type;
        var boundType = new BoundTypeExpression(node.right, targetTypeWithAnnotations, targetType);

        if (ConstantValue.IsNull(operand.constantValue) ||
            operand.kind == BoundKind.MethodGroup ||
            operand.type.IsVoidType()) {
            diagnostics.Push(Warning.AlwaysValue(node.location, isIsntOperator));
            return new BoundLiteralExpression(node, new ConstantValue(isIsntOperator, SpecialType.Bool), resultType);
        }

        if ((operand.type.isObjectType && targetType.isPrimitiveType) ||
            (operand.type.isPrimitiveType && targetType.isObjectType) ||
            targetType.isStatic) {
            diagnostics.Push(Warning.NeverGivenType(node.location, targetType));
            return new BoundLiteralExpression(node, new ConstantValue(isIsntOperator, SpecialType.Bool), resultType);
        }

        // TODO We might want to consider checking for casts if we use `is` for pattern matching
        // var operandType = operand.type;
        // var conversion = conversions.ClassifyBuiltInConversion(operandType, targetType);
        // var cast = CreateConversion(node.left, operand, conversion, false, targetType, diagnostics);
        return new BoundIsOperator(node, operand, boundType, isIsntOperator, null, resultType);
    }

    private BoundExpression BindAsOperator(BinaryExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var operand = BindRValueWithoutTargetType(node.left, diagnostics);
        var targetTypeWithAnnotations = BindType(node.right, diagnostics);
        var targetType = targetTypeWithAnnotations.type;
        var targetTypeKind = targetType.typeKind;
        var boundType = new BoundTypeExpression(node.right, targetTypeWithAnnotations, targetType);
        var resultType = targetType;

        if (operand.hasErrors || targetTypeKind == TypeKind.Error)
            return new BoundAsOperator(node, operand, boundType, null, null, resultType, true);

        if ((operand.type.isObjectType && targetType.isPrimitiveType) ||
            (operand.type.isPrimitiveType && targetType.isObjectType) ||
            targetType.isStatic) {
            diagnostics.Push(Warning.NeverGivenType(node.location, targetType));
            return new BoundLiteralExpression(node, new ConstantValue(false, SpecialType.Bool), resultType);
        }

        BoundValuePlaceholder operandPlaceholder;
        BoundExpression operandConversion;

        if (operand.IsLiteralNull()) {
            operandPlaceholder = new BoundValuePlaceholder(operand.syntax, operand.type);
            operandConversion = CreateConversion(
                node,
                operandPlaceholder,
                Conversion.NullLiteral,
                false,
                resultType,
                diagnostics
            );

            return new BoundAsOperator(node, operand, boundType, operandPlaceholder, operandConversion, resultType);
        }

        var operandType = operand.type;
        var conversion = conversions.ClassifyBuiltInConversion(operandType, targetType);

        var hasErrors = ReportAsOperatorConversionDiagnostics(
            node,
            diagnostics,
            operandType,
            targetType,
            conversion.kind,
            operand.constantValue
        );

        if (conversion.exists) {
            operandPlaceholder = new BoundValuePlaceholder(operand.syntax, operand.type);
            operandConversion = CreateConversion(node, operandPlaceholder, conversion, false, resultType, diagnostics);
        } else {
            operandPlaceholder = null;
            operandConversion = null;
        }

        return new BoundAsOperator(
            node,
            operand,
            boundType,
            operandPlaceholder,
            operandConversion,
            resultType,
            hasErrors
        );
    }

    private static bool ReportAsOperatorConversionDiagnostics(
        BelteSyntaxNode node,
        BelteDiagnosticQueue diagnostics,
        TypeSymbol operandType,
        TypeSymbol targetType,
        ConversionKind conversionKind,
        ConstantValue operandConstantValue) {
        var hasErrors = false;

        switch (conversionKind) {
            case ConversionKind.ImplicitReference:
            case ConversionKind.AnyBoxing:
            case ConversionKind.ImplicitNullable:
            case ConversionKind.Identity:
            case ConversionKind.ExplicitNullable:
            case ConversionKind.ExplicitReference:
            case ConversionKind.AnyUnboxing:
                break;

            default:
                if (!operandType.ContainsTemplateParameter() &&
                    !targetType.ContainsTemplateParameter() ||
                    operandType.IsVoidType()) {
                    // SymbolDistinguisher distinguisher = new SymbolDistinguisher(compilation, operandType, targetType);
                    // TODO what does the distinguisher do
                    diagnostics.Push(Error.CannotConvert(node.location, operandType, targetType));
                    hasErrors = true;
                }

                break;
        }

        if (!hasErrors) {
            ReportAsOperatorDiagnostics(node, diagnostics, operandType, targetType, conversionKind, operandConstantValue);
        }

        return hasErrors;
    }

    private static void ReportAsOperatorDiagnostics(
        BelteSyntaxNode node,
        BelteDiagnosticQueue diagnostics,
        TypeSymbol operandType,
        TypeSymbol targetType,
        ConversionKind conversionKind,
        ConstantValue operandConstantValue) {
        ConstantValue constantValue = null; // TODO ConstantFolding.FoldAs

        if (constantValue is not null)
            diagnostics.Push(Warning.AlwaysValue(node.location, null));
    }

    private BoundExpression BindNullCoalescingOperator(BinaryExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var leftOperand = BindValue(node.left, diagnostics, BindValueKind.RValue);
        leftOperand = BindToNaturalType(leftOperand, diagnostics);
        var rightOperand = BindValue(node.right, diagnostics, BindValueKind.RValue);

        if (leftOperand.hasErrors || rightOperand.hasErrors) {
            leftOperand = BindToTypeForErrorRecovery(leftOperand);
            rightOperand = BindToTypeForErrorRecovery(rightOperand);
            return new BoundNullCoalescingOperator(node, leftOperand, rightOperand, null, CreateErrorType(), true);
        }

        var optLeftType = leftOperand.type;
        var optRightType = rightOperand.type;
        var isLeftNullable = optLeftType is not null && optLeftType.IsNullableType();
        var optLeftType0 = isLeftNullable ? optLeftType.GetNullableUnderlyingType() : optLeftType;

        if (leftOperand.kind == BoundKind.MethodGroup)
            return GenerateNullCoalescingBadBinaryOpsError(node, leftOperand, rightOperand, diagnostics);

        if (isLeftNullable) {
            var rightConversion = conversions.ClassifyImplicitConversionFromExpression(rightOperand, optLeftType0);

            if (rightConversion.exists) {
                var convertedRightOperand = CreateConversion(
                    node.right,
                    rightOperand,
                    rightConversion,
                    false,
                    optLeftType0,
                    diagnostics
                );

                return new BoundNullCoalescingOperator(
                    node,
                    leftOperand,
                    convertedRightOperand,
                    ConstantFolding.FoldNullCoalescing(leftOperand, convertedRightOperand, optLeftType0),
                    optLeftType0
                );
            }
        }

        if (optLeftType is not null) {
            var rightConversion = conversions.ClassifyImplicitConversionFromExpression(rightOperand, optLeftType);

            if (rightConversion.exists) {
                var convertedRightOperand = CreateConversion(
                    node.right,
                    rightOperand,
                    rightConversion,
                    false,
                    optLeftType,
                    diagnostics
                );

                return new BoundNullCoalescingOperator(
                    node,
                    leftOperand,
                    convertedRightOperand,
                    ConstantFolding.FoldNullCoalescing(leftOperand, convertedRightOperand, optLeftType),
                    optLeftType
                );
            }
        }

        if (optRightType is not null) {
            rightOperand = BindToNaturalType(rightOperand, diagnostics);
            Conversion leftConversionClassification;

            if (isLeftNullable) {
                leftConversionClassification = conversions.ClassifyImplicitConversionFromType(optLeftType0, optRightType);

                if (leftConversionClassification.exists) {
                    var leftConversion = CreateConversion(
                        node,
                        leftOperand,
                        leftConversionClassification,
                        false,
                        optRightType,
                        diagnostics
                    );

                    return new BoundNullCoalescingOperator(
                        node,
                        leftConversion,
                        rightOperand,
                        ConstantFolding.FoldNullCoalescing(leftConversion, rightOperand, optRightType),
                        optRightType
                    );
                }
            } else {
                leftConversionClassification = conversions.ClassifyImplicitConversionFromExpression(
                    leftOperand,
                    optRightType
                );

                if (leftConversionClassification.exists) {
                    var leftConversion = CreateConversion(
                        node,
                        leftOperand,
                        leftConversionClassification,
                        false,
                        optRightType,
                        diagnostics
                    );

                    return new BoundNullCoalescingOperator(
                        node,
                        leftConversion,
                        rightOperand,
                        ConstantFolding.FoldNullCoalescing(leftConversion, rightOperand, optRightType),
                        optRightType
                    );
                }
            }
        }

        return GenerateNullCoalescingBadBinaryOpsError(node, leftOperand, rightOperand, diagnostics);
    }

    private BoundExpression GenerateNullCoalescingBadBinaryOpsError(
        BinaryExpressionSyntax node,
        BoundExpression leftOperand,
        BoundExpression rightOperand,
        BelteDiagnosticQueue diagnostics) {
        leftOperand = BindToTypeForErrorRecovery(leftOperand);
        rightOperand = BindToTypeForErrorRecovery(rightOperand);
        diagnostics.Push(Error.InvalidBinaryOperatorUse(
            node.location,
            SyntaxFacts.GetText(node.operatorToken.kind),
            leftOperand.type,
            rightOperand.type
        ));

        return new BoundNullCoalescingOperator(node, leftOperand, rightOperand, null, CreateErrorType(), true);
    }

    private BoundExpression BindConditionalLogicalOperator(
        BinaryExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var binary = node;
        ExpressionSyntax child;

        while (true) {
            child = binary.left;

            if (child is not BinaryExpressionSyntax childAsBinary ||
                (childAsBinary.operatorToken.kind is not SyntaxKind.PipePipeToken and
                    not SyntaxKind.AmpersandAmpersandToken)) {
                break;
            }

            binary = childAsBinary;
        }

        var left = BindRValueWithoutTargetType(child, diagnostics);

        do {
            binary = (BinaryExpressionSyntax)child.parent;
            var right = BindRValueWithoutTargetType(binary.right, diagnostics);
            left = BindConditionalLogicalOperator(binary, left, right, diagnostics);
            child = binary;
        } while ((object)child != node);

        return left;
    }

    private BoundExpression BindConditionalLogicalOperator(
        BinaryExpressionSyntax node,
        BoundExpression left,
        BoundExpression right,
        BelteDiagnosticQueue diagnostics) {
        var kind = SyntaxKindToBinaryOperatorKind(node.operatorToken.kind);

        if (left.type is not null && left.type.specialType == SpecialType.Bool &&
            right.type is not null && right.type.specialType == SpecialType.Bool) {
            var constantValue = ConstantFolding.FoldBinary(left, right, kind | BinaryOperatorKind.Bool, left.type);
            return new BoundBinaryOperator(
                node,
                left,
                right,
                kind | BinaryOperatorKind.Bool,
                constantValue,
                left.type
            );
        }

        var best = BinaryOperatorOverloadResolution(
            kind,
            left,
            right,
            node,
            diagnostics,
            out var lookupResult,
            out var originalUserDefinedOperators
        );

        if (!best.hasValue) {
            ReportBinaryOperatorError(node, diagnostics, node.operatorToken, left, right, lookupResult);
        } else {
            var signature = best.signature;
            var bothBool = signature.leftType.specialType == SpecialType.Bool &&
                signature.rightType.specialType == SpecialType.Bool;

            if (!bothBool) {
                ReportBinaryOperatorError(node, diagnostics, node.operatorToken, left, right, lookupResult);
            } else if (bothBool) {
                var resultLeft = CreateConversion(node.left, left, best.leftConversion, false, signature.leftType, diagnostics);
                var resultRight = CreateConversion(node.right, right, best.rightConversion, false, signature.rightType, diagnostics);
                var resultKind = kind | signature.kind.OperandTypes();
                return new BoundBinaryOperator(
                    node,
                    resultLeft,
                    resultRight,
                    resultKind,
                    null,
                    signature.returnType
                );
            }
        }

        return new BoundBinaryOperator(node, left, right, kind, null, CreateErrorType(), true);
    }

    private bool IsSimpleBinaryOperator(ExpressionSyntax node) {
        if (node is BinaryExpressionSyntax b) {
            switch (b.operatorToken.kind) {
                case SyntaxKind.PlusToken:
                case SyntaxKind.MinusToken:
                case SyntaxKind.AsteriskToken:
                case SyntaxKind.SlashToken:
                case SyntaxKind.PercentToken:
                case SyntaxKind.EqualsEqualsToken:
                case SyntaxKind.ExclamationEqualsToken:
                case SyntaxKind.LessThanLessThanToken:
                case SyntaxKind.GreaterThanGreaterThanToken:
                case SyntaxKind.GreaterThanGreaterThanGreaterThanToken:
                case SyntaxKind.AsteriskAsteriskToken:
                case SyntaxKind.AmpersandToken:
                case SyntaxKind.CaretToken:
                case SyntaxKind.PipeToken:
                case SyntaxKind.LessThanToken:
                case SyntaxKind.GreaterThanToken:
                case SyntaxKind.LessThanEqualsToken:
                case SyntaxKind.GreaterThanEqualsToken:
                    return true;
                case SyntaxKind.IsKeyword:
                case SyntaxKind.IsntKeyword:
                case SyntaxKind.AsKeyword:
                case SyntaxKind.AmpersandAmpersandToken:
                case SyntaxKind.PipePipeToken:
                case SyntaxKind.QuestionQuestionToken:
                default:
                    return false;
            }
        }

        return false;
    }

    private BoundExpression BindSimpleBinaryOperator(BinaryExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var syntaxNodes = ArrayBuilder<BinaryExpressionSyntax>.GetInstance();

        ExpressionSyntax current = node;
        while (IsSimpleBinaryOperator(current)) {
            var binOp = (BinaryExpressionSyntax)current;
            syntaxNodes.Push(binOp);
            current = binOp.left;
        }

        var result = BindExpression(current, diagnostics);

        if (node.operatorToken.kind == SyntaxKind.MinusToken && current.kind == SyntaxKind.ParenthesizedExpression) {
            if (result.kind == BoundKind.TypeExpression
                && !(((ParenthesisExpressionSyntax)current).expression.kind == SyntaxKind.ParenthesizedExpression)) {
                // TODO what is this error
                // Error(diagnostics, ErrorCode.ERR_PossibleBadNegCast, node);
            } else if (result.kind == BoundKind.ErrorExpression) {
                var parenthesizedExpression = (ParenthesisExpressionSyntax)current;

                if (parenthesizedExpression.expression.kind == SyntaxKind.IdentifierName
                    && ((IdentifierNameSyntax)parenthesizedExpression.expression).identifier.text == "dynamic") {
                    // TODO what is this error
                    // Error(diagnostics, ErrorCode.ERR_PossibleBadNegCast, node);
                }
            }
        }

        while (syntaxNodes.Count > 0) {
            var syntaxNode = syntaxNodes.Pop();
            var bindValueKind = GetBinaryAssignmentKind(syntaxNode.operatorToken.kind);
            var left = CheckValue(result, bindValueKind, diagnostics);
            var right = BindValue(syntaxNode.right, diagnostics, BindValueKind.RValue);
            var boundOp = BindSimpleBinaryOperator(syntaxNode, diagnostics, left, right);
            result = boundOp;
        }

        syntaxNodes.Free();
        return result;
    }

    private void ReportBinaryOperatorError(
        ExpressionSyntax node,
        BelteDiagnosticQueue diagnostics,
        SyntaxToken operatorToken,
        BoundExpression left,
        BoundExpression right,
        LookupResultKind resultKind) {
        if (resultKind == LookupResultKind.Ambiguous)
            diagnostics.Push(Error.AmbiguousBinaryOperator(node.location, operatorToken.text, left.type, right.type));
        else
            diagnostics.Push(Error.InvalidBinaryOperatorUse(node.location, operatorToken.text, left.type, right.type));
    }

    private static void ReportUnaryOperatorError(
        BelteSyntaxNode node,
        BelteDiagnosticQueue diagnostics,
        string operatorName,
        BoundExpression operand,
        LookupResultKind resultKind) {
        if (resultKind == LookupResultKind.Ambiguous)
            diagnostics.Push(Error.AmbiguousUnaryOperator(node.location, operatorName, operand.type));
        else
            diagnostics.Push(Error.InvalidUnaryOperatorUse(node.location, operatorName, operand.type));
    }

    private TypeSymbol GetBinaryOperatorErrorType(BinaryOperatorKind kind) {
        switch (kind) {
            case BinaryOperatorKind.Equal:
            case BinaryOperatorKind.NotEqual:
            case BinaryOperatorKind.GreaterThan:
            case BinaryOperatorKind.LessThan:
            case BinaryOperatorKind.GreaterThanOrEqual:
            case BinaryOperatorKind.LessThanOrEqual:
                return CorLibrary.GetSpecialType(SpecialType.Bool);
            default:
                return CreateErrorType();
        }
    }

    private BoundExpression BindSimpleBinaryOperator(
        BinaryExpressionSyntax node,
        BelteDiagnosticQueue diagnostics,
        BoundExpression left,
        BoundExpression right) {
        var kind = SyntaxKindToBinaryOperatorKind(node.operatorToken.kind);

        if (left.hasErrors || right.hasErrors) {
            left = BindToTypeForErrorRecovery(left);
            right = BindToTypeForErrorRecovery(right);

            return new BoundBinaryOperator(
                node,
                left,
                right,
                kind,
                null,
                GetBinaryOperatorErrorType(kind),
                true
            );
        }

        var leftNull = left.IsLiteralNull();
        var rightNull = right.IsLiteralNull();
        var isEquality = kind == BinaryOperatorKind.Equal || kind == BinaryOperatorKind.NotEqual;

        if (isEquality && (leftNull || rightNull)) {
            var type = CorLibrary.GetNullableType(SpecialType.Bool);
            return new BoundLiteralExpression(node, new ConstantValue(null, SpecialType.Bool), type);
        }

        var foundOperator = BindSimpleBinaryOperatorParts(
            node,
            diagnostics,
            left,
            right,
            kind,
            out var resultKind,
            out var originalUserDefinedOperators,
            out var signature,
            out var best
        );

        var resultOperatorKind = signature.kind;
        var hasErrors = false;

        if (!foundOperator) {
            ReportBinaryOperatorError(node, diagnostics, node.operatorToken, left, right, resultKind);
            resultOperatorKind &= ~BinaryOperatorKind.TypeMask;
            hasErrors = true;
        }

        var resultType = signature.returnType;
        var resultLeft = left;
        var resultRight = right;
        ConstantValue resultConstant = null;

        if (foundOperator && (resultOperatorKind.OperandTypes() != BinaryOperatorKind.NullableNull)) {
            resultLeft = CreateConversion(
                node.left,
                left,
                best.leftConversion,
                false,
                signature.leftType,
                diagnostics
            );

            resultRight = CreateConversion(
                node.right,
                right,
                best.rightConversion,
                false,
                signature.rightType,
                diagnostics
            );

            resultConstant = ConstantFolding.FoldBinary(
                resultLeft,
                resultRight,
                resultOperatorKind,
                signature.leftType
            );
        } else {
            resultLeft = BindToNaturalType(resultLeft, diagnostics, false);
            resultRight = BindToNaturalType(resultRight, diagnostics, false);
        }

        return new BoundBinaryOperator(
            node,
            resultLeft,
            resultRight,
            resultOperatorKind,
            resultConstant,
            resultType,
            hasErrors
        );
    }

    private bool BindSimpleBinaryOperatorParts(
        BinaryExpressionSyntax node,
        BelteDiagnosticQueue diagnostics,
        BoundExpression left,
        BoundExpression right,
        BinaryOperatorKind kind,
        out LookupResultKind resultKind,
        out ImmutableArray<MethodSymbol> originalUserDefinedOperators,
        out BinaryOperatorSignature resultSignature,
        out BinaryOperatorAnalysisResult best) {
        bool foundOperator;
        best = BinaryOperatorOverloadResolution(
            kind,
            left,
            right,
            node,
            diagnostics,
            out resultKind,
            out originalUserDefinedOperators
        );

        if (!best.hasValue) {
            resultSignature = new BinaryOperatorSignature(kind, null, null, CreateErrorType());
            foundOperator = false;
        } else {
            var signature = best.signature;
            var isObjectEquality = signature.kind is BinaryOperatorKind.ObjectEqual or
                BinaryOperatorKind.ObjectNotEqual;

            var leftNull = left.IsLiteralNull();
            var rightNull = right.IsLiteralNull();

            var leftType = left.type;
            var rightType = right.type;

            var isNullableEquality = signature.method is null &&
                (signature.kind.Operator() == BinaryOperatorKind.Equal ||
                signature.kind.Operator() == BinaryOperatorKind.NotEqual) &&
                (leftNull && rightType is not null && rightType.IsNullableType() ||
                    rightNull && leftType is not null && leftType.IsNullableType());

            if (isNullableEquality) {
                resultSignature = new BinaryOperatorSignature(
                    kind | BinaryOperatorKind.NullableNull,
                    null,
                    null,
                    CorLibrary.GetSpecialType(SpecialType.Bool)
                );

                foundOperator = true;
            } else {
                resultSignature = signature;
                foundOperator = !isObjectEquality ||
                    OperatorFacts.IsValidObjectEquality(conversions, leftType, leftNull, rightType, rightNull);
            }
        }

        return foundOperator;
    }

    private BinaryOperatorAnalysisResult BinaryOperatorOverloadResolution(
        BinaryOperatorKind kind,
        BoundExpression left,
        BoundExpression right,
        BelteSyntaxNode node,
        BelteDiagnosticQueue diagnostics,
        out LookupResultKind resultKind,
        out ImmutableArray<MethodSymbol> originalUserDefinedOperators) {
        var result = BinaryOperatorOverloadResolutionResult.GetInstance();
        overloadResolution.BinaryOperatorOverloadResolution(kind, left, right, result);
        var possiblyBest = result.best;

        if (result.results.Any()) {
            var builder = ArrayBuilder<MethodSymbol>.GetInstance();

            foreach (var analysisResult in result.results) {
                var method = analysisResult.signature.method;

                if (method is not null)
                    builder.Add(method);
            }

            originalUserDefinedOperators = builder.ToImmutableAndFree();

            if (possiblyBest.hasValue)
                resultKind = LookupResultKind.Viable;
            else if (result.AnyValid())
                resultKind = LookupResultKind.Ambiguous;
            else
                resultKind = LookupResultKind.OverloadResolutionFailure;
        } else {
            originalUserDefinedOperators = [];
            resultKind = possiblyBest.hasValue ? LookupResultKind.Viable : LookupResultKind.Empty;
        }

        result.Free();
        return possiblyBest;
    }

    private UnaryOperatorAnalysisResult UnaryOperatorOverloadResolution(
        UnaryOperatorKind kind,
        BoundExpression operand,
        BelteSyntaxNode node,
        BelteDiagnosticQueue diagnostics,
        out LookupResultKind resultKind,
        out ImmutableArray<MethodSymbol> originalUserDefinedOperators) {
        var result = UnaryOperatorOverloadResolutionResult.GetInstance();
        overloadResolution.UnaryOperatorOverloadResolution(kind, operand, result);
        var possiblyBest = result.best;

        if (result.results.Any()) {
            var builder = ArrayBuilder<MethodSymbol>.GetInstance();

            foreach (var analysisResult in result.results) {
                var method = analysisResult.signature.method;

                if (method is not null)
                    builder.Add(method);
            }

            originalUserDefinedOperators = builder.ToImmutableAndFree();

            if (possiblyBest.hasValue)
                resultKind = LookupResultKind.Viable;
            else if (result.AnyValid())
                resultKind = LookupResultKind.Ambiguous;
            else
                resultKind = LookupResultKind.OverloadResolutionFailure;
        } else {
            originalUserDefinedOperators = [];
            resultKind = possiblyBest.hasValue ? LookupResultKind.Viable : LookupResultKind.Empty;
        }

        result.Free();
        return possiblyBest;
    }

    internal static UnaryOperatorKind SyntaxKindToIncrementOperatorKind(SyntaxKind nodeKind, SyntaxKind operatorKind) {
        var isPostfix = nodeKind == SyntaxKind.PostfixExpression;

        return operatorKind switch {
            SyntaxKind.PlusPlusToken when isPostfix => UnaryOperatorKind.PostfixIncrement,
            SyntaxKind.PlusPlusToken => UnaryOperatorKind.PrefixIncrement,
            SyntaxKind.MinusMinusToken when isPostfix => UnaryOperatorKind.PostfixDecrement,
            SyntaxKind.MinusMinusToken => UnaryOperatorKind.PrefixDecrement,
            _ => throw ExceptionUtilities.UnexpectedValue(operatorKind),
        };
    }

    internal static UnaryOperatorKind SyntaxKindToUnaryOperatorKind(SyntaxKind kind) {
        return kind switch {
            SyntaxKind.PlusToken => UnaryOperatorKind.UnaryPlus,
            SyntaxKind.MinusToken => UnaryOperatorKind.UnaryMinus,
            SyntaxKind.ExclamationToken => UnaryOperatorKind.LogicalNegation,
            SyntaxKind.TildeToken => UnaryOperatorKind.BitwiseComplement,
            _ => throw ExceptionUtilities.UnexpectedValue(kind),
        };
    }

    internal static BinaryOperatorKind SyntaxKindToBinaryOperatorKind(SyntaxKind kind) {
        switch (kind) {
            case SyntaxKind.AsteriskToken:
            case SyntaxKind.AsteriskEqualsToken:
                return BinaryOperatorKind.Multiplication;
            case SyntaxKind.SlashEqualsToken:
            case SyntaxKind.SlashToken:
                return BinaryOperatorKind.Division;
            case SyntaxKind.PercentEqualsToken:
            case SyntaxKind.PercentToken:
                return BinaryOperatorKind.Modulo;
            case SyntaxKind.PlusEqualsToken:
            case SyntaxKind.PlusToken:
                return BinaryOperatorKind.Addition;
            case SyntaxKind.MinusEqualsToken:
            case SyntaxKind.MinusToken:
                return BinaryOperatorKind.Subtraction;
            case SyntaxKind.GreaterThanGreaterThanEqualsToken:
            case SyntaxKind.GreaterThanGreaterThanToken:
                return BinaryOperatorKind.RightShift;
            case SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken:
            case SyntaxKind.GreaterThanGreaterThanGreaterThanToken:
                return BinaryOperatorKind.UnsignedRightShift;
            case SyntaxKind.LessThanLessThanEqualsToken:
            case SyntaxKind.LessThanLessThanToken:
                return BinaryOperatorKind.LeftShift;
            case SyntaxKind.EqualsEqualsToken:
                return BinaryOperatorKind.Equal;
            case SyntaxKind.ExclamationEqualsToken:
                return BinaryOperatorKind.NotEqual;
            case SyntaxKind.GreaterThanToken:
                return BinaryOperatorKind.GreaterThan;
            case SyntaxKind.LessThanToken:
                return BinaryOperatorKind.LessThan;
            case SyntaxKind.GreaterThanEqualsToken:
                return BinaryOperatorKind.GreaterThanOrEqual;
            case SyntaxKind.LessThanEqualsToken:
                return BinaryOperatorKind.LessThanOrEqual;
            case SyntaxKind.AmpersandEqualsToken:
            case SyntaxKind.AmpersandToken:
                return BinaryOperatorKind.And;
            case SyntaxKind.PipeEqualsToken:
            case SyntaxKind.PipeToken:
                return BinaryOperatorKind.Or;
            case SyntaxKind.CaretEqualsToken:
            case SyntaxKind.CaretToken:
                return BinaryOperatorKind.Xor;
            case SyntaxKind.AmpersandAmpersandToken:
                return BinaryOperatorKind.ConditionalAnd;
            case SyntaxKind.PipePipeToken:
                return BinaryOperatorKind.ConditionalOr;
            case SyntaxKind.AsteriskAsteriskEqualsToken:
            case SyntaxKind.AsteriskAsteriskToken:
                return BinaryOperatorKind.Power;
            default:
                throw ExceptionUtilities.UnexpectedValue(kind);
        }
    }

    private static BindValueKind GetBinaryAssignmentKind(SyntaxKind kind) {
        switch (kind) {
            case SyntaxKind.EqualsToken:
                return BindValueKind.Assignable;
            case SyntaxKind.PlusEqualsToken:
            case SyntaxKind.MinusEqualsToken:
            case SyntaxKind.AsteriskEqualsToken:
            case SyntaxKind.SlashEqualsToken:
            case SyntaxKind.AmpersandEqualsToken:
            case SyntaxKind.PipeEqualsToken:
            case SyntaxKind.CaretEqualsToken:
            case SyntaxKind.AsteriskAsteriskEqualsToken:
            case SyntaxKind.LessThanLessThanEqualsToken:
            case SyntaxKind.GreaterThanGreaterThanEqualsToken:
            case SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken:
            case SyntaxKind.PercentEqualsToken:
            case SyntaxKind.QuestionQuestionEqualsToken:
                return BindValueKind.CompoundAssignment;
            default:
                return BindValueKind.RValue;
        }
    }

    private BoundExpression BindTernaryExpression(TernaryExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var whenTrue = node.center.UnwrapRefExpression(out var whenTrueRefKind);
        var whenFalse = node.right.UnwrapRefExpression(out var whenFalseRefKind);

        var isRef = whenTrueRefKind == RefKind.Ref && whenFalseRefKind == RefKind.Ref;

        if (!isRef) {
            if (whenFalseRefKind == RefKind.Ref)
                diagnostics.Push(Error.RefConditionalNeedsTwoRefs(whenFalse.GetFirstToken().location));

            if (whenTrueRefKind == RefKind.Ref)
                diagnostics.Push(Error.RefConditionalNeedsTwoRefs(whenTrue.GetFirstToken().location));
        }

        return isRef
            ? BindRefConditionalOperator(node, whenTrue, whenFalse, diagnostics)
            : BindValueConditionalOperator(node, whenTrue, whenFalse, diagnostics);
    }

    private BoundExpression BindValueConditionalOperator(
        TernaryExpressionSyntax node,
        ExpressionSyntax whenTrue,
        ExpressionSyntax whenFalse,
        BelteDiagnosticQueue diagnostics) {
        var condition = BindBooleanExpression(node.left, diagnostics);
        var trueExpr = BindValue(whenTrue, diagnostics, BindValueKind.RValue);
        var falseExpr = BindValue(whenFalse, diagnostics, BindValueKind.RValue);
        ConstantValue constantValue = null;
        var bestType = BestTypeInferrer.InferBestTypeForConditionalOperator(
            trueExpr,
            falseExpr,
            conversions,
            out var hadMultipleCandidates
        );

        if (bestType is null) {
            // ErrorCode noCommonTypeError = hadMultipleCandidates ? ErrorCode.ERR_AmbigQM : ErrorCode.ERR_InvalidQM;
            // constantValue = FoldConditionalOperator(condition, trueExpr, falseExpr);
            // TODO error
            return new BoundConditionalOperator(
                node,
                condition,
                false,
                trueExpr,
                falseExpr,
                constantValue,
                null,
                constantValue is null
            );
        }

        bool hasErrors;
        if (bestType.IsErrorType()) {
            trueExpr = BindToNaturalType(trueExpr, diagnostics, false);
            falseExpr = BindToNaturalType(falseExpr, diagnostics, false);
            hasErrors = true;
        } else {
            trueExpr = GenerateConversionForAssignment(bestType, trueExpr, diagnostics);
            falseExpr = GenerateConversionForAssignment(bestType, falseExpr, diagnostics);
            hasErrors = trueExpr.hasErrors || falseExpr.hasErrors;
        }

        if (!hasErrors)
            constantValue = ConstantFolding.FoldConditional(condition, trueExpr, falseExpr, bestType);

        return new BoundConditionalOperator(
            node,
            condition,
            isRef: false,
            trueExpr,
            falseExpr,
            constantValue,
            bestType,
            hasErrors
        );
    }

    private BoundExpression BindRefConditionalOperator(
        TernaryExpressionSyntax node,
        ExpressionSyntax whenTrue,
        ExpressionSyntax whenFalse,
        BelteDiagnosticQueue diagnostics) {
        var condition = BindBooleanExpression(node.left, diagnostics);
        var trueExpr = BindValue(whenTrue, diagnostics, BindValueKind.RValue | BindValueKind.RefersToLocation);
        var falseExpr = BindValue(whenFalse, diagnostics, BindValueKind.RValue | BindValueKind.RefersToLocation);
        var hasErrors = trueExpr.hasErrors | falseExpr.hasErrors;
        var trueType = trueExpr.type;
        var falseType = falseExpr.type;

        TypeSymbol type;
        if (!Conversions.HasIdentityConversion(trueType, falseType)) {
            // TODO error
            // if (!hasErrors)
            //     diagnostics.Add(ErrorCode.ERR_RefConditionalDifferentTypes, falseExpr.Syntax.Location, trueType);

            type = CreateErrorType();
            hasErrors = true;
        } else {
            type = BestTypeInferrer.InferBestTypeForConditionalOperator(trueExpr, falseExpr, conversions, out _);
        }

        trueExpr = BindToNaturalType(trueExpr, diagnostics, reportNoTargetType: false);
        falseExpr = BindToNaturalType(falseExpr, diagnostics, reportNoTargetType: false);

        return new BoundConditionalOperator(
            node,
            condition,
            isRef: true,
            trueExpr,
            falseExpr,
            constantValue: null,
            type,
            hasErrors
        );
    }

    private BoundExpression BindUnaryExpression(UnaryExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var operatorText = node.operatorToken.text;
        var operand = BindToNaturalType(BindValue(node.operand, diagnostics, BindValueKind.RValue), diagnostics);
        var kind = SyntaxKindToUnaryOperatorKind(node.operatorToken.kind);

        if (operand.type?.IsErrorType() == true) {
            return new BoundUnaryOperator(
                node,
                operand,
                kind,
                null,
                type: CreateErrorType(),
                hasErrors: true
            );
        }

        var best = UnaryOperatorOverloadResolution(
            kind,
            operand,
            node,
            diagnostics,
            out var resultKind,
            out var originalUserDefinedOperators
        );

        if (!best.hasValue) {
            ReportUnaryOperatorError(node, diagnostics, operatorText, operand, resultKind);

            return new BoundUnaryOperator(
                node,
                operand,
                kind,
                null,
                CreateErrorType(),
                hasErrors: true
            );
        }

        var signature = best.signature;

        var resultOperand = CreateConversion(
            operand.syntax,
            operand,
            best.conversion,
            isCast: false,
            signature.operandType,
            diagnostics
        );

        var resultType = signature.returnType;
        var resultOperatorKind = signature.kind;
        var resultConstant = ConstantFolding.FoldUnary(resultOperand, resultOperatorKind, resultType);

        return new BoundUnaryOperator(
            node,
            resultOperand,
            resultOperatorKind,
            resultConstant,
            // signature.Method,
            // signature.ConstrainedToTypeOpt,
            // resultKind,
            resultType
        );
    }

    private BoundExpression BindNullAssertOperator(PostfixExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var operand = BindExpression(node.operand, diagnostics);

        if (operand.IsLiteralNull()) {
            diagnostics.Push(Error.NullAssertAlwaysThrows(node.location));
            return new BoundNullAssertOperator(node, operand, null, CreateErrorType("<null>"), true);
        }

        var operandType = operand.type;

        if (!operandType.IsNullableType()) {
            diagnostics.Push(Error.NullAssertOnNonNullableType(node.location, operandType));
            return new BoundNullAssertOperator(node, operand, null, operandType, true);
        }

        var resultType = operandType.StrippedType();
        var constantValue = operand.constantValue;

        if (ConstantValue.IsNull(constantValue)) {
            diagnostics.Push(Error.NullAssertAlwaysThrows(node.location));
            return new BoundNullAssertOperator(node, operand, null, resultType, true);
        }

        return new BoundNullAssertOperator(node, operand, constantValue, resultType);
    }

    private BoundExpression BindIncrementOrNullAssertOperator(
        PostfixExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        if (node.operatorToken.kind == SyntaxKind.ExclamationToken)
            return BindNullAssertOperator(node, diagnostics);

        return BindIncrementOperator(node, node.operand, node.operatorToken, diagnostics);
    }

    private BoundExpression BindIncrementOperator(
        BelteSyntaxNode node,
        ExpressionSyntax operandSyntax,
        SyntaxToken operatorToken,
        BelteDiagnosticQueue diagnostics) {
        var operand = BindValue(operandSyntax, diagnostics, BindValueKind.IncrementDecrement);
        operand = BindToNaturalType(operand, diagnostics);
        var kind = SyntaxKindToIncrementOperatorKind(node.kind, operatorToken.kind);

        if (operand.hasErrors) {
            return new BoundIncrementOperator(
                node,
                kind,
                operand,
                operandPlaceholder: null,
                operandConversion: null,
                resultPlaceholder: null,
                resultConversion: null,
                LookupResultKind.Empty,
                originalUserDefinedOperators: default,
                CreateErrorType(),
                hasErrors: true
            );
        }

        var operandType = operand.type;

        var best = UnaryOperatorOverloadResolution(
            kind,
            operand,
            node,
            diagnostics,
            out var resultKind,
            out var originalUserDefinedOperators
        );

        if (!best.hasValue) {
            ReportUnaryOperatorError(node, diagnostics, operatorToken.text, operand, resultKind);

            return new BoundIncrementOperator(
                node,
                kind,
                operand,
                operandPlaceholder: null,
                operandConversion: null,
                resultPlaceholder: null,
                resultConversion: null,
                resultKind,
                originalUserDefinedOperators,
                CreateErrorType(),
                hasErrors: true
            );
        }

        var signature = best.signature;
        var resultPlaceholder = new BoundValuePlaceholder(node, signature.returnType);

        var resultConversion = GenerateConversionForAssignment(
            operandType,
            resultPlaceholder,
            diagnostics,
            ConversionForAssignmentFlags.IncrementAssignment
        );

        var hasErrors = resultConversion.hasErrors;

        if (resultConversion is not BoundCastExpression) {
            if ((object)resultConversion != resultPlaceholder) {
                resultPlaceholder = null;
                resultConversion = null;
            }
        }

        var operandPlaceholder = new BoundValuePlaceholder(operand.syntax, operand.type);
        var operandConversion = CreateConversion(
            node,
            operandPlaceholder,
            best.conversion,
            isCast: false,
            best.signature.operandType,
            diagnostics
        );

        return new BoundIncrementOperator(
            node,
            signature.kind,
            operand,
            // signature.Method,
            // signature.ConstrainedToTypeOpt,
            operandPlaceholder,
            operandConversion,
            resultPlaceholder,
            resultConversion,
            resultKind,
            originalUserDefinedOperators,
            operandType,
            hasErrors
        );
    }

    private BoundExpression BindAssignmentOperator(
        AssignmentExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        if (node.assignmentToken.kind == SyntaxKind.QuestionQuestionEqualsToken)
            return BindNullCoalescingCompoundAssignment(node, diagnostics);
        else if (node.assignmentToken.kind != SyntaxKind.EqualsToken)
            return BindCompoundAssignment(node, diagnostics);

        var rhsExpr = node.right.UnwrapRefExpression(out var refKind);
        var isRef = refKind == RefKind.Ref;
        var lhsKind = isRef ? BindValueKind.RefAssignable : BindValueKind.Assignable;
        var op1 = BindValue(node.left, diagnostics, lhsKind);
        var rhsKind = isRef ? GetRequiredRHSValueKindForRefAssignment(op1) : BindValueKind.RValue;
        var op2 = BindValue(rhsExpr, diagnostics, rhsKind);
        return BindAssignment(node.right, op1, op2, isRef, diagnostics);
    }

    private BoundExpression BindNullCoalescingCompoundAssignment(
        AssignmentExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var leftOperand = BindValue(node.left, diagnostics, BindValueKind.CompoundAssignment);
        var rightOperand = BindValue(node.right, diagnostics, BindValueKind.RValue);

        if (leftOperand.hasErrors || rightOperand.hasErrors) {
            leftOperand = BindToTypeForErrorRecovery(leftOperand);
            rightOperand = BindToTypeForErrorRecovery(rightOperand);
            return new BoundNullCoalescingAssignmentOperator(node, leftOperand, rightOperand, CreateErrorType(), true);
        }

        var leftType = leftOperand.type;

        if (!leftType.IsNullableType())
            return GenerateNullCoalescingAssignmentBadBinaryOpsError(node, leftOperand, rightOperand, diagnostics);

        var underlyingLeftType = leftType.GetNullableUnderlyingType();
        var underlyingRightConversion = conversions.ClassifyImplicitConversionFromExpression(
            rightOperand,
            underlyingLeftType
        );

        if (underlyingRightConversion.exists) {
            var convertedRightOperand = CreateConversion(
                rightOperand,
                underlyingRightConversion,
                underlyingLeftType,
                diagnostics
            );

            return new BoundNullCoalescingAssignmentOperator(node, leftOperand, convertedRightOperand, underlyingLeftType);
        }

        var rightConversion = conversions.ClassifyImplicitConversionFromExpression(rightOperand, leftType);

        if (rightConversion.exists) {
            var convertedRightOperand = CreateConversion(rightOperand, rightConversion, leftType, diagnostics);
            return new BoundNullCoalescingAssignmentOperator(node, leftOperand, convertedRightOperand, leftType);
        }

        return GenerateNullCoalescingAssignmentBadBinaryOpsError(node, leftOperand, rightOperand, diagnostics);
    }

    private BoundExpression GenerateNullCoalescingAssignmentBadBinaryOpsError(
        AssignmentExpressionSyntax node,
        BoundExpression leftOperand,
        BoundExpression rightOperand,
        BelteDiagnosticQueue diagnostics) {
        diagnostics.Push(Error.InvalidBinaryOperatorUse(
            node.location,
            SyntaxFacts.GetText(node.assignmentToken.kind),
            leftOperand.type,
            rightOperand.type
        ));

        leftOperand = BindToTypeForErrorRecovery(leftOperand);
        rightOperand = BindToTypeForErrorRecovery(rightOperand);

        return new BoundNullCoalescingAssignmentOperator(node, leftOperand, rightOperand, CreateErrorType(), true);
    }

    private BoundExpression BindCompoundAssignment(AssignmentExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var left = BindValue(node.left, diagnostics, GetBinaryAssignmentKind(node.kind));
        var right = BindValue(node.right, diagnostics, BindValueKind.RValue);
        var kind = SyntaxKindToBinaryOperatorKind(node.assignmentToken.kind);

        if (left.hasErrors || right.hasErrors) {
            left = BindToTypeForErrorRecovery(left);
            right = BindToTypeForErrorRecovery(right);
            return new BoundCompoundAssignmentOperator(
                node,
                left,
                right,
                BinaryOperatorSignature.Error,
                null,
                null,
                null,
                null,
                LookupResultKind.Empty,
                [],
                CreateErrorType(),
                true
            );
        }

        var best = BinaryOperatorOverloadResolution(
            kind,
            left,
            right,
            node,
            diagnostics,
            out var resultKind,
            out var originalUserDefinedOperators
        );

        if (!best.hasValue) {
            diagnostics.Push(
                Error.InvalidBinaryOperatorUse(node.location, node.assignmentToken.text, left.type, right.type)
            );

            left = BindToTypeForErrorRecovery(left);
            right = BindToTypeForErrorRecovery(right);

            return new BoundCompoundAssignmentOperator(
                node,
                left,
                right,
                BinaryOperatorSignature.Error,
                null,
                null,
                null,
                null,
                resultKind,
                originalUserDefinedOperators,
                CreateErrorType(),
                true
            );
        }

        var hasErrors = false;
        var bestSignature = best.signature;
        var rightConverted = CreateConversion(
            node.right,
            right,
            best.rightConversion,
            false,
            bestSignature.rightType,
            diagnostics
        );

        var isPredefinedOperator = !bestSignature.kind.IsUserDefined();
        var leftType = left.type;
        var finalPlaceholder = new BoundValuePlaceholder(node, bestSignature.returnType);

        var finalConversion = GenerateConversionForAssignment(
            leftType,
            finalPlaceholder,
            diagnostics,
            ConversionForAssignmentFlags.CompoundAssignment |
                (isPredefinedOperator
                    ? ConversionForAssignmentFlags.PredefinedOperator
                    : ConversionForAssignmentFlags.None)
        );

        if (finalConversion.hasErrors)
            hasErrors = true;

        if (finalConversion is not BoundCastExpression final) {
            if ((object)finalConversion != finalPlaceholder) {
                finalPlaceholder = null;
                finalConversion = null;
            }
        } else if (final.conversion.isExplicit && isPredefinedOperator && !kind.IsShift()) {
            var rightToLeftConversion = conversions.ClassifyConversionFromExpression(right, leftType);

            if (!rightToLeftConversion.isImplicit || !rightToLeftConversion.exists) {
                hasErrors = true;
                GenerateImplicitConversionError(diagnostics, node.right, rightToLeftConversion, right, leftType);
            }
        }

        var leftPlaceholder = new BoundValuePlaceholder(left.syntax, leftType);
        var leftConversion = CreateConversion(
            node.left,
            leftPlaceholder,
            best.leftConversion,
            false,
            best.signature.leftType,
            diagnostics
        );

        return new BoundCompoundAssignmentOperator(
            node,
            left,
            rightConverted,
            bestSignature,
            leftPlaceholder,
            leftConversion,
            finalPlaceholder,
            finalConversion,
            resultKind,
            originalUserDefinedOperators,
            leftType,
            hasErrors
        );
    }

    private BoundAssignmentOperator BindAssignment(
        SyntaxNode node,
        BoundExpression op1,
        BoundExpression op2,
        bool isRef,
        BelteDiagnosticQueue diagnostics) {
        var hasErrors = op1.hasErrors || op2.hasErrors;

        if (op1.type is { } lhsType && !lhsType.IsErrorType()) {
            if (op1.hasErrors)
                op1 = BindToTypeForErrorRecovery(op1);

            op2 = GenerateConversionForAssignment(
                lhsType,
                op2,
                hasErrors ? BelteDiagnosticQueue.Discarded : diagnostics,
                isRef ? ConversionForAssignmentFlags.RefAssignment : ConversionForAssignmentFlags.None
            );
        } else {
            op1 = BindToTypeForErrorRecovery(op1);
            op2 = BindToTypeForErrorRecovery(op2);
        }

        var type = op1.type;

        return new BoundAssignmentOperator(node, op1, op2, isRef, type);
    }

    private static BindValueKind GetRequiredRHSValueKindForRefAssignment(BoundExpression boundLeft) {
        var rhsKind = BindValueKind.RefersToLocation;
        var lhsRefKind = boundLeft.GetRefKind();

        if (lhsRefKind is RefKind.Ref)
            rhsKind |= BindValueKind.Assignable;

        return rhsKind;
    }

    #endregion

    #region Lookup

    internal Symbol ResultSymbol(
        LookupResult result,
        string simpleName,
        int arity,
        SyntaxNode where,
        BelteDiagnosticQueue diagnostics,
        out bool wasError,
        NamespaceOrTypeSymbol qualifier,
        LookupOptions options = default) {
        var symbols = result.symbols;
        wasError = false;

        if (result.isMultiViable) {
            if (symbols.Count > 1) {
                symbols.Sort(ConsistentSymbolOrder.Instance);
                var originalSymbols = symbols.ToImmutable();

                var best = GetBestSymbolInfo(symbols, out var secondBest);
                // TODO Could check for conflicting imports here
                var first = symbols[best.index];
                var second = symbols[secondBest.index];

                if (best.isFromSourceModule && !secondBest.isFromSourceModule)
                    return first;

                BelteDiagnostic error = null;
                bool reportError;

                if (first != second && NameAndArityMatchRecursively(first, second)) {
                    reportError = !(best.isFromSourceModule && secondBest.isFromSourceModule);

                    if (first.kind == SymbolKind.NamedType && second.kind == SymbolKind.NamedType) {
                        if (first.originalDefinition == second.originalDefinition) {
                            reportError = true;

                            error = Error.AmbiguousReference(
                                where.location,
                                (where as NameSyntax).ErrorDisplayName() ?? simpleName,
                                first,
                                second
                            );
                        } else {
                            // TODO is this a reachable error?
                            // ErrorCode.ERR_SameFullNameAggAgg: The type '{1}' exists in both '{0}' and '{2}'
                            // info = new CSDiagnosticInfo(ErrorCode.ERR_SameFullNameAggAgg, originalSymbols,
                            //     new object[] { first.ContainingAssembly, first, second.ContainingAssembly });

                            if (secondBest.isFromAddedModule) {
                                reportError = false;
                            }
                        }
                    } else if (first.kind == SymbolKind.Namespace && second.kind == SymbolKind.NamedType) {
                        // TODO is this a reachable error?
                        // ErrorCode.ERR_SameFullNameNsAgg: The namespace '{1}' in '{0}' conflicts with the type '{3}' in '{2}'
                        // info = new CSDiagnosticInfo(ErrorCode.ERR_SameFullNameNsAgg, originalSymbols,
                        //     new object[] { GetContainingAssembly(first), first, second.ContainingAssembly, second });

                        // Do not report this error if namespace is declared in source and the type is declared in added module,
                        // we already reported declaration error about this name collision.
                        if (best.isFromSourceModule && secondBest.isFromAddedModule)
                            reportError = false;
                    } else if (first.kind == SymbolKind.NamedType && second.kind == SymbolKind.Namespace) {
                        if (!secondBest.isFromCompilation || secondBest.isFromSourceModule) {
                            // TODO is this a reachable error?
                            // ErrorCode.ERR_SameFullNameNsAgg: The namespace '{1}' in '{0}' conflicts with the type '{3}' in '{2}'
                            // info = new CSDiagnosticInfo(ErrorCode.ERR_SameFullNameNsAgg, originalSymbols,
                            //     new object[] { GetContainingAssembly(second), second, first.ContainingAssembly, first });
                        } else {
                            // TODO is this a reachable error?
                            // ErrorCode.ERR_SameFullNameThisAggThisNs: The type '{1}' in '{0}' conflicts with the namespace '{3}' in '{2}'
                            // object arg0;

                            // if (best.isFromSourceModule) {
                            //     arg0 = first.GetFirstLocation().SourceTree.FilePath;
                            // } else {
                            //     Debug.Assert(best.IsFromAddedModule);
                            //     arg0 = first.ContainingModule;
                            // }

                            // ModuleSymbol arg2 = second.ContainingModule;

                            // // Merged namespaces that span multiple modules don't have a containing module,
                            // // so just use module with the smallest ordinal from the containing assembly.
                            // if ((object)arg2 == null) {
                            //     foreach (NamespaceSymbol ns in ((NamespaceSymbol)second).ConstituentNamespaces) {
                            //         if (ns.ContainingAssembly == Compilation.Assembly) {
                            //             ModuleSymbol module = ns.ContainingModule;

                            //             if ((object)arg2 == null || arg2.Ordinal > module.Ordinal) {
                            //                 arg2 = module;
                            //             }
                            //         }
                            //     }
                            // }

                            // Debug.Assert(arg2.ContainingAssembly == Compilation.Assembly);

                            // info = new CSDiagnosticInfo(ErrorCode.ERR_SameFullNameThisAggThisNs, originalSymbols,
                            //     new object[] { arg0, first, arg2, second });
                        }
                    } else {
                        diagnostics.Push(Error.AmbiguousReference(
                            where.location,
                            (where as NameSyntax).ErrorDisplayName() ?? simpleName,
                            first,
                            second
                        ));

                        reportError = true;
                    }
                } else {
                    reportError = true;

                    if (first is NamespaceOrTypeSymbol && second is NamespaceOrTypeSymbol) {
                        error = Error.AmbiguousReference(
                            where.location,
                            (where as NameSyntax).ErrorDisplayName() ?? simpleName,
                            first,
                            second
                        );
                    } else {
                        error = Error.AmbiguousMember(where.location, first, second);
                    }
                }

                wasError = true;

                if (reportError && error is not null)
                    diagnostics.Push(error);

                return new ExtendedErrorTypeSymbol(
                    GetContainingNamespaceOrType(symbols[0]),
                    originalSymbols,
                    LookupResultKind.Ambiguous,
                    error,
                    arity);
            } else {
                var singleResult = symbols[0];
                // TODO check if void can appear hear, would need error

                if (singleResult.kind == SymbolKind.ErrorType) {
                    var errorType = (ErrorTypeSymbol)singleResult;

                    if (errorType.unreported) {
                        var error = errorType.error;
                        diagnostics.Push(error);

                        singleResult = new ExtendedErrorTypeSymbol(
                            GetContainingNamespaceOrType(errorType),
                            errorType.name,
                            errorType.arity,
                            error,
                            false
                        );
                    }
                }

                return singleResult;
            }
        }

        wasError = true;

        if (result.kind == LookupResultKind.Empty) {
            var error = Error.UndefinedSymbol(where.location, simpleName);
            diagnostics.Push(error);

            return new ExtendedErrorTypeSymbol(
                qualifier ?? compilation.globalNamespaceInternal,
                simpleName,
                arity,
                error
            );
        }

        if (result.error is not null && (qualifier is null || qualifier.kind != SymbolKind.ErrorType))
            diagnostics.Push(result.error);

        if ((symbols.Count > 1) || (symbols[0] is NamespaceOrTypeSymbol) || result.kind == LookupResultKind.NotAType) {
            return new ExtendedErrorTypeSymbol(
                GetContainingNamespaceOrType(symbols[0]),
                symbols.ToImmutable(),
                result.kind,
                result.error,
                arity
            );
        }

        return symbols[0];
    }

    private static bool NameAndArityMatchRecursively(Symbol x, Symbol y) {
        while (true) {
            if (IsRoot(x))
                return IsRoot(y);

            if (IsRoot(y))
                return false;

            if (x.name != y.name || x.GetArity() != y.GetArity())
                return false;

            x = x.containingSymbol;
            y = y.containingSymbol;
        }

        static bool IsRoot(Symbol symbol) {
            return symbol is null || symbol is NamespaceSymbol { isGlobalNamespace: true };
        }
    }

    private Binder LookupSymbolsWithFallback(
        LookupResult result,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved = null,
        LookupOptions options = LookupOptions.Default) {
        var binder = LookupSymbolsInternal(result, name, arity, basesBeingResolved, options, false);

        if (result.kind != LookupResultKind.Viable && result.kind != LookupResultKind.Empty) {
            result.Clear();
            LookupSymbolsInternal(result, name, arity, basesBeingResolved, options, true);
        }

        return binder;
    }

    internal void LookupSymbolsSimpleName(
        LookupResult result,
        NamespaceOrTypeSymbol qualifier,
        string plainName,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        bool diagnose) {
        if (qualifier is null)
            LookupSymbolsInternal(result, plainName, arity, basesBeingResolved, options, diagnose);
        else
            LookupMembersInternal(result, qualifier, plainName, arity, basesBeingResolved, options, this, diagnose);
    }

    private void LookupMembersWithFallback(
        LookupResult result,
        NamespaceOrTypeSymbol namespaceOrType,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved = null,
        LookupOptions options = LookupOptions.Default) {
        LookupMembersInternal(
            result,
            namespaceOrType,
            name,
            arity,
            basesBeingResolved,
            options,
            this,
            false
        );

        if (!result.isMultiViable && !result.isClear) {
            result.Clear();
            LookupMembersInternal(
                result,
                namespaceOrType,
                name,
                arity,
                basesBeingResolved,
                options,
                this,
                true
            );
        }
    }

    private protected void LookupMembersInternal(
        LookupResult result,
        NamespaceOrTypeSymbol namespaceOrType,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        Binder originalBinder,
        bool diagnose) {
        if (namespaceOrType.isNamespace) {
            LookupMembersInNamespace(
                result,
                (NamespaceSymbol)namespaceOrType,
                name,
                arity,
                options,
                originalBinder,
                diagnose
            );
        } else {
            LookupMembersInType(
                result,
                (TypeSymbol)namespaceOrType,
                name,
                arity,
                basesBeingResolved,
                options,
                originalBinder,
                diagnose
            );
        }
    }

    private protected void LookupMembersInType(
        LookupResult result,
        TypeSymbol type,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        Binder originalBinder,
        bool diagnose) {
        switch (type.typeKind) {
            case TypeKind.TemplateParameter:
                LookupMembersInTemplateParameter(
                    result,
                    (TemplateParameterSymbol)type,
                    name,
                    arity,
                    basesBeingResolved,
                    options,
                    originalBinder,
                    diagnose
                );

                break;
            case TypeKind.Class:
            case TypeKind.Struct:
            case TypeKind.Array:
                LookupMembersInClass(
                    result,
                    type,
                    name,
                    arity,
                    basesBeingResolved,
                    options,
                    originalBinder,
                    diagnose
                );

                break;
            case TypeKind.Error:
                LookupMembersInErrorType(
                    result,
                    (ErrorTypeSymbol)type,
                    name,
                    arity,
                    basesBeingResolved,
                    options,
                    originalBinder,
                    diagnose
                );

                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(type.typeKind);
        }
    }

    private void LookupMembersInClass(
        LookupResult result,
        TypeSymbol type,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        Binder originalBinder,
        bool diagnose) {
        LookupMembersInClass(result, type, name, arity, basesBeingResolved, options, originalBinder, type, diagnose);
    }

    private void LookupMembersInTemplateParameter(
        LookupResult current,
        TemplateParameterSymbol templateParameter,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        Binder originalBinder,
        bool diagnose) {
        if ((options & LookupOptions.NamespacesOrTypesOnly) != 0)
            return;

        LookupMembersInClass(
            current,
            templateParameter.effectiveBaseClass,
            name,
            arity,
            basesBeingResolved,
            options,
            originalBinder,
            diagnose
        );
    }

    private void LookupMembersInClass(
        LookupResult result,
        TypeSymbol type,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        Binder originalBinder,
        TypeSymbol accessThroughType,
        bool diagnose) {
        var currentType = type;
        var temp = LookupResult.GetInstance();

        PooledHashSet<NamedTypeSymbol> visited = null;

        while (currentType is not null) {
            temp.Clear();

            LookupMembersWithoutInheritance(
                temp,
                currentType,
                name,
                arity,
                options,
                originalBinder,
                accessThroughType,
                diagnose,
                basesBeingResolved
            );

            MergeHidingLookupResults(result, temp, basesBeingResolved);
            var tempHidesMethod = temp.isMultiViable && temp.symbols[0].kind != SymbolKind.Method;

            if (result.isMultiViable && (tempHidesMethod || result.symbols[0].kind != SymbolKind.Method))
                break;

            if (basesBeingResolved is not null && basesBeingResolved.ContainsReference(type.originalDefinition)) {
                var other = GetNearestOtherSymbol(basesBeingResolved, type);
                var error = Error.CircularBase(type.location, type, other);
                var errorType = new ExtendedErrorTypeSymbol(compilation, name, arity, error, unreported: true);
                result.SetFrom(LookupResult.Good(errorType));
            }

            currentType = currentType.GetNextBaseType(basesBeingResolved, ref visited);
        }

        visited?.Free();
        temp.Free();
    }

    private static Symbol GetNearestOtherSymbol(ConsList<TypeSymbol> list, TypeSymbol type) {
        var other = type;

        for (; list is not null && list != ConsList<TypeSymbol>.Empty; list = list.tail) {
            if (TypeSymbol.Equals(list.head, type.originalDefinition, TypeCompareKind.ConsiderEverything)) {
                if (TypeSymbol.Equals(other, type, TypeCompareKind.ConsiderEverything) &&
                    list.tail is not null &&
                    list.tail != ConsList<TypeSymbol>.Empty) {
                    other = list.tail.head;
                }

                break;
            } else {
                other = list.head;
            }
        }

        return other;
    }

    private void MergeHidingLookupResults(
        LookupResult resultHiding,
        LookupResult resultHidden,
        ConsList<TypeSymbol> basesBeingResolved) {
        if (resultHiding.isMultiViable && resultHidden.isMultiViable) {
            var hidingSymbols = resultHiding.symbols;
            var hidingCount = hidingSymbols.Count;
            var hiddenSymbols = resultHidden.symbols;
            var hiddenCount = hiddenSymbols.Count;

            for (var i = 0; i < hiddenCount; i++) {
                var sym = hiddenSymbols[i];

                for (var j = 0; j < hidingCount; j++) {
                    var hidingSym = hidingSymbols[j];

                    if (hidingSym.kind != SymbolKind.Method || sym.kind != SymbolKind.Method)
                        goto symIsHidden;
                }

                hidingSymbols.Add(sym);
symIsHidden:;
            }
        } else {
            resultHiding.MergePrioritized(resultHidden);
        }
    }

    private protected static void LookupMembersWithoutInheritance(
        LookupResult result,
        TypeSymbol type,
        string name,
        int arity,
        LookupOptions options,
        Binder originalBinder,
        TypeSymbol accessThroughType,
        bool diagnose,
        ConsList<TypeSymbol> basesBeingResolved) {
        var members = GetCandidateMembers(type, name, options, originalBinder);

        foreach (var member in members) {
            var resultOfThisMember = originalBinder.CheckViability(
                member,
                arity,
                options,
                accessThroughType,
                diagnose,
                basesBeingResolved
            );

            result.MergeEqual(resultOfThisMember);
        }
    }

    private void LookupMembersInErrorType(
        LookupResult result,
        ErrorTypeSymbol errorType,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        Binder originalBinder,
        bool diagnose) {
        if (!errorType.candidateSymbols.IsDefault && errorType.candidateSymbols.Length == 1) {
            if (errorType.resultKind == LookupResultKind.Inaccessible) {
                if (errorType.candidateSymbols[0] is TypeSymbol candidateType) {
                    LookupMembersInType(
                        result,
                        candidateType,
                        name,
                        arity,
                        basesBeingResolved,
                        options,
                        originalBinder,
                        diagnose
                    );

                    return;
                }
            }
        }

        result.Clear();
    }


    private static void LookupMembersInNamespace(
        LookupResult result,
        NamespaceSymbol ns,
        string name,
        int arity,
        LookupOptions options,
        Binder originalBinder,
        bool diagnose) {
        var members = GetCandidateMembers(ns, name, options, originalBinder);

        foreach (var member in members) {
            var resultOfThisMember = originalBinder.CheckViability(member, arity, options, null, diagnose);
            result.MergeEqual(resultOfThisMember);
        }
    }

    internal static ImmutableArray<Symbol> GetCandidateMembers(
        NamespaceOrTypeSymbol nsOrType,
        string name,
        LookupOptions options,
        Binder originalBinder) {
        if ((options & LookupOptions.NamespacesOrTypesOnly) != 0 && nsOrType is TypeSymbol)
            return nsOrType.GetTypeMembers(name).Cast<NamedTypeSymbol, Symbol>();
        else
            return nsOrType.GetMembers(name);
    }

    internal static ImmutableArray<Symbol> GetCandidateMembers(
        NamespaceOrTypeSymbol nsOrType,
        LookupOptions options,
        Binder originalBinder) {
        if ((options & LookupOptions.NamespacesOrTypesOnly) != 0 && nsOrType is TypeSymbol)
            return StaticCast<Symbol>.From(nsOrType.GetTypeMembersUnordered());
        else
            return nsOrType.GetMembersUnordered();
    }

    private Binder LookupSymbolsInternal(
        LookupResult result,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        bool diagnose) {
        Binder binder = null;

        for (var scope = this; scope is not null && !result.isMultiViable; scope = scope.next) {
            if (binder is not null) {
                var tmp = LookupResult.GetInstance();
                scope.LookupSymbolsInSingleBinder(tmp, name, arity, basesBeingResolved, options, this, diagnose);
                result.MergeEqual(tmp);
                tmp.Free();
            } else {
                scope.LookupSymbolsInSingleBinder(result, name, arity, basesBeingResolved, options, this, diagnose);

                if (!result.isClear)
                    binder = scope;
            }
        }

        return binder;
    }

    internal virtual void LookupSymbolsInSingleBinder(
        LookupResult result,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        Binder originalBinder,
        bool diagnose) { }

    internal virtual void AddLookupSymbolsInfoInSingleBinder(
        LookupSymbolsInfo info,
        LookupOptions options,
        Binder originalBinder) { }

    internal void AddMemberLookupSymbolsInfo(
        LookupSymbolsInfo result,
        NamespaceOrTypeSymbol namespaceOrType,
        LookupOptions options,
        Binder originalBinder) {
        if (namespaceOrType.isNamespace)
            AddMemberLookupSymbolsInfoInNamespace(result, (NamespaceSymbol)namespaceOrType, options, originalBinder);
        else
            AddMemberLookupSymbolsInfoInType(result, (TypeSymbol)namespaceOrType, options, originalBinder);
    }

    private void AddMemberLookupSymbolsInfoInType(
        LookupSymbolsInfo result,
        TypeSymbol type,
        LookupOptions options,
        Binder originalBinder) {
        switch (type.typeKind) {
            case TypeKind.TemplateParameter:
                AddMemberLookupSymbolsInfoInTemplateParameter(
                    result,
                    (TemplateParameterSymbol)type,
                    options,
                    originalBinder
                );

                break;
            case TypeKind.Class:
            case TypeKind.Struct:
            case TypeKind.Array:
                AddMemberLookupSymbolsInfoInClass(result, type, options, originalBinder, type);
                break;
        }
    }

    private void AddMemberLookupSymbolsInfoInTemplateParameter(
        LookupSymbolsInfo result,
        TemplateParameterSymbol type,
        LookupOptions options,
        Binder originalBinder) {
        var effectiveBaseClass = type.effectiveBaseClass;
        AddMemberLookupSymbolsInfoInClass(result, effectiveBaseClass, options, originalBinder, effectiveBaseClass);
    }

    private void AddMemberLookupSymbolsInfoInClass(
        LookupSymbolsInfo result,
        TypeSymbol type,
        LookupOptions options,
        Binder originalBinder,
        TypeSymbol accessThroughType) {
        PooledHashSet<NamedTypeSymbol> visited = null;

        while (type is not null && !type.IsVoidType()) {
            AddMemberLookupSymbolsInfoWithoutInheritance(result, type, options, originalBinder, accessThroughType);
            type = type.GetNextBaseType(null, ref visited);
        }

        visited?.Free();
    }

    private static void AddMemberLookupSymbolsInfoWithoutInheritance(
        LookupSymbolsInfo result,
        TypeSymbol type,
        LookupOptions options,
        Binder originalBinder,
        TypeSymbol accessThroughType) {
        var candidateMembers = result.filterName is not null
            ? GetCandidateMembers(type, result.filterName, options, originalBinder)
            : GetCandidateMembers(type, options, originalBinder);

        foreach (var symbol in candidateMembers) {
            if (originalBinder.CanAddLookupSymbolInfo(symbol, options, result, accessThroughType))
                result.AddSymbol(symbol, symbol.name, symbol.GetArity());
        }
    }

    private static void AddMemberLookupSymbolsInfoInNamespace(
        LookupSymbolsInfo result,
        NamespaceSymbol ns,
        LookupOptions options,
        Binder originalBinder) {
        var candidateMembers = result.filterName is not null
            ? GetCandidateMembers(ns, result.filterName, options, originalBinder)
            : GetCandidateMembers(ns, options, originalBinder);

        foreach (var symbol in candidateMembers) {
            if (originalBinder.CanAddLookupSymbolInfo(symbol, options, result, null))
                result.AddSymbol(symbol, symbol.name, symbol.GetArity());
        }
    }

    internal bool CanAddLookupSymbolInfo(
        Symbol symbol,
        LookupOptions options,
        LookupSymbolsInfo info,
        TypeSymbol accessThroughType) {
        var name = symbol.name;

        if (!info.CanBeAdded(name))
            return false;

        if ((options & LookupOptions.NamespacesOrTypesOnly) != 0 && symbol is not NamespaceOrTypeSymbol) {
            return false;
        } else if ((options & LookupOptions.MustBeInvocableIfMember) != 0 && IsNonInvocableMember(symbol)) {
            return false;
        } else if (!IsAccessible(symbol, RefineAccessThroughType(options, accessThroughType))) {
            return false;
        } else if (!IsInScopeOfAssociatedSyntaxTree(symbol)) {
            return false;
        } else if ((options & LookupOptions.MustBeInstance) != 0 && !IsInstance(symbol)) {
            return false;
        } else if ((options & LookupOptions.MustNotBeInstance) != 0 && IsInstance(symbol)) {
            return false;
        } else if ((options & LookupOptions.MustNotBeNamespace) != 0 && (symbol.kind == SymbolKind.Namespace)) {
            return false;
        } else {
            return true;
        }
    }

    internal SingleLookupResult CheckViability(
        Symbol symbol,
        int arity,
        LookupOptions options,
        TypeSymbol accessThroughType,
        bool diagnose,
        ConsList<TypeSymbol> basesBeingResolved = null) {
        BelteDiagnostic error;

        if ((options & LookupOptions.MustNotBeParameter) != 0 && symbol is ParameterSymbol) {
            return LookupResult.Empty();
        } else if (!IsInScopeOfAssociatedSyntaxTree(symbol)) {
            return LookupResult.Empty();
        } else if ((options & (LookupOptions.MustNotBeInstance | LookupOptions.MustBeAbstractOrVirtual)) ==
            (LookupOptions.MustNotBeInstance | LookupOptions.MustBeAbstractOrVirtual) &&
            (symbol is not TypeSymbol && IsInstance(symbol) || !(symbol.isAbstract || symbol.isVirtual))) {
            return LookupResult.Empty();
            // } else if (WrongArity(symbol, arity, diagnose, options, out diagInfo)) {
            //     return LookupResult.WrongArity(symbol, diagInfo);
        } else if ((options & LookupOptions.NamespacesOrTypesOnly) != 0 && symbol is not NamespaceOrTypeSymbol) {
            return LookupResult.NotTypeOrNamespace(symbol, symbol, diagnose);
        } else if ((options & LookupOptions.MustBeInvocableIfMember) != 0
              && IsNonInvocableMember(symbol)) {
            return LookupResult.NotInvocable(symbol, symbol, diagnose);
        } else if (!IsAccessible(
            symbol,
            RefineAccessThroughType(options, accessThroughType),
            out var inaccessibleViaQualifier,
            basesBeingResolved)) {
            if (!diagnose)
                error = null;
            else if (inaccessibleViaQualifier)
                error = Error.InvalidProtectedAccess(symbol.location, accessThroughType, containingType);
            else
                error = Error.MemberIsInaccessible(symbol.location, symbol);

            return LookupResult.Inaccessible(symbol, error);
        } else if ((options & LookupOptions.MustBeInstance) != 0 && !IsInstance(symbol)) {
            error = Error.InstanceRequired(symbol.location, symbol);
            return LookupResult.StaticInstanceMismatch(symbol, error);
        } else if ((options & LookupOptions.MustNotBeInstance) != 0 && IsInstance(symbol)) {
            error = Error.NoInstanceRequired(symbol.location, symbol);
            return LookupResult.StaticInstanceMismatch(symbol, error);
        } else if ((options & LookupOptions.MustNotBeNamespace) != 0 && symbol.kind == SymbolKind.Namespace) {
            // TODO what is this error?
            // diagInfo = diagnose ? new CSDiagnosticInfo(ErrorCode.ERR_BadSKunknown, symbol, symbol.GetKindText()) : null;
            return LookupResult.NotTypeOrNamespace(symbol, null);
        } else {
            return LookupResult.Good(symbol);
        }
    }

    internal bool IsNonInvocableMember(Symbol symbol) {
        switch (symbol.kind) {
            case SymbolKind.Method:
            case SymbolKind.Field:
            case SymbolKind.NamedType:
                return !IsInvocableMember(symbol);
            default:
                return false;
        }
    }

    internal bool IsAccessible(
        Symbol symbol,
        TypeSymbol accessThroughType = null,
        ConsList<TypeSymbol> basesBeingResolved = null) {
        return IsAccessible(symbol, accessThroughType, out _, basesBeingResolved);
    }

    internal bool IsAccessible(
        Symbol symbol,
        TypeSymbol accessThroughType,
        out bool failedThroughTypeCheck,
        ConsList<TypeSymbol> basesBeingResolved = null) {
        if (flags.Includes(BinderFlags.IgnoreAccessibility)) {
            failedThroughTypeCheck = false;
            return true;
        }

        return IsAccessibleHelper(symbol, accessThroughType, out failedThroughTypeCheck);
    }

    internal virtual bool IsAccessibleHelper(
        Symbol symbol,
        TypeSymbol accessThroughType,
        out bool failedThroughTypeCheck) {
        return next.IsAccessibleHelper(symbol, accessThroughType, out failedThroughTypeCheck);
    }

    internal static bool IsSymbolAccessibleConditional(Symbol symbol, Symbol within) {
        return AccessCheck.IsSymbolAccessible(symbol, within);
    }


    internal bool IsSymbolAccessibleConditional(
        Symbol symbol,
        NamedTypeSymbol within,
        TypeSymbol throughTypeOpt = null) {
        return flags.Includes(BinderFlags.IgnoreAccessibility) ||
            AccessCheck.IsSymbolAccessible(symbol, within, throughTypeOpt);
    }

    internal bool IsSymbolAccessibleConditional(
        Symbol symbol,
        NamedTypeSymbol within,
        TypeSymbol throughTypeOpt,
        out bool failedThroughTypeCheck) {
        if (flags.Includes(BinderFlags.IgnoreAccessibility)) {
            failedThroughTypeCheck = false;
            return true;
        }

        return AccessCheck.IsSymbolAccessible(symbol, within, throughTypeOpt, out failedThroughTypeCheck);
    }

    private bool IsInvocableMember(Symbol symbol) {
        switch (symbol.kind) {
            case SymbolKind.Method:
                return true;
        }

        return false;
    }

    private static bool IsInstance(Symbol symbol) {
        switch (symbol.kind) {
            case SymbolKind.Field:
            case SymbolKind.Method:
                return symbol.RequiresInstanceReceiver();
            default:
                return false;
        }
    }

    private static TypeSymbol RefineAccessThroughType(LookupOptions options, TypeSymbol accessThroughType) {
        return ((options & LookupOptions.UseBaseReferenceAccessibility) != 0)
            ? null
            : accessThroughType;
    }

    private bool IsInScopeOfAssociatedSyntaxTree(Symbol symbol) {
        while (symbol is not null)
            symbol = symbol.containingType;

        if (symbol is null)
            return true;

        if ((object)symbol.declaringCompilation != compilation)
            return false;

        // TODO Is checking compilation good enough?
        // Might want to create a system of FileIdentifier s instead of comparing texts

        var symbolText = symbol.syntaxReference?.syntaxTree?.text;

        if (symbolText is null)
            return false;

        var binderText = GetEndText();

        return binderText == symbolText;

        SourceText GetEndText() {
            for (var binder = this; binder is not null; binder = binder.next) {
                if (binder is EndBinder lastBinder)
                    return lastBinder.associatedText;
            }

            throw ExceptionUtilities.Unreachable();
        }
    }

    #endregion

    #region Statements

    internal BoundStatement BindStatement(StatementSyntax node, BelteDiagnosticQueue diagnostics) {
        return node.kind switch {
            SyntaxKind.BlockStatement => BindBlockStatement((BlockStatementSyntax)node, diagnostics),
            SyntaxKind.ReturnStatement => BindReturnStatement((ReturnStatementSyntax)node, diagnostics),
            SyntaxKind.ExpressionStatement => BindExpressionStatement((ExpressionStatementSyntax)node, diagnostics),
            SyntaxKind.LocalDeclarationStatement => BindLocalDeclarationStatement((LocalDeclarationStatementSyntax)node, diagnostics),
            SyntaxKind.EmptyStatement => BindEmptyStatement((EmptyStatementSyntax)node, diagnostics),
            SyntaxKind.LocalFunctionStatement => BindLocalFunctionStatement((LocalFunctionStatementSyntax)node, diagnostics),
            SyntaxKind.IfStatement => BindIfStatement((IfStatementSyntax)node, diagnostics),
            /*
            case SyntaxKind.WhileStatement:
                return BindWhileStatement((WhileStatementSyntax)syntax);
            case SyntaxKind.ForStatement:
                return BindForStatement((ForStatementSyntax)syntax);
            case SyntaxKind.DoWhileStatement:
                return BindDoWhileStatement((DoWhileStatementSyntax)syntax);
            case SyntaxKind.TryStatement:
                return BindTryStatement((TryStatementSyntax)syntax);
            case SyntaxKind.BreakStatement:
                return BindBreakStatement((BreakStatementSyntax)syntax);
            case SyntaxKind.ContinueStatement:
                return BindContinueStatement((ContinueStatementSyntax)syntax);
            */
            _ => throw ExceptionUtilities.UnexpectedValue(node.kind),
        };
    }

    internal BoundStatement BindPossibleEmbeddedStatement(StatementSyntax node, BelteDiagnosticQueue diagnostics) {
        Binder binder;

        switch (node.kind) {
            case SyntaxKind.LocalDeclarationStatement:
                diagnostics.Push(Error.BadEmbeddedStatement(node.location));
                goto case SyntaxKind.ExpressionStatement;
            case SyntaxKind.ExpressionStatement:
            case SyntaxKind.IfStatement:
            case SyntaxKind.ReturnStatement:
                binder = GetBinder(node);
                return binder.WrapWithVariablesIfAny(node, binder.BindStatement(node, diagnostics));
            case SyntaxKind.LocalFunctionStatement:
                diagnostics.Push(Error.BadEmbeddedStatement(node.location));
                binder = GetBinder(node);
                return binder.WrapWithVariablesAndLocalFunctionsIfAny(node, binder.BindStatement(node, diagnostics));
            case SyntaxKind.EmptyStatement:
                var emptyStatement = (EmptyStatementSyntax)node;

                if (!emptyStatement.semicolon.isFabricated) {
                    switch (node.parent.kind) {
                        case SyntaxKind.ForStatement:
                        case SyntaxKind.WhileStatement:
                            if (emptyStatement.semicolon.GetNextToken().kind != SyntaxKind.OpenBraceToken)
                                break;

                            goto default;
                        default:
                            diagnostics.Push(Warning.PossibleMistakenEmptyStatement(node.location));
                            break;
                    }
                }

                goto default;
            default:
                return BindStatement(node, diagnostics);
        }
    }

    private BoundIfStatement BindIfStatement(IfStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        var condition = BindBooleanExpression(node.condition, diagnostics);
        var consequence = BindPossibleEmbeddedStatement(node.then, diagnostics);
        var alternative = (node.elseClause is null)
            ? null
            : BindPossibleEmbeddedStatement(node.elseClause.body, diagnostics);

        return new BoundIfStatement(node, condition, consequence, alternative);
    }

    private BoundStatement BindLocalFunctionStatement(
        LocalFunctionStatementSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var localSymbol = LookupLocalFunction(node.identifier);
        var hasErrors = localSymbol.scopeBinder.ValidateDeclarationNameConflictsInScope(localSymbol, diagnostics);

        BoundBlockStatement body = null;

        if (node.body is not null) {
            body = RunAnalysis(BindBlockStatement(node.body, diagnostics), diagnostics);
        } else if (!hasErrors) {
            // TODO is this a reachable error
            hasErrors = true;
            // diagnostics.Add(ErrorCode.ERR_LocalFunctionMissingBody, localSymbol.GetFirstLocation(), localSymbol);
        }

        localSymbol.GetDeclarationDiagnostics(diagnostics);

        return new BoundLocalFunctionStatement(node, localSymbol, body, hasErrors);

        BoundBlockStatement RunAnalysis(BoundBlockStatement block, BelteDiagnosticQueue blockDiagnostics) {
            if (block is not null) {
                // TODO do we need to do any control flow analysis here
            }

            return block;
        }
    }

    private BoundLocalDeclarationStatement BindLocalDeclarationStatement(
        LocalDeclarationStatementSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var typeSyntax = node.declaration.type.SkipRef(out _);
        var isConst = node.isConst;
        var isConstExpr = node.isConstExpr;

        var declarationType = BindVariableTypeWithAnnotations(
            node.declaration,
            diagnostics,
            typeSyntax,
            ref isConst,
            ref isConstExpr,
            out var isImplicitlyTyped
        );

        var kind = isConstExpr
            ? DataContainerDeclarationKind.ConstantExpression
            : (isConst ? DataContainerDeclarationKind.Constant : DataContainerDeclarationKind.Variable);

        return BindVariableDeclaration(
            kind,
            isImplicitlyTyped,
            node.declaration,
            typeSyntax,
            declarationType,
            diagnostics,
            true,
            node
        );
    }

    private protected BoundLocalDeclarationStatement BindVariableDeclaration(
        DataContainerDeclarationKind kind,
        bool isImplicitlyTyped,
        VariableDeclarationSyntax declaration,
        TypeSyntax typeSyntax,
        TypeWithAnnotations declarationType,
        BelteDiagnosticQueue diagnostics,
        bool includeBoundType,
        BelteSyntaxNode associatedSyntaxNode = null) {
        return BindVariableDeclaration(
            LocateDeclaredVariableSymbol(declaration, typeSyntax),
            kind,
            isImplicitlyTyped,
            declaration,
            typeSyntax,
            declarationType,
            diagnostics,
            includeBoundType,
            associatedSyntaxNode
        );
    }

    private SourceDataContainerSymbol LocateDeclaredVariableSymbol(
        VariableDeclarationSyntax declaration,
        TypeSyntax typeSyntax) {
        return LocateDeclaredVariableSymbol(
            declaration.identifier,
            typeSyntax,
            declaration.initializer,
            DataContainerDeclarationKind.Variable
        );
    }

    private SourceDataContainerSymbol LocateDeclaredVariableSymbol(
        SyntaxToken identifier,
        TypeSyntax typeSyntax,
        EqualsValueClauseSyntax equalsValue,
        DataContainerDeclarationKind kind) {
        var localSymbol = LookupLocal(identifier) ?? SourceDataContainerSymbol.MakeLocal(
            containingMember,
            this,
            false,
            typeSyntax,
            identifier,
            kind,
            equalsValue
        );

        return localSymbol;
    }

    private protected BoundLocalDeclarationStatement BindVariableDeclaration(
        SourceDataContainerSymbol localSymbol,
        DataContainerDeclarationKind kind,
        bool isImplicitlyTyped,
        VariableDeclarationSyntax declaration,
        TypeSyntax typeSyntax,
        TypeWithAnnotations declarationType,
        BelteDiagnosticQueue diagnostics,
        bool includeBoundType,
        BelteSyntaxNode associatedSyntaxNode = null) {
        var localDiagnostics = BelteDiagnosticQueue.GetInstance();
        associatedSyntaxNode ??= declaration;

        var nameConflict = localSymbol.scopeBinder.ValidateDeclarationNameConflictsInScope(localSymbol, diagnostics);
        var hasErrors = false;
        var equalsClauseSyntax = declaration.initializer;

        if (!IsInitializerRefKindValid(
            equalsClauseSyntax,
            declaration,
            localSymbol.refKind,
            diagnostics,
            out var valueKind,
            out var value)) {
            hasErrors = true;
        }

        BoundExpression initializer;
        if (isImplicitlyTyped) {
            initializer = BindInferredVariableInitializer(diagnostics, value, valueKind, declaration);
            var initializerType = initializer?.type;

            if (initializerType is not null) {
                declarationType = new TypeWithAnnotations(initializerType);

                if (declarationType.IsVoidType()) {
                    // Error(localDiagnostics, ErrorCode.ERR_ImplicitlyTypedVariableAssignedBadValue, declarator, declTypeOpt.Type);
                    // TODO is this a reachable error?
                    declarationType = new TypeWithAnnotations(CreateErrorType("var"));
                    hasErrors = true;
                } else if (initializerType.isPrimitiveType && initializer.constantValue is not null) {
                    // TODO This condition to check for auto-lifting *should* be correct for every case, but need to double check
                    declarationType = declarationType.SetIsAnnotated();
                }

                if (!declarationType.type.IsErrorType()) {
                    if (declarationType.isStatic) {
                        diagnostics.Push(Error.CannotInitializeVarWithStaticClass(
                            typeSyntax.location,
                            initializerType
                        ));

                        hasErrors = true;
                    }
                }
            } else {
                declarationType = new TypeWithAnnotations(CreateErrorType("var"));
                hasErrors = true;
            }
        } else {
            if (equalsClauseSyntax is null) {
                if (declarationType.IsNullableType()) {
                    initializer = new BoundLiteralExpression(
                        declaration,
                        new ConstantValue(null),
                        declarationType.type
                    );
                } else {
                    initializer = ErrorExpression(declaration);
                    diagnostics.Push(Error.NoInitOnNonNullable(declaration.location));
                    hasErrors = true;
                }
            } else {
                initializer = BindPossibleArrayInitializer(value, declarationType.type, valueKind, diagnostics);
                initializer = GenerateConversionForAssignment(
                    declarationType.type,
                    initializer,
                    localDiagnostics,
                    localSymbol.refKind != RefKind.None
                        ? ConversionForAssignmentFlags.RefAssignment
                        : ConversionForAssignmentFlags.None
                );
            }
        }

        localSymbol.SetTypeWithAnnotations(declarationType);

        if (kind == DataContainerDeclarationKind.Constant && initializer is not null) {
            var constantValueDiagnostics = localSymbol.GetConstantValueDiagnostics(initializer);
            diagnostics.PushRange(constantValueDiagnostics);
            hasErrors = constantValueDiagnostics.AnyErrors();
        }

        diagnostics.PushRangeAndFree(localDiagnostics);
        BoundTypeExpression boundDeclType = null;

        if (includeBoundType) {
            var invalidDimensions = ArrayBuilder<BoundExpression>.GetInstance();

            typeSyntax.VisitRankSpecifiers((rankSpecifier, args) => {
                var _ = false;
                var size = args.binder.BindArrayDimension(rankSpecifier.size, args.diagnostics, ref _);
                if (size is not null)
                    args.invalidDimensions.Add(size);
            }, (binder: this, invalidDimensions, diagnostics));

            boundDeclType = new BoundTypeExpression(typeSyntax, declarationType, declarationType.type);
        }

        return new BoundLocalDeclarationStatement(
            associatedSyntaxNode,
            new BoundDataContainerDeclaration(
                declaration,
                localSymbol,
                hasErrors ? BindToTypeForErrorRecovery(initializer) : initializer
            ),
            hasErrors | nameConflict
        );
    }

    internal BoundExpression BindInferredVariableInitializer(
        BelteDiagnosticQueue diagnostics,
        RefKind refKind,
        EqualsValueClauseSyntax initializer,
        BelteSyntaxNode errorSyntax) {
        IsInitializerRefKindValid(initializer, initializer, refKind, diagnostics, out var valueKind, out var value);
        return BindInferredVariableInitializer(diagnostics, value, valueKind, errorSyntax);
    }

    private protected BoundExpression BindInferredVariableInitializer(
        BelteDiagnosticQueue diagnostics,
        ExpressionSyntax initializer,
        BindValueKind valueKind,
        BelteSyntaxNode errorSyntax) {
        if (initializer is null) {
            diagnostics.Push(Error.NoInitOnImplicit(errorSyntax.location));
            return null;
        }

        var value = BindValue(initializer, diagnostics, valueKind);
        var expression = BindToNaturalType(value, diagnostics);

        if (!expression.hasErrors && !expression.HasExpressionType()) {
            // TODO Reachable error?
        }

        return expression;
    }

    private BoundExpression BindArrayDimension(
        ExpressionSyntax dimension,
        BelteDiagnosticQueue diagnostics,
        ref bool hasErrors) {
        if (dimension is null)
            return null;

        return BindValue(dimension, diagnostics, BindValueKind.RValue);
    }

    internal ArrayTypeSymbol CreateArrayTypeSymbol(TypeSymbol elementType, int rank = 1) {
        ArgumentNullException.ThrowIfNull(elementType);

        if (rank < 1)
            throw new ArgumentException(null, nameof(rank));

        return ArrayTypeSymbol.CreateArray(new TypeWithAnnotations(elementType, true), rank);
    }

    internal bool ValidateDeclarationNameConflictsInScope(Symbol symbol, BelteDiagnosticQueue diagnostics) {
        var location = GetLocation(symbol);
        return ValidateNameConflictsInScope(symbol, location, symbol.name, diagnostics);
    }

    private TextLocation GetLocation(Symbol symbol) {
        return symbol.location ?? symbol.containingSymbol.location;
    }

    private bool ValidateNameConflictsInScope(
        Symbol symbol,
        TextLocation location,
        string name,
        BelteDiagnosticQueue diagnostics) {
        if (string.IsNullOrEmpty(name))
            return false;

        for (var binder = this; binder is not null; binder = binder.next) {
            if (binder is InContainerBinder)
                return false;

            var scope = binder as LocalScopeBinder;
            if (scope?.EnsureSingleDefinition(symbol, name, location, diagnostics) == true)
                return true;

            if (binder.isNestedFunctionBinder)
                return false;

            if (binder.IsLastBinderWithinMember())
                return false;
        }

        return false;
    }

    private bool IsLastBinderWithinMember() {
        var containingMember = this.containingMember;
        return (containingMember?.kind) switch {
            null or SymbolKind.NamedType or SymbolKind.Namespace => true,
            _ => containingMember.containingSymbol?.kind == SymbolKind.NamedType &&
                                next?.containingMember != containingMember,
        };
    }

    private TypeWithAnnotations BindVariableTypeWithAnnotations(
        BelteSyntaxNode declarationNode,
        BelteDiagnosticQueue diagnostics,
        TypeSyntax typeSyntax,
        ref bool isConst,
        ref bool isConstExpr,
        out bool isImplicitlyTyped) {
        var declType = BindTypeOrImplicitType(typeSyntax.SkipRef(out _), diagnostics, out isImplicitlyTyped);

        if (!isImplicitlyTyped) {
            if (declType.isStatic)
                diagnostics.Push(Error.StaticDataContainer(declarationNode.location));
        }

        return declType;
    }

    private BoundBlockStatement BindBlockStatement(BlockStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        var binder = GetBinder(node);
        return binder.BindBlockParts(node, diagnostics);
    }

    private BoundBlockStatement BindBlockParts(BlockStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        var syntaxStatements = node.statements;
        var nStatements = syntaxStatements.Count;

        var boundStatements = ArrayBuilder<BoundStatement>.GetInstance(nStatements);

        for (var i = 0; i < nStatements; i++) {
            var boundStatement = BindStatement(syntaxStatements[i], diagnostics);
            boundStatements.Add(boundStatement);
        }

        var locals = GetDeclaredLocalsForScope(node);
        var localFunctions = GetDeclaredLocalFunctionsForScope(node);

        return new BoundBlockStatement(node, boundStatements.ToImmutableAndFree(), locals, localFunctions);
    }

    private BoundReturnStatement BindReturnStatement(ReturnStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        var expressionSyntax = node.expression.UnwrapRefExpression(out var refKind);
        BoundExpression argument = null;

        if (expressionSyntax is not null) {
            var requiredValueKind = GetRequiredReturnValueKind(refKind);
            argument = BindValue(expressionSyntax, diagnostics, requiredValueKind);
        }

        var returnType = GetCurrentReturnType(out var signatureRefKind);
        var hasErrors = false;

        if (returnType is not null && refKind != RefKind.None != (signatureRefKind != RefKind.None)) {
            if (refKind == RefKind.None)
                diagnostics.Push(Error.MustNotHaveRefReturn(node.keyword.location));
            else
                diagnostics.Push(Error.MustHaveRefReturn(node.keyword.location));

            hasErrors = true;
        }

        if (argument is not null)
            hasErrors |= argument.type is not null && argument.type.IsErrorType();

        if (hasErrors)
            return new BoundReturnStatement(node, refKind, argument, true);

        if (returnType is not null) {
            if (returnType.IsVoidType()) {
                if (argument is not null && containingMember is not SynthesizedEntryPoint) {
                    hasErrors = true;
                    diagnostics.Push(Error.UnexpectedReturnValue(node.keyword.location));
                    // TODO confirm this error has enough info, maybe include containingMember?
                }
            } else {
                if (argument is null) {
                    if (containingMember is not SynthesizedEntryPoint) {
                        hasErrors = true;
                        diagnostics.Push(Error.MissingReturnValue(node.keyword.location));
                    }
                } else {
                    argument = CreateReturnConversion(node, diagnostics, argument, signatureRefKind, returnType);
                }
            }
        } else {
            if (argument?.type is not null &&
                argument.type.IsVoidType() &&
                containingMember is not SynthesizedEntryPoint) {
                diagnostics.Push(Error.UnexpectedReturnValue(node.expression.location));
                hasErrors = true;
            }
        }

        return new BoundReturnStatement(
            node,
            refKind,
            hasErrors ? BindToTypeForErrorRecovery(argument) : argument,
            hasErrors
        );
    }

    private BoundExpressionStatement BindExpressionStatement(
        ExpressionStatementSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var expression = BindRValueWithoutTargetType(node.expression, diagnostics);

        if (!compilation.options.isScript) {
            if (expression is not BoundCallExpression
                          and not BoundAssignmentOperator
                          and not BoundErrorExpression
                          and not BoundCompoundAssignmentOperator
                          and not BoundThrowExpression) {
                diagnostics.Push(Error.InvalidExpressionStatement(node.location));
            }
        }

        return new BoundExpressionStatement(node, expression);
    }

    private BindValueKind GetRequiredReturnValueKind(RefKind refKind) {
        var requiredValueKind = BindValueKind.RValue;

        if (refKind != RefKind.None) {
            GetCurrentReturnType(out var signatureRefKind);
            requiredValueKind = signatureRefKind == RefKind.Ref ? BindValueKind.RefReturn : BindValueKind.RefConst;
        }

        return requiredValueKind;
    }

    private protected virtual TypeSymbol GetCurrentReturnType(out RefKind refKind) {
        if (containingMember is MethodSymbol symbol) {
            refKind = symbol.refKind;
            return symbol.returnType;
        }

        refKind = RefKind.None;
        return null;
    }

    internal virtual BoundNode BindMethodBody(BelteSyntaxNode syntax, BelteDiagnosticQueue diagnostics) {
        switch (syntax) {
            case BaseMethodDeclarationSyntax method:
                if (method.kind == SyntaxKind.ConstructorDeclaration)
                    return BindConstructorBody((ConstructorDeclarationSyntax)method, diagnostics);

                return BindMethodBody(method, method.body, diagnostics);
            default:
                throw ExceptionUtilities.UnexpectedValue(syntax.kind);
        }
    }

    private BoundNode BindConstructorBody(ConstructorDeclarationSyntax constructor, BelteDiagnosticQueue diagnostics) {
        var initializer = constructor.constructorInitializer;
        var bodyBinder = GetBinder(constructor);

        var initializerCall = initializer is null
            ? bodyBinder.BindImplicitConstructorInitializer(constructor, diagnostics)
            : bodyBinder.BindConstructorInitializer(initializer, diagnostics);

        var body = (BoundBlockStatement)bodyBinder.BindStatement(constructor.body, diagnostics);
        var locals = bodyBinder.GetDeclaredLocalsForScope(constructor);

        return new BoundConstructorMethodBody(constructor, locals, initializerCall, body);
    }

    private BoundNode BindMethodBody(
        BelteSyntaxNode declaration,
        BlockStatementSyntax body,
        BelteDiagnosticQueue diagnostics) {
        if (body is null)
            return null;

        return new BoundNonConstructorMethodBody(declaration, (BoundBlockStatement)BindStatement(body, diagnostics));
    }

    #endregion

    #region Initializers

    internal static void BindFieldInitializers(
        Compilation compilation,
        ImmutableArray<ImmutableArray<FieldInitializer>> fieldInitializers,
        BelteDiagnosticQueue diagnostics,
        ref ProcessedFieldInitializers processedInitializers) {
        var diagsForInstanceInitializers = BelteDiagnosticQueue.GetInstance();
        processedInitializers.boundInitializers = BindFieldInitializers(
            compilation,
            fieldInitializers,
            diagsForInstanceInitializers
        );

        processedInitializers.hasErrors = diagsForInstanceInitializers.AnyErrors();
        diagnostics.PushRange(diagsForInstanceInitializers);
        diagsForInstanceInitializers.Free();
    }

    internal static ImmutableArray<BoundInitializer> BindFieldInitializers(
        Compilation compilation,
        ImmutableArray<ImmutableArray<FieldInitializer>> initializers,
        BelteDiagnosticQueue diagnostics) {
        if (initializers.IsEmpty)
            return [];

        var boundInitializers = ArrayBuilder<BoundInitializer>.GetInstance();
        BindRegularFieldInitializers(compilation, initializers, boundInitializers, diagnostics);
        return boundInitializers.ToImmutableAndFree();
    }

    internal Binder GetFieldInitializerBinder(
        FieldSymbol fieldSymbol,
        bool suppressBinderFlagsFieldInitializer = false) {
        var binder = this;
        return new LocalScopeBinder(binder).WithAdditionalFlagsAndContainingMember(
            suppressBinderFlagsFieldInitializer ? BinderFlags.None : BinderFlags.FieldInitializer,
            fieldSymbol
        );
    }

    internal static void BindRegularFieldInitializers(
        Compilation compilation,
        ImmutableArray<ImmutableArray<FieldInitializer>> initializers,
        ArrayBuilder<BoundInitializer> boundInitializers,
        BelteDiagnosticQueue diagnostics) {

        foreach (var siblingInitializers in initializers) {
            BinderFactory binderFactory = null;

            foreach (var initializer in siblingInitializers) {
                var fieldSymbol = initializer.field;

                if (!fieldSymbol.isConstExpr) {
                    var syntaxRef = initializer.syntax;

                    switch (syntaxRef.node) {
                        case EqualsValueClauseSyntax initializerNode:
                            binderFactory ??= compilation.GetBinderFactory(syntaxRef.syntaxTree);
                            var parentBinder = binderFactory.GetBinder(initializerNode);
                            parentBinder = parentBinder.GetFieldInitializerBinder(fieldSymbol);

                            var boundInitializer = BindFieldInitializer(
                                parentBinder,
                                fieldSymbol,
                                initializerNode,
                                diagnostics
                            );

                            boundInitializers.Add(boundInitializer);
                            break;
                        default:
                            throw ExceptionUtilities.Unreachable();
                    }
                }
            }
        }
    }

    private static BoundFieldEqualsValue BindFieldInitializer(
        Binder binder,
        FieldSymbol fieldSymbol,
        EqualsValueClauseSyntax equalsValueClauseNode,
        BelteDiagnosticQueue diagnostics) {
        var fieldsBeingBound = binder.fieldsBeingBound;
        var isImplicitlyTypedField = fieldSymbol is SourceMemberFieldSymbolFromDeclarator sourceField &&
            sourceField.FieldTypeInferred(fieldsBeingBound);

        var initializerDiagnostics = isImplicitlyTypedField ? BelteDiagnosticQueue.Discarded : diagnostics;

        binder = new ExecutableCodeBinder(equalsValueClauseNode, fieldSymbol, new LocalScopeBinder(binder));
        var boundInitValue = binder.BindWithLambdaBindingCountDiagnostics(
            equalsValueClauseNode,
            fieldSymbol,
            initializerDiagnostics,
            static (binder, equalsValueClauseNode, fieldSymbol, initializerDiagnostics)
                => binder.BindFieldInitializer(fieldSymbol, equalsValueClauseNode, initializerDiagnostics)
        );

        return boundInitValue;
    }

    internal TResult BindWithLambdaBindingCountDiagnostics<TSyntax, TArg, TResult>(
        TSyntax syntax,
        TArg arg,
        BelteDiagnosticQueue diagnostics,
        Func<Binder, TSyntax, TArg, BelteDiagnosticQueue, TResult> bind)
        where TSyntax : SyntaxNode
        where TResult : BoundNode {
        var bindings = PooledDictionary<SyntaxNode, int>.GetInstance();
        LambdaBindings = bindings;

        try {
            var result = bind(this, syntax, arg, diagnostics);

            foreach (var pair in bindings) {
                const int MaxLambdaBinding = 100;
                var count = pair.Value;

                if (count > MaxLambdaBinding) {
                    // TODO Useless right now
                    throw ExceptionUtilities.Unreachable();
                    // var truncatedToHundreds = count / 100 * 100;
                    // diagnostics.Add(ErrorCode.INF_TooManyBoundLambdas, GetAnonymousFunctionLocation(pair.Key), truncatedToHundreds);
                }
            }

            return result;
        } finally {
            bindings.Free();
            LambdaBindings = null;
        }
    }

    internal BoundExpressionStatement BindImplicitConstructorInitializer(
        SyntaxNode syntax,
        BelteDiagnosticQueue diagnostics) {
        var call = BindImplicitConstructorInitializer((MethodSymbol)containingMember, diagnostics, compilation);

        if (call is null)
            return null;

        return new BoundExpressionStatement(syntax, call);
    }

    internal static BoundExpression BindImplicitConstructorInitializer(
        MethodSymbol constructor,
        BelteDiagnosticQueue diagnostics,
        Compilation compilation) {
        var containingType = constructor.containingType;
        var baseType = containingType.baseType;

        if (containingType.specialType == SpecialType.Object)
            return null;

        if (baseType is not null) {
            if (baseType.specialType == SpecialType.Object)
                return GenerateBaseParameterlessConstructorInitializer(constructor, diagnostics);
            else if (baseType.IsErrorType() || baseType.isStatic)
                return null;
        }

        if (containingType.IsStructType())
            return null;

        Binder outerBinder;

        if (constructor is not SourceMemberMethodSymbol sourceConstructor) {
            var containerNode = constructor.GetNonNullSyntaxNode();

            if (containerNode is CompilationUnitSyntax)
                containerNode = containingType.syntaxReference.node as TypeDeclarationSyntax;

            var binderFactory = compilation.GetBinderFactory(containerNode.syntaxTree);
            outerBinder = binderFactory.GetInTypeBodyBinder((TypeDeclarationSyntax)containerNode);
        } else {
            var binderFactory = compilation.GetBinderFactory(sourceConstructor.syntaxTree);

            outerBinder = sourceConstructor.syntaxNode switch {
                ConstructorDeclarationSyntax ctorDecl => binderFactory.GetBinder(ctorDecl.parameterList),
                TypeDeclarationSyntax typeDecl => binderFactory.GetInTypeBodyBinder(typeDecl),
                _ => throw ExceptionUtilities.Unreachable(),
            };
        }

        var initializersBinder = outerBinder.WithAdditionalFlagsAndContainingMember(
            BinderFlags.ConstructorInitializer,
            constructor
        );

        return initializersBinder.BindConstructorInitializer(null, constructor, diagnostics);
    }

    internal virtual BoundExpressionStatement BindConstructorInitializer(
        ConstructorInitializerSyntax initializer,
        BelteDiagnosticQueue diagnostics) {
        var call = GetBinder(initializer)
            .BindConstructorInitializer(initializer.argumentList, (MethodSymbol)containingMember, diagnostics);

        return new BoundExpressionStatement(initializer, call);
    }

    #endregion

    #region Conversions

    internal BoundExpression CreateReturnConversion(
        SyntaxNode node,
        BelteDiagnosticQueue diagnostics,
        BoundExpression argument,
        RefKind returnRefKind,
        TypeSymbol returnType) {
        var conversion = conversions.ClassifyConversionFromExpression(argument, returnType);

        if (!argument.hasErrors) {
            if (returnRefKind != RefKind.None) {
                if (conversion.kind != ConversionKind.Identity) {
                    // TODO ref return must have identity conversion?
                } else {
                    return BindToNaturalType(argument, diagnostics);
                }
            }
        }

        return CreateConversion(node, argument, conversion, isCast: false, returnType, diagnostics);
    }

    internal BoundExpression GenerateConversionForAssignment(
        TypeSymbol targetType,
        BoundExpression expression,
        BelteDiagnosticQueue diagnostics,
        ConversionForAssignmentFlags flags = ConversionForAssignmentFlags.None) {
        return GenerateConversionForAssignment(targetType, expression, diagnostics, out _, flags);
    }

    internal BoundExpression GenerateConversionForAssignment(
        TypeSymbol targetType,
        BoundExpression expression,
        BelteDiagnosticQueue diagnostics,
        out Conversion conversion,
        ConversionForAssignmentFlags flags = ConversionForAssignmentFlags.None) {
        if (expression.hasErrors)
            diagnostics = BelteDiagnosticQueue.Discarded;

        conversion = (flags & ConversionForAssignmentFlags.IncrementAssignment) == 0
            ? conversions.ClassifyConversionFromExpression(expression, targetType)
            : conversions.ClassifyConversionFromType(expression.type, targetType);

        if ((flags & ConversionForAssignmentFlags.RefAssignment) != 0) {
            if (conversion.kind != ConversionKind.Identity) {
                // Error(diagnostics, ErrorCode.ERR_RefAssignmentMustHaveIdentityConversion, expression.Syntax, targetType);
                // TODO ref assignment must have identity conversion?
            } else {
                return expression;
            }
        } else if (!conversion.exists ||
              ((flags & ConversionForAssignmentFlags.CompoundAssignment) == 0
                ? !conversion.isImplicit
                : (conversion.isExplicit && ((flags & ConversionForAssignmentFlags.PredefinedOperator) == 0)))) {
            if ((flags & ConversionForAssignmentFlags.DefaultParameter) == 0)
                GenerateImplicitConversionError(diagnostics, expression.syntax, conversion, expression, targetType);

            diagnostics = BelteDiagnosticQueue.Discarded;
        }

        return CreateConversion(expression.syntax, expression, conversion, false, targetType, diagnostics);
    }

    private protected void GenerateImplicitConversionError(
        BelteDiagnosticQueue diagnostics,
        SyntaxNode syntax,
        Conversion conversion,
        BoundExpression operand,
        TypeSymbol targetType) {
        if (targetType.typeKind == TypeKind.Error)
            return;

        if (targetType.IsVoidType()) {
            diagnostics.Push(Error.NoImplicitConversion(syntax.location, operand.type, targetType));
            return;
        }

        switch (operand.kind) {
            case BoundKind.ErrorExpression:
                return;
            case BoundKind.MethodGroup:
                diagnostics.Push(Error.MethodGroupCannotBeUsedAsValue(syntax.location, (BoundMethodGroup)operand));
                return;
            case BoundKind.LiteralExpression:
                if (ConstantValue.IsNull(operand.constantValue)) {
                    if (!targetType.IsNullableType()) {
                        diagnostics.Push(Error.ValueCannotBeNull(syntax.location, targetType));
                        return;
                    }
                }

                break;
            case BoundKind.ConditionalOperator: {
                    var conditionalOperator = (BoundConditionalOperator)operand;
                    var reportedError = false;
                    TryConversion(conditionalOperator.trueExpression, ref reportedError);
                    TryConversion(conditionalOperator.falseExpression, ref reportedError);
                    return;
                }

                void TryConversion(BoundExpression expr, ref bool reportedError) {
                    var conversion = conversions.ClassifyImplicitConversionFromExpression(expr, targetType);

                    if (!conversion.isImplicit || !conversion.exists) {
                        GenerateImplicitConversionError(diagnostics, syntax, conversion, expr, targetType);
                        reportedError = true;
                    }
                }
            case BoundKind.UnconvertedInitializerList:
                GenerateImplicitConversionErrorForList(
                    (BoundUnconvertedInitializerList)operand,
                    targetType,
                    diagnostics
                );

                return;
        }

        var sourceType = operand.type;

        if (sourceType is not null) {
            GenerateImplicitConversionError(
                diagnostics,
                syntax,
                conversion,
                sourceType,
                targetType,
                operand.constantValue
            );

            return;
        }
    }

    internal void GenerateImplicitConversionErrorForList(
        BoundUnconvertedInitializerList node,
        TypeSymbol targetType,
        BelteDiagnosticQueue diagnostics) {
        var listTypeKind = Conversions.GetListExpressionTypeKind(targetType, out var elementTypeWithAnnotations);
        var reportedErrors = false;

        if (listTypeKind != ListExpressionTypeKind.None) {
            var items = node.items;
            var elementType = elementTypeWithAnnotations.type;

            foreach (var item in items) {
                var elementConversion = conversions.ClassifyImplicitConversionFromExpression(item, elementType);

                if (!elementConversion.exists) {
                    GenerateImplicitConversionError(
                        diagnostics,
                        item.syntax,
                        elementConversion,
                        (BoundExpression)item,
                        elementType
                    );

                    reportedErrors = true;
                }
            }
        }

        if (!reportedErrors) {
            // TODO What is this error
            // Error(diagnostics, ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, node.Syntax, targetType);
        }

        return;
    }

    private protected static void GenerateImplicitConversionError(
        BelteDiagnosticQueue diagnostics,
        SyntaxNode syntax,
        Conversion conversion,
        TypeSymbol sourceType,
        TypeSymbol targetType,
        ConstantValue sourceConstantValueOpt = null) {
        if (!sourceType.ContainsErrorType() && !targetType.ContainsErrorType()) {
            if (conversion.kind == ConversionKind.ExplicitNullable &&
                conversion.underlyingConversions != default &&
                conversion.underlyingConversions.FirstOrDefault().isIdentity) {
                diagnostics.Push(Error.CannotConvertImplicitlyNullable(syntax.location, sourceType, targetType));
            } else if (conversion.isExplicit) {
                diagnostics.Push(Error.CannotConvertImplicitly(syntax.location, sourceType, targetType));
            } else {
                diagnostics.Push(Error.CannotConvert(syntax.location, sourceType, targetType));
            }
        }
    }

    internal BoundExpression CreateConversion(
        SyntaxNode node,
        BoundExpression source,
        Conversion conversion,
        bool isCast,
        TypeSymbol destination,
        BelteDiagnosticQueue diagnostics,
        bool hasErrors = false) {
        if (conversion.isIdentity) {
            source = BindToNaturalType(source, diagnostics);

            if (!isCast &&
                (source.IsLiteralNull() || source.type.Equals(destination, TypeCompareKind.IgnoreNullability))) {
                return source;
            }
        }

        // TODO method group conversion
        // if (conversion.isMethodGroup)
        //     return CreateMethodGroupConversion(node, source, conversion, isCast, destination, diagnostics);

        if (source.kind == BoundKind.UnconvertedInitializerList) {
            var listExpression = ConvertListExpression(
                (BoundUnconvertedInitializerList)source,
                destination,
                conversion,
                diagnostics
            );

            return new BoundCastExpression(
                node,
                listExpression,
                conversion,
                null,
                destination
            );
        }

        var constantValue = ConstantFolding.FoldCast(source, new TypeWithAnnotations(destination));

        return new BoundCastExpression(
            node,
            BindToNaturalType(source, diagnostics),
            conversion,
            constantValue,
            destination,
            hasErrors
        );
    }

    internal BoundExpression CreateConversion(
        BoundExpression source,
        Conversion conversion,
        TypeSymbol destination,
        BelteDiagnosticQueue diagnostics) {
        return CreateConversion(
            source.syntax,
            source,
            conversion,
            isCast: false,
            destination: destination,
            diagnostics: diagnostics
        );
    }

    private BoundInitializerList ConvertListExpression(
        BoundUnconvertedInitializerList node,
        TypeSymbol targetType,
        Conversion conversion,
        BelteDiagnosticQueue diagnostics) {
        if (conversion.isNullable) {
            targetType = targetType.GetNullableUnderlyingType();
            conversion = conversion.underlyingConversions[0];
            // TODO What is the purpose of this
            // _ = GetSpecialTypeMember(SpecialMember.System_Nullable_T__ctor, diagnostics, syntax: node.Syntax);
        }

        var listTypeKind = conversion.GetListExpressionTypeKind(out var elementType);

        if (listTypeKind == ListExpressionTypeKind.None)
            return BindListForErrorRecovery(node, targetType, diagnostics);

        var syntax = node.syntax;

        var items = node.items;
        var builder = ArrayBuilder<BoundExpression>.GetInstance(items.Length);
        var elementConversions = conversion.underlyingConversions;

        for (var i = 0; i < items.Length; i++) {
            var element = items[i];
            var elementConversion = elementConversions[i];
            var convertedElement = CreateConversion(
                element.syntax,
                element,
                elementConversion,
                isCast: false,
                destination: elementType,
                diagnostics
            );

            builder.Add(convertedElement!);
        }

        // TODO Eventually include this extra data when List<T> is added
        // return new BoundInitializerList(
        //     syntax,
        //     collectionTypeKind,
        //     implicitReceiver,
        //     collectionCreation,
        //     collectionBuilderMethod,
        //     collectionBuilderInvocationPlaceholder,
        //     collectionBuilderInvocationConversion,
        //     wasTargetTyped: true,
        //     node,
        //     builder.ToImmutableAndFree(),
        //     targetType);
        return new BoundInitializerList(syntax, builder.ToImmutableAndFree(), targetType);
    }

    #endregion
}

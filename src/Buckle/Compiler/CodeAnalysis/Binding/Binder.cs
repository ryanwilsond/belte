using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Lowering;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;
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

    internal const int MaxParameterListsForErrorRecovery = 10;

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

    internal virtual SynthesizedDataContainerSymbol commitLocal => next.commitLocal;

    internal virtual bool inMethod => next.inMethod;

    internal virtual DataContainerSymbol localInProgress => next.localInProgress;

    internal virtual ConstantFieldsInProgress constantFieldsInProgress => next.constantFieldsInProgress;

    internal virtual BoundExpression conditionalReceiverExpression => next.conditionalReceiverExpression;

    internal virtual ConsList<FieldSymbol> fieldsBeingBound => next.fieldsBeingBound;

    internal virtual ImmutableArray<DataContainerSymbol> locals => [];

    internal virtual ImmutableArray<LocalFunctionSymbol> localFunctions => [];

    internal virtual ImmutableArray<AliasAndUsingDirective> usingAliases => [];

    internal virtual ImmutableArray<LabelSymbol> labels => [];

    internal virtual ImmutableArray<TokenSymbol> tokens => [];

    internal virtual bool isInMethodBody => next.isInMethodBody;

    internal virtual bool isNestedFunctionBinder => false;

    internal virtual bool isInsideNameof => next.isInsideNameof;

    internal virtual QuickAttributeChecker quickAttributeChecker => next.quickAttributeChecker;

    internal virtual ImportChain importChain => next.importChain;

    internal NamedTypeSymbol containingType => containingMember switch {
        null => null,
        NamedTypeSymbol namedType => namedType,
        _ => containingMember.containingType
    };

    internal bool isEarlyAttributeBinder => flags.Includes(BinderFlags.EarlyAttributeBinding);

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

    internal virtual ImmutableArray<TokenSymbol> GetDeclaredTokensForScope(SyntaxNode scopeDesignator) {
        return next.GetDeclaredTokensForScope(scopeDesignator);
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

    private protected virtual SourceTokenSymbol LookupToken(SyntaxToken identifier) {
        return next.LookupToken(identifier);
    }

    private protected virtual SynthesizedDataContainerSymbol BuildWithCommit() {
        return next.BuildWithCommit();
    }

    private protected virtual bool IsUnboundTypeAllowed(TemplateNameSyntax syntax) {
        return next.IsUnboundTypeAllowed(syntax);
    }

    private bool IsSymbolAccessible(Symbol symbol, NamedTypeSymbol within, TypeSymbol throughType = null) {
        return flags.Includes(BinderFlags.IgnoreAccessibility) ||
            AccessCheck.IsSymbolAccessible(symbol, within, throughType);
    }

    private protected void MarkImportDirective(SyntaxReference directive) {
        if (directive is not null)
            compilation.MarkImportDirectiveAsUsed(directive);
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
        var basesBeingResolved = ConsList<TypeSymbol>.Empty;

        foreach (var templateParameter in templateParameters) {
            var name = templateParameter.name;
            basesBeingResolved = basesBeingResolved.Prepend(templateParameter);

            if (!names.ContainsKey(name))
                names.Add(name, names.Count);
        }

        var results = ArrayBuilder<TypeParameterConstraintClause>.GetInstance(n, fillWithValue: null);
        var syntaxNodes = ArrayBuilder<ArrayBuilder<TemplateConstraintClauseSyntax>>
            .GetInstance(n, fillWithValue: null);

        foreach (var clause in clauses) {
            if (clause.expressionConstraint is not null) {
                var syntax = clause.expressionConstraint.expression;
                results.Add(TypeParameterConstraintClause.Create(syntax));
                continue;
            }

            var name = clause.extendConstraint is not null
                ? clause.extendConstraint.name.identifier
                : clause.isConstraint is not null
                    ? clause.isConstraint.name.identifier
                    : clause.hasConstraint.name.identifier;

            if (names.TryGetValue(name.valueText, out var ordinal)) {
                if (syntaxNodes[ordinal] is null)
                    syntaxNodes[ordinal] = ArrayBuilder<TemplateConstraintClauseSyntax>.GetInstance();

                syntaxNodes[ordinal].Add(clause);
            } else {
                diagnostics.Push(Error.UnknownTemplate(name.location, containingSymbol.name, name.valueText));
            }
        }

        foreach (var parameter in templateParameters) {
            names.TryGetValue(parameter.name, out var ordinal);

            if (syntaxNodes[ordinal] is not null) {
                var constraintClause = BindTypeParameterConstraints(
                    templateParameters[ordinal],
                    syntaxNodes[ordinal],
                    basesBeingResolved,
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

    internal ImmutableArray<BoundExpression> BindExpressionConstraints(
        ImmutableArray<ExpressionSyntax> constraints,
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        BelteDiagnosticQueue diagnostics) {
        var builder = ArrayBuilder<BoundExpression>.GetInstance();
        var targetType = CorLibrary.GetNullableType(SpecialType.Bool);

        foreach (var constraint in constraints) {
            var expression = BindExpression(constraint, diagnostics);

            if (!EnsureExpressionIsCompileTime(expression, templateParameters))
                diagnostics.Push(Error.ConstraintIsNotConstant(constraint.location));

            var conversion = conversions.ClassifyImplicitConversionFromExpression(expression, targetType);

            if (!conversion.exists)
                GenerateImplicitConversionError(diagnostics, expression.syntax, conversion, expression, targetType);

            expression = CreateConversion(expression, conversion, targetType, diagnostics);
            builder.Add(expression);
        }

        return builder.ToImmutableAndFree();
    }

    private bool EnsureExpressionIsCompileTime(
        BoundExpression expression,
        ImmutableArray<TemplateParameterSymbol> templateParameters) {
        if (expression.constantValue is not null)
            return true;

        switch (expression.kind) {
            case BoundKind.UnaryOperator:
                return EnsureExpressionIsCompileTime(((BoundUnaryOperator)expression).operand, templateParameters);
            case BoundKind.BinaryOperator:
                var binary = (BoundBinaryOperator)expression;
                return EnsureExpressionIsCompileTime(binary.left, templateParameters) &&
                       EnsureExpressionIsCompileTime(binary.right, templateParameters);
            case BoundKind.IsOperator:
                return EnsureExpressionIsCompileTime(((BoundIsOperator)expression).left, templateParameters);
            case BoundKind.NullCoalescingOperator:
                var nullCoalescing = (BoundNullCoalescingOperator)expression;
                return EnsureExpressionIsCompileTime(nullCoalescing.left, templateParameters) &&
                       EnsureExpressionIsCompileTime(nullCoalescing.right, templateParameters);
            case BoundKind.NullAssertOperator:
                return EnsureExpressionIsCompileTime(((BoundNullAssertOperator)expression).operand, templateParameters);
            case BoundKind.CastExpression:
                return EnsureExpressionIsCompileTime(((BoundCastExpression)expression).operand, templateParameters);
            case BoundKind.ConditionalOperator:
                var conditional = (BoundConditionalOperator)expression;
                return EnsureExpressionIsCompileTime(conditional.condition, templateParameters) &&
                       EnsureExpressionIsCompileTime(conditional.trueExpression, templateParameters) &&
                       EnsureExpressionIsCompileTime(conditional.falseExpression, templateParameters);
            case BoundKind.TypeExpression:
                return templateParameters.Contains(expression.type);
            default:
                return false;
        }
    }

    private TypeParameterConstraintClause BindTypeParameterConstraints(
        TemplateParameterSymbol templateParameter,
        ArrayBuilder<TemplateConstraintClauseSyntax> constraintsSyntax,
        ConsList<TypeSymbol> basesBeingResolved,
        BelteDiagnosticQueue diagnostics) {
        var constraints = TypeParameterConstraintKinds.None;
        var constraintTypes = ArrayBuilder<TypeWithAnnotations>.GetInstance();

        for (int i = 0, n = constraintsSyntax.Count; i < n; i++) {
            var syntax = constraintsSyntax[i];

            if (syntax.extendConstraint is not null) {
                var typeSyntax = syntax.extendConstraint.type;
                var type = BindType(typeSyntax, diagnostics, basesBeingResolved);

                if (type.type.StrippedType().specialType.IsPrimitiveType())
                    diagnostics.Push(Error.CannotDerivePrimitive(typeSyntax.location, type.type.StrippedType()));
                else
                    constraintTypes.Add(new TypeWithAnnotations(type.nullableUnderlyingTypeOrSelf));
            } else if (syntax.isConstraint is not null) {
                switch (syntax.isConstraint.keyword.kind) {
                    case SyntaxKind.PrimitiveKeyword:
                        if ((constraints & TypeParameterConstraintKinds.Primitive) == 0)
                            constraints |= TypeParameterConstraintKinds.Primitive;
                        else
                            diagnostics.Push(Error.DuplicateConstraint(syntax.location, templateParameter.name));

                        continue;
                    case SyntaxKind.NotnullKeyword:
                        if ((constraints & TypeParameterConstraintKinds.NotNull) == 0)
                            constraints |= TypeParameterConstraintKinds.NotNull;
                        else
                            diagnostics.Push(Error.DuplicateConstraint(syntax.location, templateParameter.name));

                        continue;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(syntax.isConstraint.keyword.kind);
                }
            } else if (syntax.hasConstraint is not null) {
                switch (syntax.hasConstraint.keyword.kind) {
                    case SyntaxKind.DefaultKeyword:
                        if ((constraints & TypeParameterConstraintKinds.Default) == 0)
                            constraints |= TypeParameterConstraintKinds.Default;
                        else
                            diagnostics.Push(Error.DuplicateConstraint(syntax.location, templateParameter.name));

                        continue;
                    case SyntaxKind.ConstexprKeyword:
                        if ((constraints & TypeParameterConstraintKinds.Constructor) == 0)
                            constraints |= TypeParameterConstraintKinds.Constructor;
                        else
                            diagnostics.Push(Error.DuplicateConstraint(syntax.location, templateParameter.name));

                        continue;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(syntax.isConstraint.keyword.kind);
                }
            }
        }

        return TypeParameterConstraintClause.Create(constraints, constraintTypes.ToImmutableAndFree());
    }

    #endregion

    #region Symbols

    internal BoundExpression BindNamespaceOrType(ExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var symbol = BindNamespaceOrTypeOrAliasSymbol(node, diagnostics, null);
        return CreateBoundNamespaceOrTypeExpression(node, symbol.symbol);
    }

    private static BoundExpression CreateBoundNamespaceOrTypeExpression(ExpressionSyntax node, Symbol symbol) {
        var alias = symbol as AliasSymbol;

        if (alias is not null)
            symbol = alias.target;

        var type = symbol as TypeSymbol;

        if (type is not null)
            return new BoundTypeExpression(node, new TypeWithAnnotations(type), alias, type);

        if (symbol is NamespaceSymbol namespaceSymbol)
            return new BoundNamespaceExpression(node, namespaceSymbol, alias);

        throw ExceptionUtilities.UnexpectedValue(symbol);
    }

    internal NamespaceOrTypeOrAliasSymbolWithAnnotations BindNamespaceOrTypeSymbol(
        ExpressionSyntax syntax,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved = null) {
        var result = BindNamespaceOrTypeOrAliasSymbol(syntax, diagnostics, basesBeingResolved);
        return UnwrapAlias(result, diagnostics, syntax, basesBeingResolved);
    }

    internal TypeWithAnnotations BindType(
        ExpressionSyntax syntax,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved = null) {
        var symbol = BindTypeOrAlias(syntax, diagnostics, basesBeingResolved);
        return UnwrapAlias(symbol, diagnostics, syntax, basesBeingResolved).typeWithAnnotations;
    }

    internal TypeWithAnnotations BindTypeWithoutBufferRewrite(
        ExpressionSyntax syntax,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved = null) {
        var symbol = BindTypeOrAlias(syntax, diagnostics, basesBeingResolved, false);
        return UnwrapAlias(symbol, diagnostics, syntax, basesBeingResolved).typeWithAnnotations;
    }

    internal TypeWithAnnotations BindType(
        ExpressionSyntax syntax,
        BelteDiagnosticQueue diagnostics,
        out AliasSymbol alias,
        ConsList<TypeSymbol> basesBeingResolved = null) {
        var symbol = BindTypeOrAlias(syntax, diagnostics, basesBeingResolved);
        return UnwrapAlias(symbol, out alias, diagnostics, syntax, basesBeingResolved).typeWithAnnotations;
    }

    internal NamespaceOrTypeOrAliasSymbolWithAnnotations BindTypeOrAlias(
        ExpressionSyntax syntax,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved = null,
        bool rewriteBufferType = true) {
        var symbol = BindNamespaceOrTypeOrAliasSymbol(syntax, diagnostics, basesBeingResolved, rewriteBufferType);

        if (symbol.isType ||
            (symbol.isAlias && UnwrapAliasNoDiagnostics(symbol.symbol, basesBeingResolved) is TypeSymbol)) {
            return symbol;
        }

        var error = Error.BadSKKnown(
            syntax.location,
            symbol.symbol,
            symbol.symbol.kind.Localize(),
            MessageID.IDS_SK_TYPE.Localize()
        );

        diagnostics.Push(error);

        return new TypeWithAnnotations(
            new ExtendedErrorTypeSymbol(
                GetContainingNamespaceOrType(symbol.symbol),
                symbol.symbol,
                LookupResultKind.NotATypeOrNamespace,
                error
            )
        );
    }

    internal NamespaceOrTypeOrAliasSymbolWithAnnotations BindNamespaceOrTypeOrAliasSymbol(
        ExpressionSyntax syntax,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved = null,
        bool rewriteBufferType = true) {
        NamespaceOrTypeOrAliasSymbolWithAnnotations namespaceOrNonNullableType;

        switch (syntax.kind) {
            case SyntaxKind.NonNullableType:
                return BindNonNullable();
            case SyntaxKind.NullableType:
                return BindNullable();
            case SyntaxKind.IdentifierName:
                namespaceOrNonNullableType = BindNonTemplateSimpleNamespaceOrTypeOrAliasSymbol(
                    (IdentifierNameSyntax)syntax,
                    diagnostics,
                    basesBeingResolved,
                    null
                );

                break;
            case SyntaxKind.TemplateName:
                namespaceOrNonNullableType = BindTemplateSimpleNamespaceOrTypeOrAliasSymbol(
                    (TemplateNameSyntax)syntax,
                    diagnostics,
                    basesBeingResolved,
                    null
                );

                break;
            case SyntaxKind.AliasQualifiedName:
                namespaceOrNonNullableType = BindAlias();
                break;
            case SyntaxKind.QualifiedName: {
                    var node = (QualifiedNameSyntax)syntax;

                    namespaceOrNonNullableType = BindQualifiedName(
                        node.left,
                        node.right,
                        diagnostics,
                        basesBeingResolved
                    );

                    break;
                }
            case SyntaxKind.MemberAccessExpression: {
                    var node = (MemberAccessExpressionSyntax)syntax;

                    namespaceOrNonNullableType = BindQualifiedName(
                        node.expression,
                        node.name,
                        diagnostics,
                        basesBeingResolved
                    );

                    break;
                }
            case SyntaxKind.ArrayType:
                namespaceOrNonNullableType = BindArrayType(
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
            case SyntaxKind.TupleType:
                return new TypeWithAnnotations(BindTupleType(
                    (TupleTypeSyntax)syntax,
                    diagnostics,
                    basesBeingResolved
                ));
            case SyntaxKind.PointerType: {
                    var node = (PointerTypeSyntax)syntax;
                    var elementType = BindType(node.elementType, diagnostics, basesBeingResolved);
                    return new TypeWithAnnotations(new PointerTypeSymbol(elementType));
                }
            case SyntaxKind.FunctionPointerType:
                var functionPointerTypeSyntax = (FunctionPointerSyntax)syntax;

                return new TypeWithAnnotations(
                    FunctionPointerTypeSymbol.CreateFromSource(
                        functionPointerTypeSyntax,
                        this,
                        diagnostics,
                        basesBeingResolved
                    )
                );
            case SyntaxKind.FunctionType:
                namespaceOrNonNullableType = new TypeWithAnnotations(
                    FunctionTypeSymbol.CreateFromSource(
                        (FunctionTypeSyntax)syntax,
                        this,
                        diagnostics,
                        basesBeingResolved
                    )
                );

                break;
            default:
                return new TypeWithAnnotations(CreateErrorType());
        }

        if (namespaceOrNonNullableType.isType && rewriteBufferType)
            return new TypeWithAnnotations(RewriteBufferType(namespaceOrNonNullableType.typeWithAnnotations.type));

        return namespaceOrNonNullableType;

        TypeWithAnnotations BindNonNullable() {
            var nonNullableSyntax = (NonNullableTypeSyntax)syntax;
            var nullableType = BindType(nonNullableSyntax.type, diagnostics, basesBeingResolved);

            if (nullableType.type is TemplateParameterSymbol t &&
                (basesBeingResolved is null || !basesBeingResolved.Contains(t))) {
                diagnostics.Push(Error.CannotAnnotateTemplate(syntax.location));
                // Returning the nullable type tends to improve other diagnostic reporting
                return nullableType.SetIsAnnotated();
            }

            return new TypeWithAnnotations(nullableType.type.StrippedType(), false);
        }

        TypeWithAnnotations BindNullable() {
            var nullableSyntax = (NullableTypeSyntax)syntax;
            var underlyingType = BindType(nullableSyntax.type, diagnostics, basesBeingResolved);

            if (underlyingType.IsNullableType())
                return underlyingType;

            if (underlyingType.type.IsPointerOrFunctionPointer()) {
                diagnostics.Push(Error.CannotAnnotatePointer(syntax.location));
                return underlyingType;
            }

            return underlyingType.SetIsAnnotated();
        }

        NamespaceOrTypeOrAliasSymbolWithAnnotations BindAlias() {
            var node = (AliasQualifiedNameSyntax)syntax;
            var bindingResult = BindNamespaceAliasSymbol(node.alias, diagnostics);
            var left = bindingResult is AliasSymbol alias ? alias.target : (NamespaceOrTypeSymbol)bindingResult;

            if (left.kind == SymbolKind.NamedType) {
                var error = Error.ColonColonWithTypeAlias(node.alias.location, node.alias.identifier.valueText);
                diagnostics.Push(error);

                return new TypeWithAnnotations(
                    new ExtendedErrorTypeSymbol(
                        left,
                        LookupResultKind.NotATypeOrNamespace,
                        error
                    )
                );
            }

            return BindSimpleNamespaceOrTypeOrAliasSymbol(node.name, diagnostics, basesBeingResolved, left);
        }
    }

    private TypeSymbol RewriteBufferType(TypeSymbol type) {
        if (type.originalDefinition.specialType == SpecialType.Buffer)
            return ArrayTypeSymbol.CreateSZArray(((NamedTypeSymbol)type).templateArguments[0].type);

        return type;
    }

    private TypeSymbol BindTupleType(
        TupleTypeSyntax syntax,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved) {
        var numElements = syntax.elements.Count;
        var types = ArrayBuilder<TypeWithAnnotations>.GetInstance(numElements);
        var locations = ArrayBuilder<TextLocation>.GetInstance(numElements);
        ArrayBuilder<string> elementNames = null;

        var uniqueFieldNames = PooledHashSet<string>.GetInstance();

        for (var i = 0; i < numElements; i++) {
            var argumentSyntax = syntax.elements[i];

            var argumentType = BindType(argumentSyntax.type, diagnostics, basesBeingResolved);
            types.Add(argumentType);

            string name = null;
            var nameToken = argumentSyntax.identifier;

            if (nameToken is not null) {
                name = nameToken.valueText;
                CheckTupleMemberName(name, i, nameToken, diagnostics, uniqueFieldNames);
                locations.Add(nameToken.location);
            } else {
                locations.Add(argumentSyntax.location);
            }

            CollectTupleFieldMemberName(name, i, numElements, ref elementNames);
        }

        uniqueFieldNames.Free();

        var typesArray = types.ToImmutableAndFree();
        var locationsArray = locations.ToImmutableAndFree();

        if (typesArray.Length < 2)
            throw ExceptionUtilities.UnexpectedValue(typesArray.Length);

        return NamedTypeSymbol.CreateTuple(
            syntax.location,
            typesArray,
            locationsArray,
            elementNames is null ? default : elementNames.ToImmutableAndFree(),
            compilation,
            // TODO What should this be?
            // this.shouldCheckConstraints,
            false,
            errorPositions: default,
            syntax: syntax,
            diagnostics: diagnostics
        );
    }

    private static bool CheckTupleMemberName(
        string name,
        int index,
        SyntaxNodeOrToken syntax,
        BelteDiagnosticQueue diagnostics,
        PooledHashSet<string> uniqueFieldNames) {
        var reserved = NamedTypeSymbol.IsTupleElementNameReserved(name);

        if (reserved == 0) {
            diagnostics.Push(Error.TupleReservedElementNameAnyPosition(syntax.location, name));
            return false;
        } else if (reserved > 0 && reserved != index + 1) {
            diagnostics.Push(Error.TupleReservedElementName(syntax.location, name, reserved));
            return false;
        } else if (!uniqueFieldNames.Add(name)) {
            diagnostics.Push(Error.TupleDuplicateElementName(syntax.location, name));
            return false;
        }

        return true;
    }

    private static void CollectTupleFieldMemberName(
        string name,
        int elementIndex,
        int tupleSize,
        ref ArrayBuilder<string> elementNames) {
        if (elementNames is not null) {
            elementNames.Add(name);
        } else {
            if (name is not null) {
                elementNames = ArrayBuilder<string>.GetInstance(tupleSize);

                for (var j = 0; j < elementIndex; j++)
                    elementNames.Add(null);

                elementNames.Add(name);
            }
        }
    }

    internal Symbol BindNamespaceAliasSymbol(IdentifierNameSyntax node, BelteDiagnosticQueue diagnostics) {
        if (node.identifier.kind == SyntaxKind.GlobalKeyword) {
            return compilation.globalNamespaceAlias;
        } else {
            var plainName = node.identifier.valueText;
            var result = LookupResult.GetInstance();
            LookupSymbolsWithFallback(result, plainName, 0, node.location, null, LookupOptions.NamespaceAliasesOnly);

            var bindingResult = ResultSymbol(
                result,
                plainName,
                0,
                node,
                diagnostics,
                out _,
                null,
                LookupOptions.NamespaceAliasesOnly
            );

            result.Free();

            return bindingResult;
        }
    }

    private NamespaceOrTypeOrAliasSymbolWithAnnotations BindSimpleNamespaceOrTypeOrAliasSymbol(
        SimpleNameSyntax syntax,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved,
        NamespaceOrTypeSymbol qualifierOpt = null) {
        switch (syntax.kind) {
            case SyntaxKind.IdentifierName:
                return BindNonTemplateSimpleNamespaceOrTypeOrAliasSymbol(
                    (IdentifierNameSyntax)syntax,
                    diagnostics,
                    basesBeingResolved,
                    qualifierOpt
                );
            case SyntaxKind.TemplateName:
                return BindTemplateSimpleNamespaceOrTypeOrAliasSymbol(
                    (TemplateNameSyntax)syntax,
                    diagnostics,
                    basesBeingResolved,
                    qualifierOpt
                );
            default:
                return new TypeWithAnnotations(
                    new ExtendedErrorTypeSymbol(
                        qualifierOpt ?? compilation.globalNamespaceInternal,
                        "",
                        arity: 0,
                        error: null
                    )
                );
        }
    }

    private static Symbol UnwrapAliasNoDiagnostics(Symbol symbol, ConsList<TypeSymbol> basesBeingResolved = null) {
        if (symbol.kind == SymbolKind.Alias)
            return ((AliasSymbol)symbol).GetAliasTarget(basesBeingResolved);

        return symbol;
    }

    private Symbol UnwrapAlias(
        Symbol symbol,
        BelteDiagnosticQueue diagnostics,
        SyntaxNode syntax,
        ConsList<TypeSymbol> basesBeingResolved = null) {
        return UnwrapAlias(symbol, out _, diagnostics, syntax, basesBeingResolved);
    }

    private NamespaceOrTypeOrAliasSymbolWithAnnotations UnwrapAlias(
        in NamespaceOrTypeOrAliasSymbolWithAnnotations symbol,
        BelteDiagnosticQueue diagnostics,
        SyntaxNode syntax,
        ConsList<TypeSymbol> basesBeingResolved = null) {
        if (symbol.isAlias) {
            return NamespaceOrTypeOrAliasSymbolWithAnnotations.CreateUnannotated(
                symbol.isNullable,
                (NamespaceOrTypeSymbol)UnwrapAlias(
                    symbol.symbol,
                    out _,
                    diagnostics,
                    syntax,
                    basesBeingResolved
                )
            );
        }

        return symbol;
    }

    private NamespaceOrTypeOrAliasSymbolWithAnnotations UnwrapAlias(
        in NamespaceOrTypeOrAliasSymbolWithAnnotations symbol,
        out AliasSymbol alias,
        BelteDiagnosticQueue diagnostics,
        SyntaxNode syntax,
        ConsList<TypeSymbol> basesBeingResolved = null) {
        if (symbol.isAlias) {
            return NamespaceOrTypeOrAliasSymbolWithAnnotations.CreateUnannotated(
                symbol.isNullable,
                (NamespaceOrTypeSymbol)UnwrapAlias(symbol.symbol, out alias, diagnostics, syntax, basesBeingResolved)
            );
        }

        alias = null;
        return symbol;
    }

    private Symbol UnwrapAlias(
        Symbol symbol,
        out AliasSymbol alias,
        BelteDiagnosticQueue diagnostics,
        SyntaxNode syntax,
        ConsList<TypeSymbol> basesBeingResolved = null) {
        if (symbol.kind == SymbolKind.Alias) {
            alias = (AliasSymbol)symbol;
            var result = alias.GetAliasTarget(basesBeingResolved);

            if (result is TypeSymbol type) {
                var args = (this, diagnostics, syntax);
                type.VisitType((typePart, argTuple, isNested) => {
                    return false;
                }, args);
            }

            return result;
        }

        alias = null;
        return symbol;
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
        ConsList<TypeSymbol> basesBeingResolved,
        bool useFatArray = true) {
        var type = BindType(node.elementType, diagnostics, basesBeingResolved);
        var jaggedRank = node.rankSpecifiers.Count;

        if (type.nullableUnderlyingTypeOrSelf.isStatic)
            diagnostics.Push(Error.ArrayOfStaticType(node.elementType.location, type.nullableUnderlyingTypeOrSelf));

        for (var i = 0; i < jaggedRank; i++) {
            var rankSpecifier = node.rankSpecifiers[i];
            var dimension = rankSpecifier.size;

            if (!permitDimensions && dimension is not null)
                diagnostics.Push(Error.ArraySizeInDeclaration(rankSpecifier.size.location));

            var array = CreateArrayOrFatArray(type, 1, diagnostics, useFatArray);
            type = new TypeWithAnnotations(array);

            if (i + 1 < jaggedRank)
                type = type.SetIsAnnotated();
        }

        return type;
    }

    private TypeSymbol CreateArrayOrFatArray(
        TypeWithAnnotations elementType,
        int rank,
        BelteDiagnosticQueue diagnostics,
        bool useFatArray = true) {
        // var element = elementType.type;

        // TODO Do we want to optimize non-nullable reference type arrays to use null as the sentinel value
        // instead of a bit vector like the fat array does?
        //  || (element.IsVerifierReference() && !element.IsNullableType())
        if (!useFatArray || rank != 1)
            return ArrayTypeSymbol.CreateArray(elementType, 1);

        var fatArray = CorLibrary.TryGetWellKnownType(WellKnownType.Array, compilation);

        if (fatArray is ErrorTypeSymbol)
            diagnostics.Push(Error.PredefinedTypeNotFound(fatArray.name));

        return fatArray.Construct([new TypeOrConstant(elementType)]);
    }

    private protected NamespaceOrTypeOrAliasSymbolWithAnnotations BindNonTemplateSimpleNamespaceOrTypeOrAliasSymbol(
        IdentifierNameSyntax node,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved,
        NamespaceOrTypeSymbol qualifier) {
        var name = node.identifier.valueText;

        if (string.IsNullOrWhiteSpace(name)) {
            var error = Error.UndefinedSymbol(node.location, name);

            return new TypeWithAnnotations(
                new ExtendedErrorTypeSymbol(compilation.globalNamespaceInternal, name, 0, error)
            );
        }

        var errorResult = CreateErrorIfLookupOnTemplateParameter(node.parent, qualifier, name, 0, diagnostics);

        if (errorResult is not null)
            return new TypeWithAnnotations(errorResult);

        var result = LookupResult.GetInstance();
        var options = LookupOptions.NamespacesOrTypesOnly;

        var performedLookup = false;

        // Prefer user-defined symbols over special types EXCEPT during template binding
        // ? The only reason we make this exception is to prevent infinite loops in binding
        if (node.parent.parent.kind != SyntaxKind.TemplateParameterList) {
            LookupSymbolsSimpleName(result, qualifier, name, 0, basesBeingResolved, options, node.location, true);
            performedLookup = true;
        }

        if (!result.isMultiViable && qualifier is null) {
            var specialType = SpecialTypes.GetTypeFromMetadataName(string.Concat("global::", name));

            if (specialType != SpecialType.None)
                return new TypeWithAnnotations(CorLibrary.GetSpecialType(specialType));
        }

        if (!performedLookup)
            LookupSymbolsSimpleName(result, qualifier, name, 0, basesBeingResolved, options, node.location, true);

        var bindingResult = ResultSymbol(result, name, 0, node, diagnostics, out _, qualifier, options);

        result.Free();
        return NamespaceOrTypeOrAliasSymbolWithAnnotations.CreateUnannotated(false, bindingResult);
    }

    private NamespaceOrTypeOrAliasSymbolWithAnnotations BindQualifiedName(
        ExpressionSyntax leftName,
        SimpleNameSyntax rightName,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved) {
        var left = BindNamespaceOrTypeSymbol(leftName, diagnostics, basesBeingResolved).namespaceOrTypeSymbol;

        var isLeftUnboundTemplateType = left.kind == SymbolKind.NamedType &&
            ((NamedTypeSymbol)left).isUnboundTemplateType;

        if (isLeftUnboundTemplateType)
            left = ((NamedTypeSymbol)left).originalDefinition;

        var right = BindSimpleNamespaceOrTypeOrAliasSymbol(rightName, diagnostics, basesBeingResolved, left);

        if (isLeftUnboundTemplateType)
            return ConvertToUnbound();

        return right;

        NamespaceOrTypeOrAliasSymbolWithAnnotations ConvertToUnbound() {
            var namedTypeRight = right.symbol as NamedTypeSymbol;

            if (namedTypeRight is not null && namedTypeRight.isTemplateType)
                return new TypeWithAnnotations(namedTypeRight.AsUnboundTemplateType(), right.isNullable);

            return right;
        }
    }

    private NamespaceOrTypeOrAliasSymbolWithAnnotations BindTemplateSimpleNamespaceOrTypeOrAliasSymbol(
        TemplateNameSyntax node,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved,
        NamespaceOrTypeSymbol qualifier) {
        var plainName = node.identifier.valueText;
        var templateArguments = node.templateArgumentList.arguments;
        var options = LookupOptions.NamespacesOrTypesOnly;
        var isUnboundTypeExpr = node.isUnboundTemplateName;

        var unconstructedType = LookupTemplateTypeName(
            diagnostics,
            basesBeingResolved,
            qualifier,
            node,
            plainName,
            node.arity,
            options
        );

        NamedTypeSymbol resultType;

        if (isUnboundTypeExpr) {
            if (!IsUnboundTypeAllowed(node)) {
                if (!unconstructedType.IsErrorType())
                    diagnostics.Push(Error.UnexpectedUnboundTemplateName(node.location));

                resultType = unconstructedType.Construct(
                    UnboundArgumentErrorTypeSymbol.CreateTemplateArguments(
                        unconstructedType.templateParameters,
                        node.arity,
                        error: null
                    ),
                    unbound: false
                );
            } else {
                resultType = unconstructedType.AsUnboundTemplateType();
            }
        } else if ((flags & BinderFlags.SuppressTemplateArgumentBinding) != 0) {
            resultType = unconstructedType.Construct(
                PlaceholderTemplateArgumentSymbol.CreateTemplateArguments(unconstructedType.templateParameters)
            );
        } else {
            var boundTemplateArguments = BindTemplateArguments(templateArguments, diagnostics, basesBeingResolved);

            resultType = ConstructNamedType(
                unconstructedType,
                node,
                templateArguments,
                boundTemplateArguments,
                basesBeingResolved,
                diagnostics
            );
        }

        return new TypeWithAnnotations(resultType);
    }

    private NamedTypeSymbol LookupTemplateTypeName(
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved,
        NamespaceOrTypeSymbol qualifier,
        TemplateNameSyntax node,
        string plainName,
        int arity,
        LookupOptions options) {
        var errorResult = CreateErrorIfLookupOnTemplateParameter(
            node.parent,
            qualifier,
            plainName,
            arity,
            diagnostics
        );

        if (errorResult is not null)
            return errorResult;

        var lookupResult = LookupResult.GetInstance();
        LookupSymbolsSimpleName(
            lookupResult,
            qualifier,
            plainName,
            arity,
            basesBeingResolved,
            options,
            node.location,
            true
        );

        var lookupResultSymbol = ResultSymbol(
            lookupResult,
            plainName,
            arity,
            node,
            diagnostics,
            out _,
            qualifier,
            options
        );

        if (lookupResultSymbol is not NamedTypeSymbol type) {
            type = new ExtendedErrorTypeSymbol(
                GetContainingNamespaceOrType(lookupResultSymbol),
                [lookupResultSymbol],
                lookupResult.kind,
                lookupResult.error,
                arity
            );
        }

        lookupResult.Free();
        return type;
    }

    private ExtendedErrorTypeSymbol CreateErrorIfLookupOnTemplateParameter(
        BelteSyntaxNode node,
        NamespaceOrTypeSymbol qualifier,
        string name,
        int arity,
        BelteDiagnosticQueue diagnostics) {
        if ((qualifier is not null) && (qualifier.kind == SymbolKind.TemplateParameter)) {
            var diagnostic = Error.LookupInTemplateVariable(node.location, qualifier as TypeSymbol);
            return new ExtendedErrorTypeSymbol(compilation, name, arity, diagnostic, unreported: false);
        }

        return null;
    }

    private NamedTypeSymbol ConstructNamedType(
        NamedTypeSymbol type,
        SyntaxNode typeSyntax,
        SeparatedSyntaxList<BaseArgumentSyntax> templateArgumentsSyntax,
        AnalyzedArguments analyzedArguments,
        ConsList<TypeSymbol> basesBeingResolved,
        BelteDiagnosticQueue diagnostics) {
        var argumentAnalysis = OverloadResolution.AnalyzeArguments(
            type.templateParameters.ToImmutableArray<Symbol>(),
            analyzedArguments,
            false,
            false
        );

        if (!argumentAnalysis.isValid) {
            // TODO We are synthesizing an overload result to reuse the same diagnostic logic
            // This probably means refactor
            var analysisResult = MemberAnalysisResult.ArgumentParameterMismatch(argumentAnalysis);
            var errorResult = new MemberResolutionResult<NamedTypeSymbol>(type, type, analysisResult, false);
            var overloadResult = new OverloadResolutionResult<NamedTypeSymbol>();
            overloadResult.resultsBuilder.Add(errorResult);
            overloadResult.ReportDiagnostics(
                this,
                typeSyntax.location,
                typeSyntax,
                diagnostics,
                type.name,
                null,
                typeSyntax,
                analyzedArguments,
                [type],
                null
            );

            var errorArguments = analyzedArguments.arguments.ToImmutable();
            analyzedArguments.Free();

            return new ConstructedErrorTypeSymbol(
                (ErrorTypeSymbol)CreateErrorType(type.name),
                errorArguments.Select(e => e.typeOrConstant).ToImmutableArray()
            );
        }

        var (rearrangedArguments, _) = RearrangeArguments(
            analyzedArguments.arguments,
            analyzedArguments.refKinds,
            argumentAnalysis.argsToParams
        );

        for (var i = 0; i < type.templateParameters.Length; i++) {
            var parameter = type.templateParameters[i];
            var argument = rearrangedArguments[i];

            var targetType = parameter.underlyingType.type;

            if (argument.isTypeOrConstant &&
                argument.typeOrConstant.isConstant && ConstantValue.IsNull(argument.typeOrConstant.constant)) {
                if (!targetType.IsNullableType())
                    diagnostics.Push(Error.CannotConvertArgument(argument.syntax.location, null, targetType, i + 1));

                continue;
            }

            var sourceType = (argument.isTypeOrConstant && argument.typeOrConstant.isType)
                ? (argument.typeOrConstant.type.typeKind == TypeKind.TemplateParameter
                    ? (argument.typeOrConstant.type.type as TemplateParameterSymbol).underlyingType.type
                    : CorLibrary.GetSpecialType(SpecialType.Type))
                : argument.type;

            if (sourceType is not null) {
                var conversion = conversions.ClassifyImplicitConversionFromType(sourceType, targetType);

                if (!conversion.exists) {
                    GenerateImplicitConversionError(
                        diagnostics,
                        argument.syntax,
                        conversion,
                        argument.type,
                        targetType
                    );
                }
            } else {
                Debug.Assert(diagnostics.Count > 0);
            }
        }

        var templateArguments = FixTemplateArgumentsForConstruction(rearrangedArguments, type.templateParameters);

        analyzedArguments.Free();
        type = type.Construct(templateArguments);

        if (!flags.Includes(BinderFlags.SuppressConstraintChecks) && ConstraintsHelpers.RequiresChecking(type))
            type.CheckConstraintsForNamedType(typeSyntax.location, diagnostics, typeSyntax);

        return type;
    }

    private ImmutableArray<TypeOrConstant> FixTemplateArgumentsForConstruction(
        ImmutableArray<BoundExpressionOrTypeOrConstant> arguments,
        ImmutableArray<TemplateParameterSymbol> parameters) {
        var builder = ArrayBuilder<TypeOrConstant>.GetInstance();

        for (var i = 0; i < arguments.Length; i++) {
            var argument = arguments[i].typeOrConstant;
            var parameter = parameters[i];

            if (parameter.hasNotNullConstraint)
                builder.Add(new TypeOrConstant(argument.type.type.StrippedType()));
            else
                builder.Add(argument);
        }

        return builder.ToImmutableAndFree();
    }

    private NamedTypeSymbol ConstructNamedTypeUnlessTemplateArgumentOmitted(
        SyntaxNode typeSyntax,
        NamedTypeSymbol type,
        SeparatedSyntaxList<BaseArgumentSyntax> templateArgumentsSyntax,
        AnalyzedArguments templateArguments,
        BelteDiagnosticQueue diagnostics) {
        if (templateArgumentsSyntax?.Any(SyntaxKind.OmittedArgument) == true) {
            diagnostics.Push(Error.BadArity(
                typeSyntax.location,
                type,
                MessageID.IDS_SK_TYPE.Localize(),
                templateArgumentsSyntax.Count
            ));

            templateArguments.Free();
            return type;
        } else {
            return ConstructNamedType(
                type,
                typeSyntax,
                templateArgumentsSyntax,
                templateArguments,
                basesBeingResolved: null,
                diagnostics: diagnostics
            );
        }
    }

    internal TypeWithAnnotations BindTypeOrImplicitType(
        TypeSyntax syntax,
        BelteDiagnosticQueue diagnostics,
        out bool isImplicitlyTyped,
        out bool isNonNullable,
        out bool isNullable) {
        return BindTypeOrImplicitType(
            syntax,
            diagnostics,
            out isImplicitlyTyped,
            out isNonNullable,
            out isNullable,
            out _
        );
    }

    internal TypeWithAnnotations BindTypeOrImplicitType(
        TypeSyntax syntax,
        BelteDiagnosticQueue diagnostics,
        out bool isImplicitlyTyped,
        out bool isNonNullable,
        out bool isNullable,
        out AliasSymbol alias) {
        if (syntax.isImplicitlyTyped || (syntax is NonNullableTypeSyntax nn && nn.type.isImplicitlyTyped) ||
            (syntax is NullableTypeSyntax n && n.type.isImplicitlyTyped)) {
            isImplicitlyTyped = true;
            isNonNullable = syntax.kind == SyntaxKind.NonNullableType;
            isNullable = syntax.kind == SyntaxKind.NullableType;
            alias = null;
            return new TypeWithAnnotations(null, true);
        } else {
            var symbol = BindTypeOrAlias(syntax, diagnostics);
            isImplicitlyTyped = false;
            isNonNullable = false;
            isNullable = false;
            return UnwrapAlias(symbol, out alias, diagnostics, syntax).typeWithAnnotations;
        }
    }

    internal BoundExpression BindTypeOrRValue(ExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var valueOrType = BindExpressionInternal(node, diagnostics: diagnostics, called: false, indexed: false);

        if (valueOrType.kind == BoundKind.TypeExpression)
            return valueOrType;

        return CheckValue(valueOrType, BindValueKind.RValue, diagnostics);
    }

    internal BoundExpression BindTypeOrRValueAllowingImplicitEnum(
        ExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var valueOrType = BindExpressionInternal(node, diagnostics: diagnostics, called: false, indexed: false);

        if (valueOrType.kind is BoundKind.TypeExpression or BoundKind.UnconvertedImplicitEnumFieldExpression)
            return valueOrType;

        return CheckValue(valueOrType, BindValueKind.RValue, diagnostics);
    }

    private AnalyzedArguments BindTemplateArguments(
        SeparatedSyntaxList<BaseArgumentSyntax> templateArguments,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved = null) {
        var analyzedArguments = AnalyzedArguments.GetInstance();

        for (var i = 0; i < templateArguments.Count; i++) {
            BindTemplateArgument(
                analyzedArguments,
                templateArguments[i],
                diagnostics,
                i,
                basesBeingResolved
            );
        }

        return analyzedArguments;
    }

    private void BindTemplateArgument(
        AnalyzedArguments analyzedArguments,
        BaseArgumentSyntax templateArgument,
        BelteDiagnosticQueue diagnostics,
        int index,
        ConsList<TypeSymbol> basesBeingResolved = null) {
        analyzedArguments.syntaxes.Add(templateArgument);

        if (templateArgument.kind == SyntaxKind.OmittedArgument) {
            var errorType = UnboundArgumentErrorTypeSymbol.Instance;

            analyzedArguments.arguments.Add(
                new BoundExpressionOrTypeOrConstant(templateArgument, new TypeOrConstant(errorType))
            );

            analyzedArguments.hasErrors.Add(true);
            analyzedArguments.types.Add(errorType);
            return;
        }

        var argument = (ArgumentSyntax)templateArgument;

        if (argument.identifier is not null)
            analyzedArguments.AddName(argument.identifier);

        var typeWithAnnotations = BindType(argument.expression, BelteDiagnosticQueue.Discarded);
        var type = typeWithAnnotations.type;

        if (type.StrippedType() is not ErrorTypeSymbol) {
            analyzedArguments.types.Add(type);
            analyzedArguments.hasErrors.Add(false);
            analyzedArguments.arguments.Add(new BoundExpressionOrTypeOrConstant(argument, new TypeOrConstant(type)));
            return;
        }

        var boundArgument = BindExpression(argument.expression, diagnostics);

        analyzedArguments.types.Add(boundArgument.Type());

        if (boundArgument.constantValue is null) {
            diagnostics.Push(Error.ConstantExpected(templateArgument.location));
            analyzedArguments.hasErrors.Add(true);
        } else {
            analyzedArguments.hasErrors.Add(false);
        }

        analyzedArguments.arguments.Add(
            new BoundExpressionOrTypeOrConstant(argument, new TypeOrConstant(boundArgument.constantValue))
        );
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

                if (tpNames is not null && tpNames.Contains(name))
                    diagnostics.Push(Error.LocalSameNameAsTemplate(GetLocation(p), name));

                if (!pNames.Add(name))
                    diagnostics.Push(Error.DuplicateParameterName(GetLocation(p), name));
            }
        }

        tpNames?.Free();
        pNames?.Free();
    }

    #endregion

    #region Operators

    internal static bool? ExpressionOfTypeMatchesPatternType(
        Conversions conversions,
        TypeSymbol expressionType,
        TypeSymbol patternType,
        out Conversion conversion,
        ConstantValue operandConstantValue = null,
        bool operandCouldBeNull = false) {
        if (expressionType.Equals(patternType, TypeCompareKind.AllIgnoreOptions)) {
            conversion = Conversion.Identity;
            return true;
        }

        conversion = conversions.ClassifyBuiltInConversion(expressionType, patternType);
        return GetIsOperatorConstantResult(expressionType, patternType, conversion.kind, operandConstantValue, operandCouldBeNull);
    }

    internal static bool? GetIsOperatorConstantResult(
        TypeSymbol operandType,
        TypeSymbol targetType,
        ConversionKind conversionKind,
        ConstantValue operandConstantValue,
        bool operandCouldBeNull = true) {
        if (ConstantValue.IsNull(operandConstantValue))
            return false;

        operandCouldBeNull =
            operandCouldBeNull &&
            operandType.IsNullableType() &&
            (operandConstantValue is null || ConstantValue.IsNull(operandConstantValue));

        switch (conversionKind) {
            case ConversionKind.None:
                if (!operandType.ContainsTemplateParameter() && !targetType.ContainsTemplateParameter())
                    return false;

                if (operandType.isValueType && targetType.IsClassType() && targetType.specialType != SpecialType.Enum ||
                    targetType.isValueType && operandType.IsClassType() && operandType.specialType != SpecialType.Enum) {
                    return false;
                }

                return null;
            case ConversionKind.ImplicitNumeric:
            case ConversionKind.ExplicitNumeric:
            case ConversionKind.ImplicitEnum:
            case ConversionKind.ImplicitConstant:
            case ConversionKind.ImplicitUserDefined:
            case ConversionKind.ExplicitUserDefined:
                return false;
            case ConversionKind.ExplicitEnum:
                if (operandType.IsEnumType() && targetType.IsEnumType())
                    goto case ConversionKind.None;

                return false;
            case ConversionKind.ExplicitNullable:
                if (targetType.IsNullableType())
                    return false;

                if (Conversions.HasIdentityConversion(operandType.GetNullableUnderlyingType(), targetType))
                    return operandCouldBeNull ? null : true;

                return false;
            case ConversionKind.ImplicitReference:
                return operandCouldBeNull ? null : true;
            case ConversionKind.ExplicitReference:
            case ConversionKind.AnyUnboxing:
                return null;
            case ConversionKind.Identity:
                return operandCouldBeNull ? null : true;
            case ConversionKind.AnyBoxing:
                return operandCouldBeNull ? null : true;
            case ConversionKind.ImplicitNullable:
                return operandType.Equals(targetType.GetNullableUnderlyingType(), TypeCompareKind.AllIgnoreOptions)
                    ? true : false;
            default:
            case ConversionKind.ExplicitPointerToInteger:
            case ConversionKind.ExplicitPointerToPointer:
            case ConversionKind.ImplicitPointerToVoid:
            case ConversionKind.ExplicitIntegerToPointer:
            case ConversionKind.ImplicitNullToPointer:
            case ConversionKind.NullLiteral:
            case ConversionKind.DefaultLiteral:
                throw ExceptionUtilities.UnexpectedValue(conversionKind);
        }
    }

    internal static BinaryOperatorKind RelationalOperatorType(TypeSymbol type) {
        return type.specialType switch {
            SpecialType.Float32 => BinaryOperatorKind.Float32,
            SpecialType.Float64 => BinaryOperatorKind.Float64,
            SpecialType.Decimal => BinaryOperatorKind.Float64,
            SpecialType.Char => BinaryOperatorKind.Char,
            SpecialType.Int8 => BinaryOperatorKind.Int32,
            SpecialType.UInt8 => BinaryOperatorKind.Int32,
            SpecialType.UInt16 => BinaryOperatorKind.Int32,
            SpecialType.Int16 => BinaryOperatorKind.Int32,
            SpecialType.Int32 => BinaryOperatorKind.Int32,
            SpecialType.UInt32 => BinaryOperatorKind.UInt32,
            SpecialType.Int64 => BinaryOperatorKind.Int64,
            SpecialType.Int => BinaryOperatorKind.Int64,
            SpecialType.UInt64 => BinaryOperatorKind.UInt64,
            SpecialType.String => BinaryOperatorKind.String,
            SpecialType.Bool => BinaryOperatorKind.Bool,
            _ => BinaryOperatorKind.Error,
        };
    }

    private BoundExpression BindBinaryExpression(BinaryExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        if (IsSimpleBinaryOperator(node))
            return BindSimpleBinaryOperator(node, diagnostics);
        else if (node.operatorToken.kind is SyntaxKind.IsKeyword or SyntaxKind.IsntKeyword)
            return BindIsOperator(node, diagnostics);
        else if (node.operatorToken.kind == SyntaxKind.AsKeyword)
            return BindAsOperator(node, diagnostics);
        else if (node.operatorToken.kind is SyntaxKind.PipePipeToken or SyntaxKind.AmpersandAmpersandToken)
            return BindConditionalLogicalOperator(node, diagnostics);
        else if (node.operatorToken.kind is SyntaxKind.QuestionQuestionToken or SyntaxKind.QuestionExclamationToken)
            return BindNullCoalescingOrPropagationOperator(node, diagnostics);

        throw ExceptionUtilities.Unreachable();
    }

    private BoundExpression BindIsPatternExpression(IsPatternExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var expression = BindRValueWithoutTargetType(node.left, diagnostics);
        var expressionType = expression.type;
        var hasErrors = false;

        if (expressionType is null || expressionType.IsVoidType()) {
            diagnostics.Push(Error.BadPatternExpression(node.left.location, expression));
            hasErrors = true;
            expression = ErrorExpression(expression.syntax, expression);
        }

        var pattern = (DeclarationPatternSyntax)node.right;
        var localSymbol = LookupLocal(pattern.identifier) ?? throw ExceptionUtilities.Unreachable();

        var sourceType = expressionType.StrippedType();
        var targetType = localSymbol.type.StrippedType();

        if (localSymbol.type.IsNullableType() && targetType.isValueType) {
            diagnostics.Push(Error.CannotAnnotateTypePattern(pattern.type.location, localSymbol.type, targetType));
            hasErrors = true;
        }

        var boolType = CorLibrary.GetSpecialType(SpecialType.Bool);

        if (!hasErrors && !IsCanHandle(sourceType, targetType)) {
            diagnostics.Push(Error.PatternCannotHandleTypes(pattern.type.location, sourceType, targetType));
            hasErrors = true;
        }

        return new BoundIsPatternExpression(
            node,
            expression,
            localSymbol,
            null,
            boolType,
            hasErrors
        );
    }

    private static bool IsCanHandle(TypeSymbol sourceType, TypeSymbol targetType) {
        if (sourceType.specialType != targetType.specialType) {
            if (sourceType.specialType == SpecialType.Any || targetType.specialType == SpecialType.Any)
                return true;

            if (sourceType.specialType == SpecialType.Object || targetType.specialType == SpecialType.Object)
                return true;

            return false;
        }

        if (sourceType.specialType == targetType.specialType && sourceType.specialType != SpecialType.None)
            return true;

        if (sourceType.IsArray() || targetType.IsArray())
            return sourceType.Equals(targetType);

        return !targetType.isStatic;
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

            if (operand.type is not null &&
                !operand.type.IsNullableType() &&
                (operand.type is not TemplateParameterSymbol tp || tp.hasNotNullConstraint)) {
                diagnostics.Push(Error.CannotNullCheckNonNull(node.location, node.operatorToken.text, operand.type));
            }

            var boundRight = BindLiteralExpression(l, diagnostics);
            var constantValue = ConstantFolding.FoldIs(operand, boundRight, isIsntOperator);
            return new BoundIsOperator(node, operand, boundRight, isIsntOperator, constantValue, resultType);
        }

        var targetTypeWithAnnotations = BindType(node.right, diagnostics, out var alias);
        var targetType = targetTypeWithAnnotations.type;
        var strippedType = targetType.StrippedType();
        var boundType = new BoundTypeExpression(node.right, targetTypeWithAnnotations, alias, targetType);

        if (ConstantValue.IsNull(operand.constantValue) ||
            operand.kind == BoundKind.MethodGroup ||
            operand.type.IsVoidType()) {
            diagnostics.Push(Warning.AlwaysValue(node.location, isIsntOperator));
            return new BoundLiteralExpression(node, new ConstantValue(isIsntOperator, SpecialType.Bool), resultType);
        }

        var sourceType = operand.Type().StrippedType();

        if (!IsCanHandle(sourceType, strippedType)) {
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
        var targetTypeWithAnnotations = BindType(node.right, diagnostics, out var alias);
        var targetType = targetTypeWithAnnotations.type;
        var targetTypeKind = targetType.typeKind;
        var boundType = new BoundTypeExpression(node.right, targetTypeWithAnnotations, alias, targetType);
        var resultType = CorLibrary.GetOrCreateNullableType(targetType);

        if (operand.hasAnyErrors || targetTypeKind == TypeKind.Error)
            return new BoundAsOperator(node, operand, boundType, null, null, resultType, true);

        if (targetType.isStatic) {
            diagnostics.Push(Warning.NeverGivenType(node.location, targetType));
            return new BoundLiteralExpression(node, new ConstantValue(null, SpecialType.None), resultType);
        }

        BoundValuePlaceholder operandPlaceholder;
        BoundExpression operandConversion;

        if (operand.IsLiteralNull()) {
            operandPlaceholder = new BoundValuePlaceholder(operand.syntax, operand.Type());
            operandConversion = CreateConversion(
                node,
                operandPlaceholder,
                Conversion.NullLiteral,
                false,
                targetType,
                diagnostics
            );

            return new BoundAsOperator(node, operand, boundType, operandPlaceholder, operandConversion, resultType);
        }

        var operandType = operand.Type();
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
            operandPlaceholder = new BoundValuePlaceholder(operand.syntax, operand.Type());
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

    internal static SpecialType GetEnumPromotedType(SpecialType underlyingType) {
        switch (underlyingType) {
            case SpecialType.UInt8:
            case SpecialType.Int8:
            case SpecialType.Int16:
            case SpecialType.UInt16:
            case SpecialType.Char:
                return SpecialType.Int32;
            case SpecialType.Int32:
            case SpecialType.UInt32:
            case SpecialType.Int64:
            case SpecialType.UInt64:
            case SpecialType.String:
            case SpecialType.Int:
                return underlyingType;
            default:
                throw ExceptionUtilities.UnexpectedValue(underlyingType);
        }
    }

    private BoundExpression BindNullCoalescingOrPropagationOperator(
        BinaryExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var isPropagation = node.operatorToken.kind == SyntaxKind.QuestionExclamationToken;
        var leftOperand = BindValue(node.left, diagnostics, BindValueKind.RValue);
        leftOperand = BindToNaturalType(leftOperand, diagnostics);
        var rightOperand = BindValue(node.right, diagnostics, BindValueKind.RValue);

        if (leftOperand.hasAnyErrors || rightOperand.hasAnyErrors) {
            leftOperand = BindToTypeForErrorRecovery(leftOperand);
            rightOperand = BindToTypeForErrorRecovery(rightOperand);

            return new BoundNullCoalescingOperator(
                node,
                leftOperand,
                rightOperand,
                isPropagation,
                null,
                CreateErrorType(),
                true
            );
        }

        var optLeftType = leftOperand.Type();
        var optRightType = rightOperand.Type();
        var isLeftNullable = optLeftType is not null && optLeftType.IsNullableType();
        var optLeftType0 = isPropagation ? optLeftType : (isLeftNullable ? optLeftType.GetNullableUnderlyingType() : optLeftType);

        if (leftOperand.kind == BoundKind.MethodGroup || (optLeftType is not null && !optLeftType.IsNullableType()))
            return GenerateNullCoalescingBadBinaryOpsError(node, leftOperand, rightOperand, isPropagation, diagnostics);

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
                    isPropagation,
                    ConstantFolding.FoldNullCoalescing(leftOperand, convertedRightOperand, isPropagation, optLeftType0),
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
                    isPropagation,
                    ConstantFolding.FoldNullCoalescing(leftOperand, convertedRightOperand, isPropagation, optLeftType),
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
                        isPropagation,
                        ConstantFolding.FoldNullCoalescing(leftConversion, rightOperand, isPropagation, optRightType),
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
                        isPropagation,
                        ConstantFolding.FoldNullCoalescing(leftConversion, rightOperand, isPropagation, optRightType),
                        optRightType
                    );
                }
            }
        }

        return GenerateNullCoalescingBadBinaryOpsError(node, leftOperand, rightOperand, isPropagation, diagnostics);
    }

    private BoundExpression GenerateNullCoalescingBadBinaryOpsError(
        BinaryExpressionSyntax node,
        BoundExpression leftOperand,
        BoundExpression rightOperand,
        bool isPropagation,
        BelteDiagnosticQueue diagnostics) {
        leftOperand = BindToTypeForErrorRecovery(leftOperand);
        rightOperand = BindToTypeForErrorRecovery(rightOperand);
        diagnostics.Push(Error.InvalidBinaryOperatorUse(
            node.location,
            SyntaxFacts.GetText(node.operatorToken.kind),
            leftOperand.Type(),
            rightOperand.Type()
        ));

        return new BoundNullCoalescingOperator(
            node,
            leftOperand,
            rightOperand,
            isPropagation,
            null,
            CreateErrorType(),
            true
        );
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

        if (left.type is not null && left.StrippedType().specialType == SpecialType.Bool &&
            right.type is not null && right.StrippedType().specialType == SpecialType.Bool) {
            var constantValue = ConstantFolding.FoldBinary(
                left,
                right,
                kind | BinaryOperatorKind.Bool,
                left.Type(),
                node.location,
                diagnostics
            );

            return new BoundBinaryOperator(
                node,
                left,
                right,
                kind | BinaryOperatorKind.Bool,
                null,
                constantValue,
                left.StrippedType()
            );
        }

        if (left.hasAnyErrors || right.hasAnyErrors) {
            return new BoundBinaryOperator(
                node,
                left,
                right,
                kind | BinaryOperatorKind.Bool,
                null,
                null,
                left.StrippedType(),
                true
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
                    signature.method,
                    null,
                    signature.returnType
                );
            }
        }

        return new BoundBinaryOperator(node, left, right, kind, null, null, CreateErrorType(), true);
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
                case SyntaxKind.SlashBackslashToken:
                case SyntaxKind.BackslashSlashToken:
                    return true;
                case SyntaxKind.IsKeyword:
                case SyntaxKind.IsntKeyword:
                case SyntaxKind.AsKeyword:
                case SyntaxKind.AmpersandAmpersandToken:
                case SyntaxKind.PipePipeToken:
                case SyntaxKind.QuestionQuestionToken:
                case SyntaxKind.QuestionExclamationToken:
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
                diagnostics.Push(Error.PossibleBadNegativeCast(node.location));
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
        if (resultKind == LookupResultKind.Ambiguous) {
            diagnostics.Push(
                Error.AmbiguousBinaryOperator(node.location, operatorToken.text, left.Type(), right.Type())
            );
        } else {
            diagnostics.Push(
                Error.InvalidBinaryOperatorUse(node.location, operatorToken.text, left.Type(), right.Type())
            );
        }
    }

    private static void ReportUnaryOperatorError(
        BelteSyntaxNode node,
        BelteDiagnosticQueue diagnostics,
        string operatorName,
        BoundExpression operand,
        LookupResultKind resultKind) {
        if (resultKind == LookupResultKind.Ambiguous)
            diagnostics.Push(Error.AmbiguousUnaryOperator(node.location, operatorName, operand.Type()));
        else
            diagnostics.Push(Error.InvalidUnaryOperatorUse(node.location, operatorName, operand.Type()));
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

        if (left.hasAnyErrors || right.hasAnyErrors) {
            left = BindToTypeForErrorRecovery(left);
            right = BindToTypeForErrorRecovery(right);

            return new BoundBinaryOperator(
                node,
                left,
                right,
                kind,
                null,
                null,
                GetBinaryOperatorErrorType(kind),
                true
            );
        }

        var leftNull = left.IsLiteralNull();
        var rightNull = right.IsLiteralNull();
        var isEquality = kind == BinaryOperatorKind.Equal || kind == BinaryOperatorKind.NotEqual;
        var isEqual = kind == BinaryOperatorKind.Equal;

        if (isEquality) {
            var boolType = CorLibrary.GetSpecialType(SpecialType.Bool); ;

            if (leftNull && rightNull) {
                return new BoundLiteralExpression(
                    node,
                    new ConstantValue(isEqual, SpecialType.Bool),
                    boolType
                );
            }

            if ((leftNull && !right.type.IsNullableType()) || (rightNull && !left.type.IsNullableType())) {
                diagnostics.Push(Warning.AlwaysValue(node.location, !isEqual));

                return new BoundLiteralExpression(
                    node,
                    new ConstantValue(!isEqual, SpecialType.Bool),
                    boolType
                );
            }

            if (rightNull)
                diagnostics.Push(Warning.NullBinaryEquality(node.location, !isEqual, left));
        }

        if (IsTupleBinaryOperation(left, right) && isEquality)
            return BindTupleBinaryOperator(node, kind, left, right, diagnostics);

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

            resultConstant = FoldBinaryOperator(
                signature,
                resultOperatorKind,
                resultLeft,
                resultRight,
                node.location,
                diagnostics
            );

            if (ConstantValue.IsNull(resultConstant))
                diagnostics.Push(Warning.AlwaysValue(node.location, null));
        } else {
            resultLeft = BindToNaturalType(resultLeft, diagnostics, false);
            resultRight = BindToNaturalType(resultRight, diagnostics, false);
        }

        return new BoundBinaryOperator(
            node,
            resultLeft,
            resultRight,
            resultOperatorKind,
            signature.method,
            resultConstant,
            resultType,
            hasErrors
        );
    }

    private static bool IsTupleBinaryOperation(BoundExpression left, BoundExpression right) {
        var leftDefaultOrNew = left.IsLiteralDefaultOrImplicitObjectCreation();
        var rightDefaultOrNew = right.IsLiteralDefaultOrImplicitObjectCreation();

        if (leftDefaultOrNew && rightDefaultOrNew)
            return false;

        return (GetTupleCardinality(left) > 1 || leftDefaultOrNew) &&
               (GetTupleCardinality(right) > 1 || rightDefaultOrNew);
    }

    private BoundTupleBinaryOperator BindTupleBinaryOperator(
        BinaryExpressionSyntax node,
        BinaryOperatorKind kind,
        BoundExpression left,
        BoundExpression right,
        BelteDiagnosticQueue diagnostics) {
        var operators = BindTupleBinaryOperatorNestedInfo(node, kind, left, right, diagnostics);
        var convertedLeft = ApplyConvertedTypes(left, operators, isRight: false, diagnostics);
        var convertedRight = ApplyConvertedTypes(right, operators, isRight: true, diagnostics);

        return new BoundTupleBinaryOperator(
            node,
            convertedLeft,
            convertedRight,
            kind,
            operators,
            CorLibrary.GetSpecialType(SpecialType.Bool)
        );
    }

    private TupleBinaryOperatorInfo.Multiple BindTupleBinaryOperatorNestedInfo(
        BinaryExpressionSyntax node,
        BinaryOperatorKind kind,
        BoundExpression left,
        BoundExpression right,
        BelteDiagnosticQueue diagnostics) {
        left = GiveTupleTypeToDefaultLiteralIfNeeded(left, right.type);
        right = GiveTupleTypeToDefaultLiteralIfNeeded(right, left.type);

        if (left.IsLiteralDefaultOrImplicitObjectCreation() ||
            right.IsLiteralDefaultOrImplicitObjectCreation()) {
            ReportBinaryOperatorError(node, diagnostics, node.operatorToken, left, right, LookupResultKind.Ambiguous);
            return TupleBinaryOperatorInfo.Multiple.ErrorInstance;
        }

        var leftCardinality = GetTupleCardinality(left);
        var rightCardinality = GetTupleCardinality(right);

        if (leftCardinality != rightCardinality) {
            // TODO Do we care about a tuple-specific error in this case?
            ReportBinaryOperatorError(node, diagnostics, node.operatorToken, left, right, LookupResultKind.Empty);
            // Error(diagnostics, ErrorCode.ERR_TupleSizesMismatchForBinOps, node, leftCardinality, rightCardinality);
            return TupleBinaryOperatorInfo.Multiple.ErrorInstance;
        }

        var (leftParts, leftNames) = GetTupleArgumentsOrPlaceholders(left);
        var (rightParts, rightNames) = GetTupleArgumentsOrPlaceholders(right);
        // ReportNamesMismatchesIfAny(left, right, leftNames, rightNames, diagnostics);

        var length = leftParts.Length;
        var operatorsBuilder = ArrayBuilder<TupleBinaryOperatorInfo>.GetInstance(length);

        for (var i = 0; i < length; i++)
            operatorsBuilder.Add(BindTupleBinaryOperatorInfo(node, kind, leftParts[i], rightParts[i], diagnostics));

        var operators = operatorsBuilder.ToImmutableAndFree();

        var leftNullable = left.type?.IsNullableType() == true;
        var rightNullable = right.type?.IsNullableType() == true;
        var isNullable = leftNullable || rightNullable;

        var leftTupleType = MakeConvertedType(
            operators.SelectAsArray(o => o.leftConvertedType),
            node.left,
            leftParts,
            leftNames,
            isNullable,
            compilation,
            diagnostics
        );

        var rightTupleType = MakeConvertedType(
            operators.SelectAsArray(o => o.rightConvertedType),
            node.right,
            rightParts,
            rightNames,
            isNullable,
            compilation,
            diagnostics
        );

        return new TupleBinaryOperatorInfo.Multiple(operators, leftTupleType, rightTupleType);
    }

    private TypeSymbol MakeConvertedType(
        ImmutableArray<TypeSymbol> convertedTypes,
        BelteSyntaxNode syntax,
        ImmutableArray<BoundExpression> elements,
        ImmutableArray<string> names,
        bool isNullable,
        Compilation compilation,
        BelteDiagnosticQueue diagnostics) {
        foreach (var convertedType in convertedTypes) {
            if (convertedType is null)
                return null;
        }

        var elementLocations = elements.SelectAsArray(e => e.syntax.location);

        var tuple = NamedTypeSymbol.CreateTuple(
            location: null,
            elementTypesWithAnnotations: convertedTypes.SelectAsArray(t => new TypeWithAnnotations(t)),
            elementLocations,
            elementNames: names,
            compilation,
            shouldCheckConstraints: true,
            errorPositions: default,
            syntax,
            diagnostics
        );

        if (!isNullable)
            return tuple;

        var nullableT = CorLibrary.GetSpecialType(SpecialType.Nullable);
        return nullableT.Construct([new TypeOrConstant(tuple)]);
    }

    private TupleBinaryOperatorInfo BindTupleBinaryOperatorInfo(
        BinaryExpressionSyntax node,
        BinaryOperatorKind kind,
        BoundExpression left,
        BoundExpression right,
        BelteDiagnosticQueue diagnostics) {
        if (IsTupleBinaryOperation(left, right))
            return BindTupleBinaryOperatorNestedInfo(node, kind, left, right, diagnostics);

        var comparison = BindSimpleBinaryOperator(node, diagnostics, left, right);

        switch (comparison) {
            case BoundLiteralExpression _:
                return new TupleBinaryOperatorInfo.NullNull(kind);
            case BoundBinaryOperator binary:
                PrepareBoolConversionAndTruthOperator(
                    binary.type,
                    node,
                    kind,
                    diagnostics,
                    out var conversionIntoBoolOperator,
                    out var conversionIntoBoolOperatorPlaceholder,
                    out var boolOperator
                );

                return new TupleBinaryOperatorInfo.Single(
                    binary.left.type,
                    binary.right.type,
                    binary.operatorKind,
                    binary.method,
                    binary.type,
                    conversionIntoBoolOperatorPlaceholder,
                    conversionIntoBoolOperator,
                    boolOperator
                );
            default:
                throw ExceptionUtilities.UnexpectedValue(comparison);
        }
    }

    private void PrepareBoolConversionAndTruthOperator(
        TypeSymbol type,
        BinaryExpressionSyntax node,
        BinaryOperatorKind binaryOperator,
        BelteDiagnosticQueue diagnostics,
        out BoundExpression conversionForBool,
        out BoundValuePlaceholder conversionForBoolPlaceholder,
        out UnaryOperatorSignature boolOperator) {
        var boolean = CorLibrary.GetSpecialType(SpecialType.Bool);
        var conversion = conversions.ClassifyImplicitConversionFromType(type, boolean);

        if (conversion.isImplicit) {
            conversionForBoolPlaceholder = new BoundValuePlaceholder(node, type);
            conversionForBool = CreateConversion(
                node,
                conversionForBoolPlaceholder,
                conversion,
                isCast: false,
                boolean,
                diagnostics
            );

            boolOperator = default;
            return;
        }

        UnaryOperatorKind boolOpKind;

        switch (binaryOperator) {
            case BinaryOperatorKind.Equal:
                boolOpKind = UnaryOperatorKind.False;
                break;
            case BinaryOperatorKind.NotEqual:
                boolOpKind = UnaryOperatorKind.True;
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(binaryOperator);
        }

        BoundExpression comparisonResult = new BoundValuePlaceholder(node, type);
        var best = UnaryOperatorOverloadResolution(
            boolOpKind,
            comparisonResult,
            node,
            diagnostics,
            out _,
            out _
        );

        if (best.hasValue) {
            conversionForBoolPlaceholder = new BoundValuePlaceholder(node, type);
            conversionForBool = CreateConversion(
                node,
                conversionForBoolPlaceholder,
                best.conversion,
                isCast: false,
                best.signature.operandType,
                diagnostics
            );

            boolOperator = best.signature;
            return;
        }

        GenerateImplicitConversionError(diagnostics, node, conversion, comparisonResult, boolean);
        conversionForBoolPlaceholder = null;
        conversionForBool = null;
        boolOperator = default;
        return;
    }

    private static (ImmutableArray<BoundExpression> Elements, ImmutableArray<string> Names) GetTupleArgumentsOrPlaceholders(
        BoundExpression expr) {
        if (expr is BoundTupleExpression tuple)
            return (tuple.arguments, default);

        var tupleType = expr.type.StrippedType();
        var placeholders = tupleType.tupleElementTypes
            .SelectAsArray((t, s) => (BoundExpression)new BoundValuePlaceholder(s, t.type.type), expr.syntax);

        return (placeholders, tupleType.tupleElementNames);
    }

    internal static BoundExpression GiveTupleTypeToDefaultLiteralIfNeeded(BoundExpression expr, TypeSymbol targetType) {
        if (!expr.IsLiteralDefault() || targetType is null)
            return expr;

        return new BoundDefaultExpression(expr.syntax, false, null, null, targetType);
    }

    private BoundExpression ApplyConvertedTypes(
        BoundExpression expr,
        TupleBinaryOperatorInfo @operator,
        bool isRight,
        BelteDiagnosticQueue diagnostics) {
        var convertedType = isRight ? @operator.rightConvertedType : @operator.leftConvertedType;

        if (convertedType is null) {
            if (@operator.infoKind == TupleBinaryOperatorInfoKind.Multiple && expr is BoundTupleLiteral tuple) {
                var multiple = (TupleBinaryOperatorInfo.Multiple)@operator;

                if (multiple.operators.Length == 0)
                    return BindToNaturalType(expr, diagnostics, reportNoTargetType: false);

                var arguments = tuple.arguments;
                var length = arguments.Length;
                var builder = ArrayBuilder<BoundExpression>.GetInstance(length);

                for (var i = 0; i < length; i++)
                    builder.Add(ApplyConvertedTypes(arguments[i], multiple.operators[i], isRight, diagnostics));

                return new BoundConvertedTupleLiteral(
                    tuple.syntax,
                    tuple,
                    wasTargetTyped: false,
                    builder.ToImmutableAndFree(),
                    tuple.type,
                    tuple.hasErrors
                );
            }

            return BindToNaturalType(expr, diagnostics, reportNoTargetType: false);
        }

        return GenerateConversionForAssignment(convertedType, expr, diagnostics);
    }

    private static int GetTupleCardinality(BoundExpression expr) {
        if (expr is BoundTupleExpression tuple)
            return tuple.arguments.Length;

        var type = expr.type;

        if (type is null)
            return -1;

        if (type.StrippedType() is { isTupleType: true } tupleType)
            return tupleType.tupleElementTypes.Length;

        return -1;
    }

    private static ConstantValue FoldBinaryOperator(
        BinaryOperatorSignature signature,
        BinaryOperatorKind resultOperatorKind,
        BoundExpression resultLeft,
        BoundExpression resultRight,
        TextLocation location,
        BelteDiagnosticQueue diagnostics) {
        if (resultRight.constantValue is not null &&
            resultRight.constantValue.specialType.IsNumeric() &&
            !resultRight.constantValue.specialType.IsFloatingPoint() &&
            resultRight.constantValue.specialType != SpecialType.Char &&
            Convert.ToDouble(resultRight.constantValue.value) == 0 &&
            resultOperatorKind.Operator() == BinaryOperatorKind.Division) {
            diagnostics.Push(Error.DivideByZero(location));
            return null;
        }

        return ConstantFolding.FoldBinary(
            resultLeft,
            resultRight,
            resultOperatorKind,
            signature.leftType,
            location,
            diagnostics
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

            var leftType = left.Type();
            var rightType = right.Type();

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
            case SyntaxKind.SlashBackslashToken:
            case SyntaxKind.SlashBackslashEqualsToken:
                return BinaryOperatorKind.Min;
            case SyntaxKind.BackslashSlashToken:
            case SyntaxKind.BackslashSlashEqualsToken:
                return BinaryOperatorKind.Max;
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
            case SyntaxKind.QuestionExclamationEqualsToken:
                return BindValueKind.CompoundAssignment;
            default:
                return BindValueKind.RValue;
        }
    }

    private BoundExpression BindClampExpression(ClampExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        // TODO Operator overloading?
        var isAssignment = node.operatorToken.kind == SyntaxKind.GreaterThanLessThanEqualsToken;
        var leftValueKind = isAssignment ? BindValueKind.CompoundAssignment : BindValueKind.RValue;

        var left = BindToNaturalType(BindValue(node.left, diagnostics, leftValueKind), diagnostics);
        var lower = BindValue(node.lower, diagnostics, BindValueKind.RValue);
        var upper = BindValue(node.upper, diagnostics, BindValueKind.RValue);

        var hasErrors = left.type.IsErrorType();
        var specialType = left.type.StrippedType().specialType;

        // Exclude intptr and uintptr
        if (!hasErrors && !specialType.IsIntegral() && !specialType.IsFloatingPoint()) {
            diagnostics.Push(Error.ClampMustBeNumeric(node.left.location, left.type));
            hasErrors = true;
        }

        if (hasErrors) {
            lower = BindToNaturalType(lower, diagnostics, false);
            upper = BindToNaturalType(upper, diagnostics, false);
        } else {
            lower = GenerateConversionForAssignment(left.type, lower, diagnostics);
            upper = GenerateConversionForAssignment(left.type, upper, diagnostics);
            hasErrors = lower.hasAnyErrors || upper.hasAnyErrors;
        }

        ConstantValue constantValue = null;

        if (!hasErrors && !isAssignment)
            constantValue = ConstantFolding.FoldClamp(left, lower, upper, left.type);

        return new BoundClampOperator(
            node,
            left,
            isAssignment,
            lower,
            upper,
            constantValue,
            left.type,
            hasErrors
        );
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
            var noCommonError = hadMultipleCandidates
                ? Error.AmbiguousTernary(node.location, trueExpr.type, falseExpr.type)
                : Error.InvalidTernary(node.location, trueExpr.type, falseExpr.type);

            constantValue = ConstantFolding.FoldConditional(condition, trueExpr, falseExpr, null);

            return new BoundUnconvertedConditionalOperator(
                node,
                condition,
                trueExpr,
                falseExpr,
                constantValue,
                noCommonError,
                false
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
            hasErrors = trueExpr.hasAnyErrors || falseExpr.hasAnyErrors;
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
        var trueType = trueExpr.Type();
        var falseType = falseExpr.Type();

        TypeSymbol type;
        if (!Conversions.HasIdentityConversion(trueType, falseType)) {
            if (!hasErrors)
                diagnostics.Push(Error.RefConditionalDifferentTypes(falseExpr.syntax.location, trueType));

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

    private BoundExpression BindAddressOfExpression(UnaryExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var operand = BindToNaturalType(BindValue(node.operand, diagnostics, BindValueKind.AddressOf), diagnostics);

        if (operand is BoundMethodGroup group) {
            // TODO Error checking
            var method = group.methods.FirstOrDefault();
            var paramRefKinds = method.parameterRefKinds.IsDefault
                ? method.parameterTypesWithAnnotations.Select(p => RefKind.None).ToImmutableArray()
                : method.parameterRefKinds;

            // TODO propagate unmanaged calling convention from attribute data
            var unmanagedAttribute = method.GetUnmanagedCallersOnlyAttributeData(true);
            var callingConvention = (unmanagedAttribute is null ||
                unmanagedAttribute == UnmanagedCallersOnlyAttributeData.Uninitialized)
                    ? CallingConvention.Winapi
                    : CallingConvention.Unmanaged;

            var functionPointerType = FunctionPointerTypeSymbol.CreateFromParts(
                callingConvention,
                method.returnTypeWithAnnotations,
                method.refKind,
                method.parameterTypesWithAnnotations,
                paramRefKinds
            );

            if (!method.isStatic)
                diagnostics.Push(Error.CannotTakeFunctionPointerOfNonStatic(node.operand.location, method));

            return new BoundFunctionPointerLoad(
                node,
                group.methods.FirstOrDefault(),
                null,
                functionPointerType,
                operand.hasErrors
            );
        }

        var operandType = operand.Type();
        var pointerType = new PointerTypeSymbol(new TypeWithAnnotations(operandType));
        return new BoundAddressOfOperator(node, operand, false, pointerType, operand.hasErrors);
    }

    private BoundExpression BindPointerIndirectionExpression(UnaryExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var operand = BindToNaturalType(BindValue(node.operand, diagnostics, BindValueKind.RValue), diagnostics);
        BindPointerIndirectionExpressionInternal(node, diagnostics, operand, out var pointedAtType, out var hasErrors);
        return new BoundPointerIndirectionOperator(node, operand, false, pointedAtType ?? CreateErrorType(), hasErrors);
    }

    private BoundExpression BindCompileTimeExpression(UnaryExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var operand = BindExpression(node.operand, diagnostics);
        var conditional = node.operatorToken.kind == SyntaxKind.DollarQuestionToken;
        var nodeType = operand.StrippedType();

        if (!nodeType.IsPrimitiveType() && !nodeType.IsStructType() && !nodeType.IsArray())
            diagnostics.Push(Error.InvalidCompileTimeType(node.location));

        return new BoundCompileTimeExpression(node, operand, conditional, operand.type);
    }

    private static void BindPointerIndirectionExpressionInternal(
        ExpressionSyntax node,
        BelteDiagnosticQueue diagnostics,
        BoundExpression operand,
        out TypeSymbol pointedAtType,
        out bool hasErrors) {
        hasErrors = operand.hasAnyErrors;

        if (operand.StrippedType() is not PointerTypeSymbol operandType) {
            pointedAtType = null;

            if (!hasErrors) {
                diagnostics.Push(Error.PtrExpected(node.location));
                hasErrors = true;
            }
        } else {
            pointedAtType = operandType.pointedAtType;

            if (pointedAtType.IsVoidType()) {
                pointedAtType = null;

                if (!hasErrors) {
                    diagnostics.Push(Error.VoidPtr(node.location));
                    hasErrors = true;
                }
            }
        }
    }

    private BoundExpression BindUnaryExpression(UnaryExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        if (node.operatorToken.kind == SyntaxKind.AmpersandToken)
            return BindAddressOfExpression(node, diagnostics);

        if (node.operatorToken.kind == SyntaxKind.AsteriskToken)
            return BindPointerIndirectionExpression(node, diagnostics);

        if (node.operatorToken.kind is SyntaxKind.DollarToken or SyntaxKind.DollarQuestionToken)
            return BindCompileTimeExpression(node, diagnostics);

        var operatorText = node.operatorToken.text;
        var operand = BindToNaturalType(BindValue(node.operand, diagnostics, BindValueKind.RValue), diagnostics);
        var kind = SyntaxKindToUnaryOperatorKind(node.operatorToken.kind);

        if (operand.Type()?.IsErrorType() == true) {
            return new BoundUnaryOperator(
                node,
                operand,
                kind,
                null,
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
            signature.method,
            resultConstant,
            // signature.ConstrainedToTypeOpt,
            // resultKind,
            resultType
        );
    }

    private BoundExpression BindNullAssertOperator(PostfixExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var operand = BindExpression(node.operand, diagnostics);

        if (operand.IsLiteralNull()) {
            diagnostics.Push(Error.NullAssertAlwaysThrows(node.location));
            return new BoundNullAssertOperator(node, operand, true, null, CreateErrorType("<null>"), true);
        }

        var operandType = operand.Type();

        if (!operandType.IsNullableType() && operand.kind != BoundKind.ObjectCreationExpression) {
            diagnostics.Push(Error.NullAssertOnNonNullableType(node.location, operandType));
            return new BoundNullAssertOperator(node, operand, true, null, operandType, true);
        }

        var resultType = operandType.StrippedType();
        var constantValue = operand.constantValue;

        if (ConstantValue.IsNull(constantValue)) {
            diagnostics.Push(Error.NullAssertAlwaysThrows(node.location));
            return new BoundNullAssertOperator(node, operand, true, null, resultType, true);
        }

        var throwIfNull = compilation.options.optimizationLevel == OptimizationLevel.Debug
            ? true
            : node.operatorToken.kind == SyntaxKind.ExclamationToken;

        return new BoundNullAssertOperator(node, operand, throwIfNull, constantValue, resultType);
    }

    private BoundExpression BindNullErasureOperator(PostfixExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var operand = BindExpression(node.operand, diagnostics);

        if (operand.IsLiteralNull() || operand.kind == BoundKind.UnconvertedNullptrExpression) {
            diagnostics.Push(Error.NullErasureOnNull(node.location));
            return new BoundNullErasureOperator(node, operand, null, null, CreateErrorType(), true);
        }

        var operandType = operand.Type();

        if (!operandType.IsNullableType()) {
            diagnostics.Push(Error.NullErasureOnNonNullableType(node.location, operandType));
            return new BoundNullErasureOperator(node, operand, null, null, operandType, true);
        }

        var resultType = operandType.StrippedType();
        var constantValue = operand.constantValue;

        if (!LiteralUtilities.TypeHasDefaultValue(resultType.specialType)) {
            diagnostics.Push(Error.NullErasureOnTypeWithNoDefault(node.location, operandType));
            return new BoundNullErasureOperator(node, operand, null, null, operandType, true);
        }

        var defaultValue = LiteralUtilities.TryGetDefaultValue(resultType);

        if (ConstantValue.IsNull(constantValue))
            constantValue = defaultValue;

        return new BoundNullErasureOperator(node, operand, defaultValue, constantValue, resultType);
    }

    private BoundExpression BindIncrementOrNullAssertOperator(
        PostfixExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        if (node.operatorToken.kind is SyntaxKind.ExclamationToken or SyntaxKind.ExclamationExclamationToken)
            return BindNullAssertOperator(node, diagnostics);

        if (node.operatorToken.kind == SyntaxKind.QuestionToken)
            return BindNullErasureOperator(node, diagnostics);

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

        if (operand.hasAnyErrors) {
            return new BoundIncrementOperator(
                node,
                kind,
                operand,
                method: null,
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

        var operandType = operand.Type();

        var best = UnaryOperatorOverloadResolution(
            kind,
            operand,
            node,
            diagnostics,
            out var resultKind,
            out var originalUserDefinedOperators
        );

        if (!best.hasValue || !best.conversion.isImplicit) {
            return CreateErrorIncrementOperator(
                node,
                operatorToken,
                diagnostics,
                operand,
                kind,
                resultKind,
                originalUserDefinedOperators
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

        var operandPlaceholder = new BoundValuePlaceholder(operand.syntax, operand.Type());
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
            signature.method,
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

    private BoundExpression CreateErrorIncrementOperator(
        BelteSyntaxNode node,
        SyntaxToken operatorToken,
        BelteDiagnosticQueue diagnostics,
        BoundExpression operand,
        UnaryOperatorKind kind,
        LookupResultKind resultKind,
        ImmutableArray<MethodSymbol> originalUserDefinedOperators) {
        ReportUnaryOperatorError(node, diagnostics, operatorToken.text, operand, resultKind);

        return new BoundIncrementOperator(
            node,
            kind,
            operand,
            method: null,
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

    private BoundExpression BindAssignmentOperator(
        AssignmentExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        if (node.left.kind == SyntaxKind.TupleExpression || node.left.kind == SyntaxKind.DeclarationExpression)
            return BindDeconstruction(node, diagnostics);

        if (node.assignmentToken.kind is SyntaxKind.QuestionQuestionEqualsToken or SyntaxKind.QuestionExclamationEqualsToken)
            return BindNullCoalescingCompoundAssignment(node, diagnostics);
        else if (node.assignmentToken.kind != SyntaxKind.EqualsToken)
            return BindCompoundAssignment(node, diagnostics);

        var left = BindExpressionInternal(node.left, diagnostics, false, false);
        return BindSimpleAssignmentWithUncheckedBoundLeft(node, left, diagnostics);
    }

    internal BoundExpression BindDeconstruction(
        AssignmentExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var left = node.left;
        var right = node.right;
        DeclarationExpressionSyntax declaration = null;
        ExpressionSyntax expression = null;
        var result = BindDeconstruction(node, left, right, diagnostics, ref declaration, ref expression);

        if (declaration is not null) {
            switch (node.parent?.kind) {
                case null:
                case SyntaxKind.ExpressionStatement:
                    break;
                default:
                    diagnostics.Push(Error.InvalidDeclarationExpression(declaration.location));
                    break;
            }
        }

        return result;
    }

    internal BoundDeconstructionAssignmentOperator BindDeconstruction(
        BelteSyntaxNode deconstruction,
        ExpressionSyntax left,
        ExpressionSyntax right,
        BelteDiagnosticQueue diagnostics,
        ref DeclarationExpressionSyntax declaration,
        ref ExpressionSyntax expression) {
        var locals = BindDeconstructionVariables(left, diagnostics, ref declaration, ref expression);

        var deconstructionDiagnostics = BelteDiagnosticQueue.GetInstance();
        var boundRight = BindValue(right, deconstructionDiagnostics, BindValueKind.RValue);

        boundRight = FixTupleLiteral(locals.nestedVariables, boundRight, deconstruction, deconstructionDiagnostics);
        boundRight = BindToNaturalType(boundRight, diagnostics);

        var resultIsUsed = IsDeconstructionResultUsed(left);

        var assignment = BindDeconstructionAssignment(
            deconstruction,
            left,
            boundRight,
            locals.nestedVariables,
            resultIsUsed,
            deconstructionDiagnostics
        );

        DeconstructionVariable.FreeDeconstructionVariables(locals.nestedVariables);

        diagnostics.PushRangeAndFree(deconstructionDiagnostics);
        return assignment;
    }

    private BoundDeconstructionAssignmentOperator BindDeconstructionAssignment(
        BelteSyntaxNode node,
        ExpressionSyntax left,
        BoundExpression boundRight,
        ArrayBuilder2<DeconstructionVariable> checkedVariables,
        bool resultIsUsed,
        BelteDiagnosticQueue diagnostics) {
        if (boundRight.type is null || boundRight.type.IsErrorType()) {
            FailRemainingInferences(checkedVariables, diagnostics);
            var voidType = CorLibrary.GetSpecialType(SpecialType.Void);
            var type = boundRight.type ?? voidType;

            return new BoundDeconstructionAssignmentOperator(
                node,
                DeconstructionVariablesAsTuple(left, checkedVariables, diagnostics, ignoreDiagnosticsFromTuple: true),
                new BoundCastExpression(
                    boundRight.syntax,
                    boundRight,
                    Conversion.Deconstruction,
                    constantValue: null,
                    type: type,
                    hasErrors: true
                ),
                resultIsUsed,
                voidType,
                hasErrors: true
            );
        }

        var hasErrors = !MakeDeconstructionConversion(
            boundRight.type,
            node,
            boundRight.syntax,
            diagnostics,
            checkedVariables,
            out var conversion
        );

        // TODO Warning
        // if (conversion.method is not null)
        //     CheckImplicitThisCopyInReadOnlyMember(boundRight, conversion.method, diagnostics);

        FailRemainingInferences(checkedVariables, diagnostics);

        var lhsTuple = DeconstructionVariablesAsTuple(
            left,
            checkedVariables,
            diagnostics,
            ignoreDiagnosticsFromTuple: diagnostics.AnyErrors() || !resultIsUsed
        );

        var returnType = hasErrors ? CreateErrorType() : lhsTuple.type;

        var boundConversion = new BoundCastExpression(
            boundRight.syntax,
            boundRight,
            conversion,
            constantValue: null,
            type: returnType,
            hasErrors: hasErrors
        );

        return new BoundDeconstructionAssignmentOperator(node, lhsTuple, boundConversion, resultIsUsed, returnType);
    }

    private BoundTupleExpression DeconstructionVariablesAsTuple(
        BelteSyntaxNode syntax,
        ArrayBuilder2<DeconstructionVariable> variables,
        BelteDiagnosticQueue diagnostics,
        bool ignoreDiagnosticsFromTuple) {
        var count = variables.Count;
        var valuesBuilder = ArrayBuilder<BoundExpression>.GetInstance(count);
        var typesWithAnnotationsBuilder = ArrayBuilder<TypeWithAnnotations>.GetInstance(count);
        var locationsBuilder = ArrayBuilder<TextLocation>.GetInstance(count);
        var namesBuilder = ArrayBuilder<string>.GetInstance(count);

        foreach (var variable in variables) {
            BoundExpression value;
            if (variable.nestedVariables is not null) {
                value = DeconstructionVariablesAsTuple(
                    variable.syntax,
                    variable.nestedVariables,
                    diagnostics,
                    ignoreDiagnosticsFromTuple
                );

                namesBuilder.Add(null);
            } else {
                value = variable.single;
                namesBuilder.Add(null);
            }

            valuesBuilder.Add(value);
            typesWithAnnotationsBuilder.Add(new TypeWithAnnotations(value.type));
            locationsBuilder.Add(variable.syntax.location);
        }

        var arguments = valuesBuilder.ToImmutableAndFree();

        var uniqueFieldNames = PooledHashSet<string>.GetInstance();
        // RemoveDuplicateInferredTupleNamesAndFreeIfEmptied(ref namesBuilder, uniqueFieldNames);
        uniqueFieldNames.Free();

        var tupleNames = namesBuilder is null ? default : namesBuilder.ToImmutableAndFree();

        var type = NamedTypeSymbol.CreateTuple(
            syntax.location,
            typesWithAnnotationsBuilder.ToImmutableAndFree(),
            locationsBuilder.ToImmutableAndFree(),
            tupleNames,
            compilation,
            shouldCheckConstraints: !ignoreDiagnosticsFromTuple,
            errorPositions: default,
            syntax: syntax,
            diagnostics: ignoreDiagnosticsFromTuple ? null : diagnostics
        );

        return (BoundTupleExpression)BindToNaturalType(
            new BoundTupleLiteral(syntax, arguments, type),
            diagnostics
        );
    }

    private bool MakeDeconstructionConversion(
        TypeSymbol type,
        SyntaxNode syntax,
        SyntaxNode rightSyntax,
        BelteDiagnosticQueue diagnostics,
        ArrayBuilder2<DeconstructionVariable> variables,
        out Conversion conversion) {
        ImmutableArray<TypeSymbol> tupleOrDeconstructedTypes;
        conversion = Conversion.Deconstruction;
        var deconstructMethod = default(DeconstructMethodInfo);

        if (type.isTupleType) {
            tupleOrDeconstructedTypes = type.tupleElementTypes.SelectAsArray(t => t.type.type);
            SetInferredTypes(variables, tupleOrDeconstructedTypes, diagnostics);

            if (variables.Count != tupleOrDeconstructedTypes.Length) {
                diagnostics.Push(Error.DeconstructWrongCardinality(
                    syntax.location,
                    tupleOrDeconstructedTypes.Length,
                    variables.Count
                ));

                return false;
            }
        } else {
            if (variables.Count < 2) {
                throw ExceptionUtilities.Unreachable();
                // diagnostics.Push(Error.DeconstructTooFewElements(syntax.location));
                // return false;
            }

            var inputPlaceholder = new BoundValuePlaceholder(syntax, type);
            var deconstructInvocation = MakeDeconstructInvocationExpression(
                variables.Count,
                inputPlaceholder,
                rightSyntax,
                diagnostics,
                variables,
                out var placeholders
            );

            if (deconstructInvocation.hasAnyErrors)
                return false;

            deconstructMethod = new DeconstructMethodInfo(deconstructInvocation, inputPlaceholder, placeholders);

            tupleOrDeconstructedTypes = placeholders.SelectAsArray(p => p.type);
            SetInferredTypes(variables, tupleOrDeconstructedTypes, diagnostics);
        }

        var hasErrors = false;
        var count = variables.Count;
        var nestedConversions = ArrayBuilder<(BoundValuePlaceholder?, BoundExpression?)>.GetInstance(count);

        for (var i = 0; i < count; i++) {
            var variable = variables[i];

            Conversion nestedConversion;

            if (variable.nestedVariables is not null) {
                var elementSyntax = syntax.kind == SyntaxKind.TupleExpression
                    ? ((TupleExpressionSyntax)syntax).arguments[i]
                    : syntax;

                hasErrors |= !MakeDeconstructionConversion(
                    tupleOrDeconstructedTypes[i],
                    elementSyntax,
                    rightSyntax,
                    diagnostics,
                    variable.nestedVariables,
                    out nestedConversion
                );

                var operandPlaceholder = new BoundValuePlaceholder(syntax, ErrorTypeSymbol.UnknownResultType);

                nestedConversions.Add((
                    operandPlaceholder,
                    new BoundCastExpression(
                        syntax,
                        operandPlaceholder,
                        nestedConversion,
                        constantValue: null,
                        type: ErrorTypeSymbol.UnknownResultType
                    )
                ));
            } else {
                var single = variable.single;

                nestedConversion = conversions.ClassifyConversionFromType(
                    tupleOrDeconstructedTypes[i],
                    single.type
                );

                if (!nestedConversion.isImplicit) {
                    hasErrors = true;

                    GenerateImplicitConversionError(
                        diagnostics,
                        single.syntax,
                        nestedConversion,
                        tupleOrDeconstructedTypes[i],
                        single.type
                    );

                    nestedConversions.Add((null, null));
                } else {
                    var operandPlaceholder = new BoundValuePlaceholder(syntax, tupleOrDeconstructedTypes[i]);
                    nestedConversions.Add((
                        operandPlaceholder,
                        CreateConversion(
                            syntax,
                            operandPlaceholder,
                            nestedConversion,
                            isCast: false,
                            single.type,
                            diagnostics
                        )
                    ));
                }
            }
        }

        conversion = new Conversion(
            ConversionKind.Deconstruction,
            deconstructMethod,
            nestedConversions.ToImmutableAndFree()
        );

        return !hasErrors;
    }

    private BoundExpression MakeDeconstructInvocationExpression(
        int numCheckedVariables,
        BoundExpression receiver,
        SyntaxNode rightSyntax,
        BelteDiagnosticQueue diagnostics,
        ArrayBuilder2<DeconstructionVariable> variables,
        out ImmutableArray<BoundValuePlaceholder> placeholders) {
        var receiverSyntax = (BelteSyntaxNode)receiver.syntax;
        receiver = BindToNaturalType(receiver, diagnostics);
        placeholders = default;

        if (receiver.type is not NamedTypeSymbol namedType)
            return MissingDeconstruct(receiver, rightSyntax, numCheckedVariables, diagnostics, null);

        var candidates = namedType.GetOperators(WellKnownMemberNames.ImplicitConversionName)
            .WhereAsArray(o => o.returnType.IsTupleTypeOfCardinality(numCheckedVariables));

        if (candidates.Length == 0)
            return MissingDeconstruct(receiver, rightSyntax, numCheckedVariables, diagnostics, null);

        if (candidates.Length == 1) {
            var method = candidates[0];
            CheckCandidate(receiverSyntax, method, null, variables, out placeholders);

            return new BoundCallExpression(
                rightSyntax,
                receiver,
                method,
                [],
                [],
                default,
                LookupResultKind.Viable,
                method.returnType
            );
        }

        var filteredCandidatesBuilder = ArrayBuilder<MethodSymbol>.GetInstance(candidates.Length);

        foreach (var candidate in candidates)
            CheckCandidate(receiverSyntax, candidate, filteredCandidatesBuilder, variables, out _);

        var filteredCandidates = filteredCandidatesBuilder.ToImmutableAndFree();

        if (filteredCandidates.Length == 1) {
            var method = filteredCandidates[0];
            CheckCandidate(receiverSyntax, method, null, variables, out placeholders);

            return new BoundCallExpression(
                rightSyntax,
                receiver,
                method,
                [],
                [],
                default,
                LookupResultKind.Viable,
                method.returnType
            );
        }

        diagnostics.Push(Error.AmbiguousDeconstruct(rightSyntax.location, namedType));

        return ErrorExpression(rightSyntax, receiver);

        void CheckCandidate(
            SyntaxNode syntax,
            MethodSymbol candidate,
            ArrayBuilder<MethodSymbol> builder,
            ArrayBuilder2<DeconstructionVariable> variables,
            out ImmutableArray<BoundValuePlaceholder> placeholders) {
            var elementTypes = candidate.returnType.tupleElementTypes;
            var failed = false;
            var placeholdersBuilder = ArrayBuilder<BoundValuePlaceholder>.GetInstance(elementTypes.Length);

            for (var i = 0; i < elementTypes.Length; i++) {
                var type = variables[i].single.type;
                var placeholder = new BoundValuePlaceholder(syntax, elementTypes[i].type.type);
                placeholdersBuilder.Add(placeholder);

                if (type is null || type.IsErrorType())
                    continue;

                var conversion = conversions.ClassifyImplicitConversionFromType(type, placeholder.type);

                if (!conversion.exists)
                    failed = true;
            }

            if (!failed)
                builder?.Add(candidate);

            placeholders = placeholdersBuilder.ToImmutableAndFree();
        }
    }

    private BoundExpression MissingDeconstruct(
        BoundExpression receiver,
        SyntaxNode rightSyntax,
        int numParameters,
        BelteDiagnosticQueue diagnostics,
        BoundExpression childNode) {
        if (receiver.type?.IsErrorType() == false)
            diagnostics.Push(Error.MissingDeconstruct(rightSyntax.location, receiver.type, numParameters));

        return ErrorExpression(rightSyntax, childNode);
    }

    private void FailRemainingInferences(
        ArrayBuilder2<DeconstructionVariable> variables,
        BelteDiagnosticQueue diagnostics) {
        var count = variables.Count;

        for (var i = 0; i < count; i++) {
            var variable = variables[i];

            if (variable.nestedVariables is object) {
                FailRemainingInferences(variable.nestedVariables, diagnostics);
            } else {
                switch (variable.single.kind) {
                    case BoundKind.DeconstructionVariablePendingInference:
                        var errorLocal = ((DeconstructionVariablePendingInference)variable.single)
                            .FailInference(this, diagnostics);

                        variables[i] = new DeconstructionVariable(errorLocal, errorLocal.syntax);
                        break;
                    case BoundKind.DiscardExpression:
                        var pending = (BoundDiscardExpression)variable.single;

                        if (pending.type is null) {
                            diagnostics.Push(Error.TypeInferenceFailedForDeconstruction(pending.syntax.location, "_"));
                            variables[i] = new DeconstructionVariable(
                                pending.FailInference(this, diagnostics),
                                pending.syntax
                            );
                        }

                        break;
                }
            }
        }
    }

    private static bool IsDeconstructionResultUsed(ExpressionSyntax left) {
        var parent = left.parent;

        if (parent is null /*|| parent.Kind() == SyntaxKind.ForEachVariableStatement*/)
            return false;

        var grandParent = parent.parent;

        if (grandParent is null)
            return false;

        switch (grandParent.kind) {
            case SyntaxKind.ExpressionStatement:
                return ((ExpressionStatementSyntax)grandParent).expression != parent;
            // case SyntaxKind.ForStatement:
            //     // Incrementors and Initializers don't have to produce a value
            //     var loop = (ForStatementSyntax)grandParent;
            //     return !loop.Incrementors.Contains(parent) && !loop.Initializers.Contains(parent);
            default:
                return true;
        }
    }

    private BoundExpression FixTupleLiteral(
        ArrayBuilder2<DeconstructionVariable> checkedVariables,
        BoundExpression boundRight,
        BelteSyntaxNode syntax,
        BelteDiagnosticQueue diagnostics) {
        if (boundRight.kind == BoundKind.TupleLiteral) {
            var hadErrors = diagnostics.AnyErrors();
            var mergedTupleType = MakeMergedTupleType(
                checkedVariables,
                (BoundTupleLiteral)boundRight,
                syntax,
                hadErrors ? null : diagnostics
            );

            if (mergedTupleType is not null)
                boundRight = GenerateConversionForAssignment(mergedTupleType, boundRight, diagnostics);
        } else if (boundRight.type is null) {
            diagnostics.Push(Error.DeconstructRequiresExpression(boundRight.syntax.location));
        }

        return boundRight;
    }

    private TypeSymbol MakeMergedTupleType(
        ArrayBuilder2<DeconstructionVariable> lhsVariables,
        BoundTupleLiteral rhsLiteral,
        BelteSyntaxNode syntax,
        BelteDiagnosticQueue diagnostics) {
        var leftLength = lhsVariables.Count;
        var rightLength = rhsLiteral.arguments.Length;

        var typesWithAnnotationsBuilder = ArrayBuilder<TypeWithAnnotations>.GetInstance(leftLength);
        var locationsBuilder = ArrayBuilder<TextLocation>.GetInstance(leftLength);

        for (var i = 0; i < rightLength; i++) {
            var element = rhsLiteral.arguments[i];
            var mergedType = element.type;

            if (i < leftLength) {
                var variable = lhsVariables[i];

                if (variable.nestedVariables is object) {
                    if (element.kind == BoundKind.TupleLiteral) {
                        mergedType = MakeMergedTupleType(
                            variable.nestedVariables,
                            (BoundTupleLiteral)element,
                            syntax,
                            diagnostics
                        );
                    } else if (mergedType is null && diagnostics is not null) {
                        // TODO reachable err?
                        throw ExceptionUtilities.Unreachable();
                        // (variables) on the left and null on the right
                        // Error(diagnostics, ErrorCode.ERR_DeconstructRequiresExpression, element.Syntax);
                    }
                } else {
                    if (variable.single.type is not null)
                        mergedType = variable.single.type;
                }
            } else {
                if (mergedType is null && diagnostics is not null) {
                    // TODO reachable err?
                    throw ExceptionUtilities.Unreachable();
                    // a typeless element on the right, matching no variable on the left
                    // Error(diagnostics, ErrorCode.ERR_DeconstructRequiresExpression, element.Syntax);
                }
            }

            typesWithAnnotationsBuilder.Add(new TypeWithAnnotations(mergedType));
            locationsBuilder.Add(element.syntax.location);
        }

        if (typesWithAnnotationsBuilder.Any(t => !t.hasType)) {
            typesWithAnnotationsBuilder.Free();
            locationsBuilder.Free();
            return null;
        }

        return NamedTypeSymbol.CreateTuple(
            location: null,
            elementTypesWithAnnotations: typesWithAnnotationsBuilder.ToImmutableAndFree(),
            elementLocations: locationsBuilder.ToImmutableAndFree(),
            elementNames: default,
            compilation: compilation,
            diagnostics: diagnostics,
            shouldCheckConstraints: true,
            errorPositions: default,
            syntax: syntax
        );
    }

    private DeconstructionVariable BindDeconstructionVariables(
        ExpressionSyntax node,
        BelteDiagnosticQueue diagnostics,
        ref DeclarationExpressionSyntax declaration,
        ref ExpressionSyntax expression) {
        switch (node.kind) {
            case SyntaxKind.DeclarationExpression: {
                    var component = (DeclarationExpressionSyntax)node;

                    declaration ??= component;

                    var isConst = false;
                    var isConstExpr = false;
                    // TODO Use the out info?
                    var declType = BindVariableTypeWithAnnotations(
                        component,
                        diagnostics,
                        component.type.SkipRef(out _),
                        ref isConst,
                        ref isConstExpr,
                        out var isImplicitlyTyped,
                        out var isNonNullable,
                        out var isNullable,
                        out var alias
                    );

                    return BindDeconstructionVariables(declType, component, component, diagnostics);
                }
            case SyntaxKind.TupleExpression: {
                    var component = (TupleExpressionSyntax)node;
                    var builder = ArrayBuilder2<DeconstructionVariable>.GetInstance(component.arguments.Count);

                    foreach (var arg in component.arguments)
                        builder.Add(BindDeconstructionVariables(arg, diagnostics, ref declaration, ref expression));

                    return new DeconstructionVariable(builder, node);
                }
            default:
                var boundVariable = BindExpression(node, diagnostics);
                var checkedVariable = CheckValue(boundVariable, BindValueKind.Assignable, diagnostics);

                if (expression is null && checkedVariable.kind != BoundKind.DiscardExpression)
                    expression = node;

                return new DeconstructionVariable(checkedVariable, node);
        }
    }

    private DeconstructionVariable BindDeconstructionVariables(
        TypeWithAnnotations declTypeWithAnnotations,
        DeclarationExpressionSyntax node,
        BelteSyntaxNode syntax,
        BelteDiagnosticQueue diagnostics) {
        return new DeconstructionVariable(
            BindDeconstructionVariable(declTypeWithAnnotations, node, syntax, diagnostics),
            syntax
        );
    }

    private BoundExpression BindSimpleAssignmentWithUncheckedBoundLeft(
        AssignmentExpressionSyntax node,
        BoundExpression left,
        BelteDiagnosticQueue diagnostics) {
        var rhsExpr = node.right.UnwrapRefExpression(out var refKind);
        var isRef = refKind == RefKind.Ref;
        var lhsKind = isRef ? BindValueKind.RefAssignable : BindValueKind.Assignable;
        var op1 = CheckValue(left, lhsKind, diagnostics);
        var rhsKind = isRef ? GetRequiredRHSValueKindForRefAssignment(op1) : BindValueKind.RValue;
        var op2 = BindPossibleArrayInitializer(rhsExpr, op1.Type(), rhsKind, diagnostics);

        if (op1.kind == BoundKind.DiscardExpression) {
            op2 = BindToNaturalType(op2, diagnostics);
            op1 = InferTypeForDiscardAssignment((BoundDiscardExpression)op1, op2, diagnostics);
        } else {
            op2 = ReduceNumericIfApplicable(op1.Type(), op2);
        }

        return BindAssignment(node, op1, op2, isRef, diagnostics);
    }

    private BoundExpression InferTypeForDiscardAssignment(
        BoundDiscardExpression op1,
        BoundExpression op2,
        BelteDiagnosticQueue diagnostics) {
        var inferredType = op2.type;
        if (inferredType is null)
            return op1.FailInference(this, diagnostics);

        if (inferredType.IsVoidType())
            diagnostics.Push(Error.VoidAssignment(op1.syntax.location));

        return op1.SetInferredTypeWithAnnotations(new TypeWithAnnotations(inferredType));
    }

    private BoundExpression BindNullCoalescingCompoundAssignment(
        AssignmentExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var leftOperand = BindValue(node.left, diagnostics, BindValueKind.CompoundAssignment);
        return BindNullCoalescingCompoundAssignmentWithBoundLeft(node, leftOperand, diagnostics);
    }

    private BoundExpression BindNullCoalescingCompoundAssignmentWithBoundLeft(
        AssignmentExpressionSyntax node,
        BoundExpression leftOperand,
        BelteDiagnosticQueue diagnostics) {
        var rightOperand = BindValue(node.right, diagnostics, BindValueKind.RValue);
        var isPropagation = node.assignmentToken.kind == SyntaxKind.QuestionExclamationEqualsToken;

        if (leftOperand.hasAnyErrors || rightOperand.hasAnyErrors) {
            leftOperand = BindToTypeForErrorRecovery(leftOperand);
            rightOperand = BindToTypeForErrorRecovery(rightOperand);

            return new BoundNullCoalescingAssignmentOperator(
                node,
                leftOperand,
                rightOperand,
                isPropagation,
                CreateErrorType(),
                true
            );
        }

        var leftType = leftOperand.Type();

        if (!leftType.IsNullableType()) {
            return GenerateNullCoalescingAssignmentBadBinaryOpsError(
                node,
                leftOperand,
                rightOperand,
                isPropagation,
                diagnostics
            );
        }

        var underlyingLeftType = isPropagation ? leftType : leftType.GetNullableUnderlyingType();
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

            return new BoundNullCoalescingAssignmentOperator(
                node,
                leftOperand,
                convertedRightOperand,
                isPropagation,
                underlyingLeftType
            );
        }

        var rightConversion = conversions.ClassifyImplicitConversionFromExpression(rightOperand, leftType);

        if (rightConversion.exists) {
            var convertedRightOperand = CreateConversion(rightOperand, rightConversion, leftType, diagnostics);

            return new BoundNullCoalescingAssignmentOperator(
                node,
                leftOperand,
                convertedRightOperand,
                isPropagation,
                leftType
            );
        }

        return GenerateNullCoalescingAssignmentBadBinaryOpsError(
            node,
            leftOperand,
            rightOperand,
            isPropagation,
            diagnostics
        );
    }

    private BoundExpression GenerateNullCoalescingAssignmentBadBinaryOpsError(
        AssignmentExpressionSyntax node,
        BoundExpression leftOperand,
        BoundExpression rightOperand,
        bool isPropagation,
        BelteDiagnosticQueue diagnostics) {
        diagnostics.Push(Error.InvalidBinaryOperatorUse(
            node.location,
            SyntaxFacts.GetText(node.assignmentToken.kind),
            leftOperand.Type(),
            rightOperand.Type()
        ));

        leftOperand = BindToTypeForErrorRecovery(leftOperand);
        rightOperand = BindToTypeForErrorRecovery(rightOperand);

        return new BoundNullCoalescingAssignmentOperator(
            node,
            leftOperand,
            rightOperand,
            isPropagation,
            CreateErrorType(),
            true
        );
    }

    private BoundExpression BindCompoundAssignment(AssignmentExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var left = BindValue(node.left, diagnostics, GetBinaryAssignmentKind(node.assignmentToken.kind));
        return BindCompoundAssignmentWithBoundLeft(node, left, diagnostics);
    }

    private BoundExpression BindCompoundAssignmentWithBoundLeft(
        AssignmentExpressionSyntax node,
        BoundExpression left,
        BelteDiagnosticQueue diagnostics) {
        var right = BindValue(node.right, diagnostics, BindValueKind.RValue);
        var kind = SyntaxKindToBinaryOperatorKind(node.assignmentToken.kind);

        if (left.hasAnyErrors || right.hasAnyErrors) {
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
                Error.InvalidBinaryOperatorUse(node.location, node.assignmentToken.text, left.Type(), right.Type())
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
        var leftType = left.Type();
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
        var hasErrors = op1.hasAnyErrors || op2.hasAnyErrors;

        if (op1.Type() is { } lhsType && !lhsType.IsErrorType()) {
            if (op1.hasAnyErrors)
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

        var type = op1.Type();

        return new BoundAssignmentOperator(node, op1, op2, isRef, type);
    }

    private static BindValueKind GetRequiredRHSValueKindForRefAssignment(BoundExpression boundLeft) {
        var rhsKind = BindValueKind.RefersToLocation;
        var lhsRefKind = boundLeft.GetRefKind();

        if (lhsRefKind is RefKind.Ref or RefKind.Out)
            rhsKind |= BindValueKind.Assignable;

        return rhsKind;
    }

    #endregion

    #region Lookup

    internal void AddLookupSymbolsInfo(LookupSymbolsInfo result, LookupOptions options = LookupOptions.Default) {
        for (var scope = this; scope is not null; scope = scope.next)
            scope.AddLookupSymbolsInfoInSingleBinder(result, options, originalBinder: this);
    }

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

                for (var i = 0; i < symbols.Count; i++)
                    symbols[i] = UnwrapAlias(symbols[i], diagnostics, where);

                var best = GetBestSymbolInfo(symbols, out var secondBest);

                if (best.isFromCompilation && !secondBest.isFromCompilation) {
                    var srcSymbol = symbols[best.index];
                    var mdSymbol = symbols[secondBest.index];

                    object arg0;

                    if (best.isFromSourceModule)
                        arg0 = srcSymbol.location.fileName;
                    else
                        arg0 = srcSymbol.containingModule;

                    if (NameAndArityMatchRecursively(srcSymbol, mdSymbol)) {
                        if (srcSymbol.kind == SymbolKind.Namespace && mdSymbol.kind == SymbolKind.NamedType) {
                            throw ExceptionUtilities.Unreachable();
                            // ErrorCode.WRN_SameFullNameThisNsAgg: The namespace '{1}' in '{0}' conflicts with the imported type '{3}' in '{2}'. Using the namespace defined in '{0}'.
                            // diagnostics.Add(ErrorCode.WRN_SameFullNameThisNsAgg, where.Location, originalSymbols,
                            //     arg0,
                            //     srcSymbol,
                            //     mdSymbol.ContainingAssembly,
                            //     mdSymbol);

                            // return originalSymbols[best.Index];
                        } else if (srcSymbol.kind == SymbolKind.NamedType && mdSymbol.kind == SymbolKind.Namespace) {
                            throw ExceptionUtilities.Unreachable();
                            // ErrorCode.WRN_SameFullNameThisAggNs: The type '{1}' in '{0}' conflicts with the imported namespace '{3}' in '{2}'. Using the type defined in '{0}'.
                            // diagnostics.Add(ErrorCode.WRN_SameFullNameThisAggNs, where.Location, originalSymbols,
                            //     arg0,
                            //     srcSymbol,
                            //     GetContainingAssembly(mdSymbol),
                            //     mdSymbol);

                            // return originalSymbols[best.Index];
                        } else if (srcSymbol.kind == SymbolKind.NamedType && mdSymbol.kind == SymbolKind.NamedType) {
                            throw ExceptionUtilities.Unreachable();
                            // WRN_SameFullNameThisAggAgg: The type '{1}' in '{0}' conflicts with the imported type '{3}' in '{2}'. Using the type defined in '{0}'.
                            // diagnostics.Add(ErrorCode.WRN_SameFullNameThisAggAgg, where.Location, originalSymbols,
                            //     arg0,
                            //     srcSymbol,
                            //     mdSymbol.ContainingAssembly,
                            //     mdSymbol);

                            // return originalSymbols[best.Index];
                        }
                    }
                }

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
                            error = Error.SameFullNameAggAgg(
                                first.containingAssembly,
                                first,
                                second.containingAssembly
                            );

                            if (secondBest.isFromAddedModule)
                                reportError = false;
                        }
                    } else if (first.kind == SymbolKind.Namespace && second.kind == SymbolKind.NamedType) {
                        // TODO is this a reachable error?
                        throw ExceptionUtilities.Unreachable();
                        // ErrorCode.ERR_SameFullNameNsAgg: The namespace '{1}' in '{0}' conflicts with the type '{3}' in '{2}'
                        // info = new CSDiagnosticInfo(ErrorCode.ERR_SameFullNameNsAgg, originalSymbols,
                        //     new object[] { GetContainingAssembly(first), first, second.ContainingAssembly, second });

                        // Do not report this error if namespace is declared in source and the type is declared in added module,
                        // we already reported declaration error about this name collision.
                        // if (best.isFromSourceModule && secondBest.isFromAddedModule)
                        //     reportError = false;
                    } else if (first.kind == SymbolKind.NamedType && second.kind == SymbolKind.Namespace) {
                        if (!secondBest.isFromCompilation || secondBest.isFromSourceModule) {
                            // TODO is this a reachable error?
                            throw ExceptionUtilities.Unreachable();
                            // ErrorCode.ERR_SameFullNameNsAgg: The namespace '{1}' in '{0}' conflicts with the type '{3}' in '{2}'
                            // info = new CSDiagnosticInfo(ErrorCode.ERR_SameFullNameNsAgg, originalSymbols,
                            //     new object[] { GetContainingAssembly(second), second, first.ContainingAssembly, first });
                        } else {
                            // TODO is this a reachable error?
                            throw ExceptionUtilities.Unreachable();
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
                if (singleResult is TypeSymbol t && t.IsVoidType())
                    throw ExceptionUtilities.Unreachable();

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
            string aliasOpt = null;
            var node = where;

            while (node is ExpressionSyntax) {
                if (node.kind == SyntaxKind.AliasQualifiedName) {
                    aliasOpt = ((AliasQualifiedNameSyntax)node).alias.identifier.valueText;
                    break;
                }

                node = node.parent;
            }

            var error = NotFound(
                where,
                simpleName,
                arity,
                (where as NameSyntax)?.ErrorDisplayName() ?? simpleName,
                diagnostics,
                aliasOpt,
                qualifier,
                options
            );

            return new ExtendedErrorTypeSymbol(
                qualifier ?? compilation.globalNamespaceInternal,
                simpleName,
                arity,
                error
            );
        }

        if (result.error is not null && (qualifier is null || qualifier.kind != SymbolKind.ErrorType))
            diagnostics.Push(result.error);

        if ((symbols.Count > 1) || (symbols[0] is NamespaceOrTypeSymbol) || result.kind == LookupResultKind.NotATypeOrNamespace) {
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
        TextLocation errorLocation,
        ConsList<TypeSymbol> basesBeingResolved = null,
        LookupOptions options = LookupOptions.Default) {
        var binder = LookupSymbolsInternal(result, name, arity, basesBeingResolved, options, errorLocation, false);

        if (result.kind != LookupResultKind.Viable && result.kind != LookupResultKind.Empty) {
            result.Clear();
            LookupSymbolsInternal(result, name, arity, basesBeingResolved, options, errorLocation, true);
        }

        return binder;
    }

    private protected void LookupSymbolInAliases(
        ImmutableDictionary<string, AliasAndUsingDirective> usingAliases,
        Binder originalBinder,
        LookupResult result,
        string name,
        int arity,
        TextLocation errorLocation,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        bool diagnose) {
        if (usingAliases.TryGetValue(name, out var alias)) {
            var res = originalBinder.CheckViability(
                alias.alias,
                arity,
                options,
                null,
                diagnose,
                errorLocation,
                basesBeingResolved
            );

            if (res.kind == LookupResultKind.Viable)
                MarkImportDirective(alias.usingDirectiveReference);

            result.MergeEqual(res);
        }
    }

    private protected bool IsUsingAlias(ImmutableDictionary<string, AliasAndUsingDirective> usingAliases, string name) {
        if (usingAliases.TryGetValue(name, out var node)) {
            MarkImportDirective(node.usingDirectiveReference);
            return true;
        }

        return false;
    }

    private protected void AddLookupSymbolsInfoInAliases(
        ImmutableDictionary<string, AliasAndUsingDirective> usingAliases,
        LookupSymbolsInfo result,
        LookupOptions options,
        Binder originalBinder) {
        foreach (var pair in usingAliases) {
            var aliasSymbol = pair.Value.alias;
            var targetSymbol = aliasSymbol.GetAliasTarget(basesBeingResolved: null);

            if (originalBinder.CanAddLookupSymbolInfo(
                targetSymbol,
                options,
                result,
                accessThroughType: null,
                aliasSymbol: aliasSymbol)) {
                result.AddSymbol(aliasSymbol, aliasSymbol.name, 0);
            }
        }
    }

    internal void LookupSymbolsSimpleName(
        LookupResult result,
        NamespaceOrTypeSymbol qualifier,
        string plainName,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        TextLocation errorLocation,
        bool diagnose) {
        if (qualifier is null) {
            LookupSymbolsInternal(result, plainName, arity, basesBeingResolved, options, errorLocation, diagnose);
        } else {
            LookupMembersInternal(
                result,
                qualifier,
                plainName,
                arity,
                basesBeingResolved,
                options,
                this,
                errorLocation,
                diagnose
            );
        }
    }

    private void LookupMembersWithFallback(
        LookupResult result,
        NamespaceOrTypeSymbol namespaceOrType,
        string name,
        int arity,
        TextLocation errorLocation,
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
            errorLocation,
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
                errorLocation,
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
        TextLocation errorLocation,
        bool diagnose) {
        if (namespaceOrType.isNamespace) {
            LookupMembersInNamespace(
                result,
                (NamespaceSymbol)namespaceOrType,
                name,
                arity,
                options,
                errorLocation,
                originalBinder,
                diagnose
            );
        } else {
            LookupMembersInType(
                result,
                ((TypeSymbol)namespaceOrType).StrippedType(),
                name,
                arity,
                basesBeingResolved,
                options,
                originalBinder,
                errorLocation,
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
        TextLocation errorLocation,
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
                    errorLocation,
                    diagnose
                );

                break;
            case TypeKind.Interface:
                LookupMembersInInterface(
                    result,
                    (NamedTypeSymbol)type,
                    name,
                    arity,
                    basesBeingResolved,
                    options,
                    originalBinder,
                    errorLocation,
                    diagnose
                );

                break;
            case TypeKind.Class:
            case TypeKind.Struct:
            case TypeKind.Enum:
            case TypeKind.Array:
                LookupMembersInClass(
                    result,
                    type,
                    name,
                    arity,
                    basesBeingResolved,
                    options,
                    originalBinder,
                    errorLocation,
                    diagnose
                );

                break;
            case TypeKind.Error:
                LookupMembersInErrorType(
                    result,
                    (ErrorTypeSymbol)type.originalDefinition,
                    name,
                    arity,
                    basesBeingResolved,
                    options,
                    originalBinder,
                    errorLocation,
                    diagnose
                );

                break;
            case TypeKind.Pointer:
            case TypeKind.FunctionPointer:
            case TypeKind.Function:
                result.Clear();
                break;
            case TypeKind.Primitive:
                result.MergeEqual(LookupResult.NotTypeOrNamespace(type, Error.PrimitivesDoNotHaveMembers(null)));
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
        TextLocation errorLocation,
        bool diagnose) {
        LookupMembersInClass(
            result,
            type,
            name,
            arity,
            basesBeingResolved,
            options,
            originalBinder,
            type,
            errorLocation,
            diagnose
        );
    }

    private void LookupMembersInTemplateParameter(
        LookupResult current,
        TemplateParameterSymbol templateParameter,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        Binder originalBinder,
        TextLocation errorLocation,
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
            errorLocation,
            diagnose
        );
    }

    private void LookupMembersInInterface(
        LookupResult current,
        NamedTypeSymbol type,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        Binder originalBinder,
        TextLocation errorLocation,
        bool diagnose) {
        LookupMembersInInterfaceOnly(
            current,
            type,
            name,
            arity,
            basesBeingResolved,
            options,
            originalBinder,
            errorLocation,
            type,
            diagnose
        );

        var tmp = LookupResult.GetInstance();

        LookupMembersInClass(
            tmp,
            CorLibrary.GetSpecialType(SpecialType.Object),
            name,
            arity,
            basesBeingResolved,
            options,
            originalBinder,
            type.location,
            diagnose
        );

        MergeHidingLookupResults(current, tmp, basesBeingResolved);
        tmp.Free();
    }

    private void LookupMembersInInterfaceOnly(
        LookupResult current,
        NamedTypeSymbol type,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        Binder originalBinder,
        TextLocation errorLocation,
        TypeSymbol accessThroughType,
        bool diagnose) {
        LookupMembersWithoutInheritance(
            current,
            type,
            name,
            arity,
            options,
            originalBinder,
            errorLocation,
            accessThroughType,
            diagnose,
            basesBeingResolved
        );

        if ((options & LookupOptions.NamespaceAliasesOnly) == 0 &&
            ((options & LookupOptions.NamespacesOrTypesOnly) == 0 ||
                !(current.isMultiViable &&
                    TypeSymbol.Equals(
                        current.singleSymbolOrDefault.containingType,
                        type,
                        TypeCompareKind.AllIgnoreOptions)))) {
            LookupMembersInInterfacesWithoutInheritance(
                current,
                GetBaseInterfaces(type, basesBeingResolved),
                name,
                arity,
                basesBeingResolved,
                options,
                originalBinder,
                errorLocation,
                accessThroughType,
                diagnose
            );
        }
    }

    private static ImmutableArray<NamedTypeSymbol> GetBaseInterfaces(
        NamedTypeSymbol type,
        ConsList<TypeSymbol> basesBeingResolved) {
        // TODO
        throw ExceptionUtilities.Unreachable();
        // if (basesBeingResolved?.Any() != true) {
        //     return type.AllInterfacesWithDefinitionUseSiteDiagnostics(ref useSiteInfo);
        // }

        // if (basesBeingResolved.ContainsReference(type.originalDefinition))
        //     return [];

        // var interfaces = type.GetDeclaredInterfaces(basesBeingResolved);

        // if (interfaces.IsEmpty)
        //     return [];

        // var cycleGuard = ConsList<NamedTypeSymbol>.Empty.Prepend(type.originalDefinition);

        // var result = ArrayBuilder<NamedTypeSymbol>.GetInstance();
        // var visited = new HashSet<NamedTypeSymbol>(SymbolEqualityComparer.ConsiderEverything);

        // for (int i = interfaces.Length - 1; i >= 0; i--)
        //     AddAllInterfaces(interfaces[i], visited, result, basesBeingResolved, cycleGuard);

        // result.ReverseContents();
        // return result.ToImmutableAndFree();

        // static void AddAllInterfaces(
        //     NamedTypeSymbol @interface,
        //     HashSet<NamedTypeSymbol> visited,
        //     ArrayBuilder<NamedTypeSymbol> result,
        //     ConsList<TypeSymbol> basesBeingResolved,
        //     ConsList<NamedTypeSymbol> cycleGuard) {
        //     NamedTypeSymbol originalDefinition;

        //     if (@interface.isInterface &&
        //         !cycleGuard.ContainsReference(originalDefinition = @interface.originalDefinition) &&
        //         visited.Add(@interface)) {
        //         if (!basesBeingResolved.ContainsReference(originalDefinition)) {
        //             var baseInterfaces = @interface.GetDeclaredInterfaces(basesBeingResolved);

        //             if (!baseInterfaces.IsEmpty) {
        //                 cycleGuard = cycleGuard.Prepend(originalDefinition);

        //                 for (var i = baseInterfaces.Length - 1; i >= 0; i--) {
        //                     var baseInterface = baseInterfaces[i];
        //                     AddAllInterfaces(baseInterface, visited, result, basesBeingResolved, cycleGuard);
        //                 }
        //             }
        //         }

        //         result.Add(@interface);
        //     }
        // }
    }

    private void LookupMembersInInterfacesWithoutInheritance(
        LookupResult current,
        ImmutableArray<NamedTypeSymbol> interfaces,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        Binder originalBinder,
        TextLocation errorLocation,
        TypeSymbol accessThroughType,
        bool diagnose) {
        if (interfaces.Length > 0) {
            var tmp = LookupResult.GetInstance();
            HashSet<NamedTypeSymbol> seenInterfaces = null;
            if (interfaces.Length > 1)
                seenInterfaces = new HashSet<NamedTypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var baseInterface in interfaces) {
                if (seenInterfaces is null || seenInterfaces.Add(baseInterface)) {
                    LookupMembersWithoutInheritance(
                        tmp,
                        baseInterface,
                        name,
                        arity,
                        options,
                        originalBinder,
                        errorLocation,
                        accessThroughType,
                        diagnose,
                        basesBeingResolved
                    );

                    MergeHidingLookupResults(current, tmp, basesBeingResolved);
                    tmp.Clear();
                }
            }

            tmp.Free();
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
        TypeSymbol accessThroughType,
        TextLocation errorLocation,
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
                errorLocation,
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
        TextLocation errorLocation,
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
                errorLocation,
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
        TextLocation errorLocation,
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
                        errorLocation,
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
        TextLocation errorLocation,
        Binder originalBinder,
        bool diagnose) {
        var members = GetCandidateMembers(ns, name, options, originalBinder);

        foreach (var member in members) {
            var resultOfThisMember = originalBinder.CheckViability(
                member,
                arity,
                options,
                null,
                diagnose,
                errorLocation
            );

            result.MergeEqual(resultOfThisMember);
        }
    }

    internal static ImmutableArray<Symbol> GetCandidateMembers(
        NamespaceOrTypeSymbol nsOrType,
        string name,
        LookupOptions options,
        Binder originalBinder) {
        if ((options & LookupOptions.NamespacesOrTypesOnly) != 0 && nsOrType is TypeSymbol)
            return nsOrType.StrippedTypeOrSelf().GetTypeMembers(name).Cast<NamedTypeSymbol, Symbol>();
        else
            return nsOrType.StrippedTypeOrSelf().GetMembers(name);
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

    private TokenSymbol LookupTokenByName(string name) {
        Binder binder = null;

        for (var scope = this; scope is not null; scope = scope.next) {
            if (binder is not null) {
                var result = scope.LookupTokenInSingleBinder(name);

                if (result is not null)
                    return result;
            } else {
                var result = scope.LookupTokenInSingleBinder(name);

                if (result is null)
                    binder = scope;
                else
                    return result;
            }
        }

        return null;
    }

    private Binder LookupSymbolsInternal(
        LookupResult result,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        TextLocation errorLocation,
        bool diagnose) {
        Binder binder = null;

        for (var scope = this; scope is not null && !result.isMultiViable; scope = scope.next) {
            if (binder is not null) {
                var tmp = LookupResult.GetInstance();

                scope.LookupSymbolsInSingleBinder(
                    tmp,
                    name,
                    arity,
                    basesBeingResolved,
                    options,
                    this,
                    errorLocation,
                    diagnose
                );

                result.MergeEqual(tmp);
                tmp.Free();
            } else {
                scope.LookupSymbolsInSingleBinder(
                    result,
                    name,
                    arity,
                    basesBeingResolved,
                    options,
                    this,
                    errorLocation,
                    diagnose
                );

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
        TextLocation errorLocation,
        bool diagnose) { }

    internal virtual TokenSymbol LookupTokenInSingleBinder(string name) {
        return null;
    }

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

    private protected void AddMemberLookupSymbolsInfoInSubmissions(
        LookupSymbolsInfo result,
        LookupOptions options,
        Binder originalBinder) {
        for (var submission = compilation; submission is not null; submission = submission.previous) {
            if (submission.globalNamespaceInternal is not null) {
                AddMemberLookupSymbolsInfoInNamespace(
                    result,
                    submission.globalNamespaceInternal,
                    options,
                    originalBinder
                );
            }
        }
    }

    private protected TokenSymbol LookupTokenInSubmissions(string name) {
        // TODO If we want to allow using tokens from previous submissions, compilations need to track token statements
        return null;
    }

    private protected void LookupMembersInSubmissions(
        LookupResult result,
        CompilationUnitSyntax declarationSyntax,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        Binder originalBinder,
        TextLocation errorLocation,
        bool diagnose) {
        var submissionSymbols = LookupResult.GetInstance();
        var nonViable = LookupResult.GetInstance();
        SymbolKind? lookingForOverloadsOfKind = null;

        for (var submission = compilation; submission is not null; submission = submission.previous) {
            submissionSymbols.Clear();

            if (submission.globalNamespaceInternal is not null) {
                LookupMembersInNamespace(
                    submissionSymbols,
                    submission.globalNamespaceInternal,
                    name,
                    arity,
                    options,
                    errorLocation,
                    originalBinder,
                    diagnose
                );
            }

            if (lookingForOverloadsOfKind is null) {
                if (!submissionSymbols.isMultiViable) {
                    nonViable.MergePrioritized(submissionSymbols);
                    continue;
                }

                result.MergeEqual(submissionSymbols);

                var firstSymbol = submissionSymbols.symbols.First();

                if (firstSymbol.kind != SymbolKind.Method)
                    break;

                options &= ~(LookupOptions.MustBeInvocableIfMember | LookupOptions.NamespacesOrTypesOnly);
                lookingForOverloadsOfKind = firstSymbol.kind;
            } else {
                if (submissionSymbols.symbols.Count > 0 &&
                    submissionSymbols.symbols.First().kind != lookingForOverloadsOfKind.Value) {
                    break;
                }

                if (submissionSymbols.isMultiViable)
                    result.MergeEqual(submissionSymbols);
            }
        }

        if (result.symbols.Count == 0)
            result.SetFrom(nonViable);

        submissionSymbols.Free();
        nonViable.Free();
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
            case TypeKind.Enum:
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
        TypeSymbol accessThroughType,
        AliasSymbol aliasSymbol = null) {
        var name = aliasSymbol is not null ? aliasSymbol.name : symbol.name;

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
        TextLocation errorLocation,
        ConsList<TypeSymbol> basesBeingResolved = null) {
        var unwrappedSymbol = symbol.kind == SymbolKind.Alias
            ? ((AliasSymbol)symbol).GetAliasTarget(basesBeingResolved)
            : symbol;

        if ((options & LookupOptions.MustNotBeParameter) != 0 && symbol is ParameterSymbol) {
            return LookupResult.Empty();
        } else if (!IsInScopeOfAssociatedSyntaxTree(unwrappedSymbol)) {
            return LookupResult.Empty();
        } else if ((options & (LookupOptions.MustNotBeInstance | LookupOptions.MustBeAbstractOrVirtual)) ==
            (LookupOptions.MustNotBeInstance | LookupOptions.MustBeAbstractOrVirtual) &&
            (unwrappedSymbol is not TypeSymbol && IsInstance(unwrappedSymbol) ||
            !(unwrappedSymbol.isAbstract || unwrappedSymbol.isVirtual))) {
            return LookupResult.Empty();
        } else if (WrongArity(symbol, arity, diagnose, options, errorLocation, out var error)) {
            return LookupResult.WrongArity(symbol, error);
        } else if ((options & LookupOptions.NamespacesOrTypesOnly) != 0 &&
            unwrappedSymbol is not NamespaceOrTypeSymbol) {
            return LookupResult.NotTypeOrNamespace(symbol, symbol, diagnose);
        } else if ((options & LookupOptions.MustBeInvocableIfMember) != 0
              && IsNonInvocableMember(unwrappedSymbol)) {
            return LookupResult.NotInvocable(unwrappedSymbol, symbol, diagnose, errorLocation);
        } else if (!IsAccessible(
            unwrappedSymbol,
            RefineAccessThroughType(options, accessThroughType),
            out var inaccessibleViaQualifier,
            basesBeingResolved)) {
            if (!diagnose)
                error = null;
            else if (inaccessibleViaQualifier)
                error = Error.InvalidProtectedAccess(symbol.location, symbol, accessThroughType, containingType);
            else
                error = Error.MemberIsInaccessible(symbol.location, symbol);

            return LookupResult.Inaccessible(symbol, error);
        } else if ((options & LookupOptions.MustBeInstance) != 0 && !IsInstance(unwrappedSymbol)) {
            error = Error.InstanceRequired(symbol.location, symbol);
            return LookupResult.StaticInstanceMismatch(symbol, error);
        } else if ((options & LookupOptions.MustNotBeInstance) != 0 && IsInstance(unwrappedSymbol)) {
            error = Error.NoInstanceRequired(symbol.location, symbol.name, symbol.containingSymbol);
            return LookupResult.StaticInstanceMismatch(symbol, error);
        } else if ((options & LookupOptions.MustNotBeNamespace) != 0 && unwrappedSymbol.kind == SymbolKind.Namespace) {
            error = diagnose ? Error.BadSKUnknown(symbol.location, unwrappedSymbol, unwrappedSymbol.kind.Localize()) : null;
            return LookupResult.NotTypeOrNamespace(symbol, error);
        } else {
            return LookupResult.Good(symbol);
        }
    }

    private static bool WrongArity(
        Symbol symbol,
        int arity,
        bool diagnose,
        LookupOptions options,
        TextLocation errorLocation,
        out BelteDiagnostic error) {
        switch (symbol.kind) {
            case SymbolKind.NamedType:
                if (arity != 0 || (options & LookupOptions.AllNamedTypesOnArityZero) == 0) {
                    var namedType = (NamedTypeSymbol)symbol;

                    if (namedType.arity != arity) {
                        if (namedType.arity == 0) {
                            error = diagnose
                                ? Error.HasNoTemplate(errorLocation, namedType, MessageID.IDS_SK_TYPE.Localize())
                                : null;
                        } else {
                            error = diagnose
                                ? Error.BadArity(
                                    errorLocation,
                                    namedType,
                                    MessageID.IDS_SK_TYPE.Localize(),
                                    namedType.arity
                                )
                                : null;
                        }

                        return true;
                    }
                }

                break;
            case SymbolKind.Method:
                if (arity != 0 || (options & LookupOptions.AllMethodsOnArityZero) == 0) {
                    var method = (MethodSymbol)symbol;

                    if (method.arity != arity) {
                        if (method.arity == 0) {
                            error = diagnose
                                ? Error.HasNoTemplate(errorLocation, method, MessageID.IDS_SK_METHOD.Localize())
                                : null;
                        } else {
                            error = diagnose
                                ? Error.BadArity(
                                    errorLocation,
                                    method,
                                    MessageID.IDS_SK_METHOD.Localize(),
                                    method.arity
                                )
                                : null;
                        }

                        return true;
                    }
                }

                break;
            default:
                if (arity != 0) {
                    error = diagnose
                        ? Error.TemplateNotAllowed(errorLocation, symbol, symbol.kind.Localize())
                        : null;

                    return true;
                }

                break;
        }

        error = null;
        return false;
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
        TypeSymbol type = null;

        switch (symbol.kind) {
            case SymbolKind.Method:
                return true;
            case SymbolKind.Field:
                type = ((FieldSymbol)symbol).GetFieldType(fieldsBeingBound).type;
                break;
        }

        return type is not null && type.StrippedType().typeKind is TypeKind.FunctionPointer or TypeKind.Function;
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
            diagsForInstanceInitializers,
            out var firstImportChain
        );

        processedInitializers.hasErrors = diagsForInstanceInitializers.AnyErrors();
        processedInitializers.firstImportChain = firstImportChain;
        diagnostics.PushRange(diagsForInstanceInitializers);
        diagsForInstanceInitializers.Free();
    }

    internal static ImmutableArray<BoundInitializer> BindFieldInitializers(
        Compilation compilation,
        ImmutableArray<ImmutableArray<FieldInitializer>> initializers,
        BelteDiagnosticQueue diagnostics,
        out ImportChain firstImportChain) {
        if (initializers.IsEmpty) {
            firstImportChain = null;
            return [];
        }

        var boundInitializers = ArrayBuilder<BoundInitializer>.GetInstance();
        BindRegularFieldInitializers(compilation, initializers, boundInitializers, diagnostics, out firstImportChain);
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
        BelteDiagnosticQueue diagnostics,
        out ImportChain firstImportChain) {
        firstImportChain = null;

        foreach (var siblingInitializers in initializers) {
            BinderFactory binderFactory = null;

            foreach (var initializer in siblingInitializers) {
                var fieldSymbol = initializer.field;

                if (!fieldSymbol.isConstExpr) {
                    var syntaxRef = initializer.syntax;

                    if (!fieldSymbol.isStatic && fieldSymbol.containingType.IsStructType())
                        diagnostics.Push(Error.CannotInitializeInStructs(syntaxRef.location));

                    switch (syntaxRef.node) {
                        case EqualsValueClauseSyntax initializerNode:
                            binderFactory ??= compilation.GetBinderFactory(syntaxRef.syntaxTree);
                            var parentBinder = binderFactory.GetBinder(initializerNode);
                            parentBinder = parentBinder.GetFieldInitializerBinder(fieldSymbol);

                            firstImportChain ??= parentBinder.importChain;

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
        if (constructor.methodKind != MethodKind.Constructor || constructor.isExtern)
            return null;

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

        if (containingType.IsStructType() || containingType.IsEnumType())
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

    private static ExpressionSyntax SkipParensAndNullSuppressions(
        ExpressionSyntax expression,
        BelteDiagnosticQueue diagnostics,
        ref bool hasErrors) {
        while (true) {
            switch (expression) {
                case LiteralExpressionSyntax literal when literal.token.kind == SyntaxKind.DefaultKeyword:
                    throw ExceptionUtilities.Unreachable();
                // diagnostics.Add(ErrorCode.ERR_DefaultPattern, expression.Location);
                // hasErrors = true;
                // return expression;
                case ParenthesisExpressionSyntax paren:
                    expression = paren.expression;
                    continue;
                default:
                    return expression;
            }
        }
    }

    private BoundExpression BindExpressionOrTypeForPattern(
        TypeSymbol inputType,
        ExpressionSyntax patternExpression,
        ref bool hasErrors,
        BelteDiagnosticQueue diagnostics,
        out ConstantValue constantValueOpt,
        out bool wasExpression,
        out Conversion patternExpressionConversion) {
        constantValueOpt = null;
        var expression = BindTypeOrRValue(patternExpression, diagnostics);
        wasExpression = expression.kind != BoundKind.TypeExpression;

        if (wasExpression) {
            return BindExpressionForPatternContinued(
                expression,
                inputType,
                patternExpression,
                ref hasErrors,
                diagnostics,
                out constantValueOpt,
                out patternExpressionConversion
            );
        } else {
            // TODO patterns
            throw ExceptionUtilities.Unreachable();
            // hasErrors |= CheckValidPatternType(patternExpression, inputType, expression.Type, diagnostics: diagnostics);
            // patternExpressionConversion = Conversion.None;
            // return expression;
        }
    }

    private BoundExpression BindExpressionForPatternContinued(
        BoundExpression expression,
        TypeSymbol inputType,
        ExpressionSyntax patternExpression,
        ref bool hasErrors,
        BelteDiagnosticQueue diagnostics,
        out ConstantValue constantValue,
        out Conversion patternExpressionConversion) {
        var convertedExpression = ConvertPatternExpression(
            inputType,
            patternExpression,
            expression,
            out constantValue,
            hasErrors,
            diagnostics,
            out patternExpressionConversion
        );

        if (!convertedExpression.hasErrors && !hasErrors) {
            if (constantValue is null) {
                var strippedInputType = inputType.StrippedType();

                // TODO do we care about this error
                if (strippedInputType.kind is not SymbolKind.ErrorType and not SymbolKind.TemplateParameter &&
                    strippedInputType.specialType is not SpecialType.Object and not SpecialType.ValueType) {
                    throw ExceptionUtilities.Unreachable();
                    //     diagnostics.Add(ErrorCode.ERR_ConstantValueOfTypeExpected, patternExpression.Location, strippedInputType);
                } else {
                    diagnostics.Push(Error.ConstantExpected(patternExpression.location));
                }

                hasErrors = true;
            }
        }

        if (convertedExpression.type is null && constantValue.value is not null)
            convertedExpression = BindToTypeForErrorRecovery(convertedExpression);

        return convertedExpression;
    }

    internal BoundPattern BindConstantPatternWithFallbackToTypePattern(
        SyntaxNode node,
        ExpressionSyntax expression,
        TypeSymbol inputType,
        bool hasErrors,
        BelteDiagnosticQueue diagnostics) {
        var innerExpression = SkipParensAndNullSuppressions(expression, diagnostics, ref hasErrors);
        var convertedExpression = BindExpressionOrTypeForPattern(
            inputType,
            innerExpression,
            ref hasErrors,
            diagnostics,
            out var constantValue,
            out var wasExpression,
            out var patternConversion
        );

        if (wasExpression) {
            var convertedType = convertedExpression.type ?? inputType;

            // TODO Interfaces
            // if (constantValue is not null && constantValue.specialType.IsNumeric() && ShouldBlockINumberBaseConversion(patternConversion, inputType)) {
            // Cannot use a numeric constant or relational pattern on '{0}' because it inherits from or extends 'INumberBase&lt;T&gt;'. Consider using a type pattern to narrow to a specific numeric type.
            // diagnostics.Add(ErrorCode.ERR_CannotMatchOnINumberBase, node.Location, inputType);
            // }

            return new BoundConstantPattern(
                node,
                convertedExpression,
                constantValue ?? ConstantValue.Unset,
                inputType,
                convertedType,
                hasErrors || constantValue is null
            );
        } else {
            // TODO patterns
            throw ExceptionUtilities.Unreachable();
            // var boundType = (BoundTypeExpression)convertedExpression;
            // var isExplicitNotNullTest = boundType.type.specialType == SpecialType.Object;
            // return new BoundTypePattern(node, boundType, isExplicitNotNullTest, inputType, boundType.Type, hasErrors);
        }
    }

    internal BoundExpression CreateReturnConversion(
        SyntaxNode node,
        BelteDiagnosticQueue diagnostics,
        BoundExpression argument,
        RefKind returnRefKind,
        TypeSymbol returnType) {
        var conversion = conversions.ClassifyConversionFromExpression(argument, returnType);

        if (!argument.hasAnyErrors) {
            if (returnRefKind != RefKind.None) {
                if (conversion.kind is not ConversionKind.Identity and not ConversionKind.NullLiteral)
                    diagnostics.Push(Error.RefReturnMustHaveIdentityConversion(argument.syntax.location, returnType));
                else if (conversion.kind == ConversionKind.NullLiteral)
                    return BoundFactory.Literal(argument.syntax, null, returnType);
                else
                    return BindToNaturalType(argument, diagnostics);
            } else if (!conversion.isImplicit || !conversion.exists) {
                GenerateImplicitConversionError(diagnostics, argument.syntax, conversion, argument, returnType);
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
        if (expression.hasAnyErrors)
            diagnostics = BelteDiagnosticQueue.Discarded;

        conversion = (flags & ConversionForAssignmentFlags.IncrementAssignment) == 0
            ? conversions.ClassifyConversionFromExpression(expression, targetType)
            : conversions.ClassifyConversionFromType(expression.Type(), targetType);

        if ((flags & ConversionForAssignmentFlags.RefAssignment) != 0) {
            if (conversion.kind != ConversionKind.Identity)
                diagnostics.Push(Error.RefAssignmentMustHaveIdentityConversion(expression.syntax.location, targetType));
            else
                return expression;
        } else {
            var collapsedConversion = Conversion.CollapseConversion(conversion);

            if (!collapsedConversion.exists ||
              ((flags & ConversionForAssignmentFlags.CompoundAssignment) == 0
                ? !collapsedConversion.isImplicit
                : (collapsedConversion.isExplicit && ((flags & ConversionForAssignmentFlags.PredefinedOperator) == 0)))) {
                if (expression.kind != BoundKind.UnconvertedExtendedLiteralExpression) {
                    if ((flags & ConversionForAssignmentFlags.DefaultParameter) == 0) {
                        GenerateImplicitConversionError(
                            diagnostics,
                            expression.syntax,
                            collapsedConversion,
                            expression,
                            targetType
                        );
                    }

                    // Implicit enum field errors are handled separately
                    if (expression.kind != BoundKind.UnconvertedImplicitEnumFieldExpression)
                        diagnostics = BelteDiagnosticQueue.Discarded;
                }
            }
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
            diagnostics.Push(Error.NoImplicitConversion(syntax.location, operand.Type(), targetType));
            return;
        }

        switch (operand.kind) {
            case BoundKind.ErrorExpression:
                return;
            case BoundKind.MethodGroup:
                ReportMethodGroupDiagnostics((BoundMethodGroup)operand);
                return;
            case BoundKind.LiteralExpression:
                if (ConstantValue.IsNull(operand.constantValue)) {
                    if (!targetType.IsNullableType()) {
                        if (!targetType.isStatic)
                            diagnostics.Push(Error.ValueCannotBeNull(syntax.location, targetType));

                        return;
                    }
                }

                break;
            case BoundKind.UnconvertedConditionalOperator: {
                    var conditionalOperator = (BoundUnconvertedConditionalOperator)operand;
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
            case BoundKind.UnconvertedArrayLength:
                operand = BindToNaturalType(operand, diagnostics);
                GenerateImplicitConversionError(diagnostics, syntax, conversion, operand, targetType);
                return;
            case BoundKind.ConditionalAccessExpression:
                var access = (BoundConditionalAccessExpression)operand;

                if (access.accessExpression is BoundUnconvertedArrayLength length) {
                    var newLength = BindToNaturalType(length, diagnostics);
                    GenerateImplicitConversionError(diagnostics, syntax, conversion, newLength, targetType);
                    return;
                }

                break;
        }

        var sourceType = operand.Type();

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

        void ReportMethodGroupDiagnostics(BoundMethodGroup operand) {
            if (!Conversions.ReportMethodGroupDiagnostics(this, operand, targetType, diagnostics)) {
                if (targetType.StrippedType().typeKind == TypeKind.Function)
                    diagnostics.Push(Error.MethodFunctionMismatch(syntax.location, (BoundMethodGroup)operand, targetType));
                else
                    diagnostics.Push(Error.MethodGroupCannotBeUsedAsValue(syntax.location, (BoundMethodGroup)operand));
            }
        }
    }

    private BoundExpression ConvertConditionalExpression(
        BoundUnconvertedConditionalOperator source,
        TypeSymbol destination,
        Conversion? conversionIfTargetTyped,
        BelteDiagnosticQueue diagnostics,
        bool hasErrors = false) {
        var targetTyped = conversionIfTargetTyped is not null;
        var conversion = conversionIfTargetTyped.GetValueOrDefault();
        var underlyingConversions = conversion.underlyingConversions;
        var condition = source.condition;
        hasErrors |= source.hasErrors || destination.IsErrorType();

        var trueExpr = targetTyped
            ? CreateConversion(
                source.trueExpression.syntax,
                source.trueExpression,
                underlyingConversions[0],
                isCast: false,
                destination,
                diagnostics
            )
            : GenerateConversionForAssignment(destination, source.trueExpression, diagnostics);
        var falseExpr = targetTyped
            ? CreateConversion(
                source.falseExpression.syntax,
                source.falseExpression,
                underlyingConversions[1],
                isCast: false,
                destination,
                diagnostics
            )
            : GenerateConversionForAssignment(destination, source.falseExpression, diagnostics);

        var constantValue = ConstantFolding.FoldConditional(condition, trueExpr, falseExpr, destination);

        return new BoundConditionalOperator(
            source.syntax,
            condition,
            isRef: false,
            trueExpr,
            falseExpr,
            constantValue,
            destination,
            hasErrors
        );
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
            throw ExceptionUtilities.Unreachable();
            // Error(diagnostics, ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, node.Syntax, targetType);
        }

        return;
    }

    internal static void GenerateImplicitConversionError(
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
        BoundExpression source,
        TypeSymbol destination,
        BelteDiagnosticQueue diagnostics) {
        var conversion = conversions.ClassifyConversionFromExpression(source, destination);

        return CreateConversion(
            source.syntax,
            source,
            conversion,
            isCast: false,
            destination: destination,
            diagnostics: diagnostics
        );
    }

    internal BoundExpression CreateConversion(
        SyntaxNode node,
        BoundExpression source,
        Conversion conversion,
        bool isCast,
        TypeSymbol destination,
        BelteDiagnosticQueue diagnostics,
        bool hasErrors = false) {
        switch (source.kind) {
            case BoundKind.UnconvertedInitializerList:
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
                    destination,
                    hasErrors
                );
            case BoundKind.UnconvertedNullptrExpression:
                var ptrExpression = ConvertNullptrExpression(
                    (BoundUnconvertedNullptrExpression)source,
                    destination,
                    conversion,
                    diagnostics
                );

                return new BoundCastExpression(
                    node,
                    ptrExpression,
                    conversion,
                    null,
                    destination,
                    hasErrors
                );
            case BoundKind.UnconvertedImplicitEnumFieldExpression:
                var fieldExpression = ConvertImplicitEnumFieldExpression(
                    (BoundUnconvertedImplicitEnumFieldExpression)source,
                    destination,
                    conversion,
                    diagnostics
                );

                return new BoundCastExpression(
                    node,
                    fieldExpression,
                    conversion,
                    fieldExpression.constantValue,
                    destination,
                    hasErrors
                );
            case BoundKind.UnconvertedExtendedLiteralExpression:
                var literalCreation = ConvertExtendedLiteralExpression(
                    (BoundUnconvertedExtendedLiteralExpression)source,
                    destination,
                    conversion,
                    diagnostics
                );

                return new BoundCastExpression(
                    node,
                    literalCreation,
                    conversion,
                    null,
                    destination,
                    hasErrors
                );
            case BoundKind.UnconvertedArrayLength:
                return CreateArrayLengthConversion(
                    node,
                    (BoundUnconvertedArrayLength)source,
                    conversion,
                    destination,
                    diagnostics
                );
            case BoundKind.ConditionalAccessExpression:
                var access = (BoundConditionalAccessExpression)source;

                if (access.accessExpression.kind == BoundKind.UnconvertedArrayLength)
                    return CreateArrayLengthConversion(node, access, conversion, destination, diagnostics);

                break;
        }

        if (conversion.kind == ConversionKind.ObjectCreation) {
            var objectCreation = ConvertObjectCreationExpression(
                (BoundUnconvertedObjectCreationExpression)source,
                destination,
                conversion,
                diagnostics
            );

            return new BoundCastExpression(
                node,
                objectCreation,
                conversion,
                null,
                destination,
                hasErrors
            );
        }

        if (conversion.kind == ConversionKind.ConditionalExpression) {
            var convertedConditional = ConvertConditionalExpression(
                (BoundUnconvertedConditionalOperator)source,
                destination,
                conversionIfTargetTyped: conversion,
                diagnostics
            );

            return new BoundCastExpression(
                node,
                convertedConditional,
                conversion,
                convertedConditional.constantValue,
                destination,
                hasErrors
            );
        }

        if (conversion.kind == ConversionKind.ImplicitTupleLiteral) {
            return CreateTupleLiteralConversion(
                node,
                (BoundTupleLiteral)source,
                conversion,
                isCast,
                destination,
                diagnostics
            );
        }

        if (conversion.isIdentity) {
            source = BindToNaturalType(source, diagnostics);

            if (!isCast &&
                (source.IsLiteralNull() ||
                (source.type is not null && source.Type().Equals(destination)))) {
                return source;
            }
        }

        ConstantValue constantValue = null;

        if (conversion.exists && conversion.kind is not ConversionKind.ImplicitNullToPointer and not
            ConversionKind.ExplicitIntegerToPointer and not ConversionKind.ExplicitPointerToInteger) {
            constantValue = conversion.method is null
                ? ConstantFolding.FoldCast(source, new TypeWithAnnotations(destination), diagnostics)
                : null;
        }

        if (conversion.kind == ConversionKind.DefaultLiteral) {
            var defaultLiteral = (BoundDefaultLiteral)source;

            ReportDefaultExpressionErrors(source.syntax, destination, defaultLiteral.isLowLevel, true, diagnostics);

            source = new BoundDefaultExpression(
                source.syntax,
                isLowLevel: defaultLiteral.isLowLevel,
                targetType: null,
                constantValue,
                type: destination
            );
        }

        if (conversion.method is not null && conversion.kind != ConversionKind.MethodGroup) {
            var targetType = conversion.method.GetParameterTypes()[0].type;
            var argumentConversion = conversions.ClassifyConversionFromExpression(source, targetType);
            source = CreateConversion(source, argumentConversion, targetType, diagnostics);
        }

        return new BoundCastExpression(
            node,
            BindToNaturalType(source, diagnostics),
            conversion,
            constantValue,
            destination,
            hasErrors
        );
    }

    private void ReportDefaultExpressionErrors(
        SyntaxNode syntax,
        TypeSymbol destination,
        bool isLowLevel,
        bool isLiteral,
        BelteDiagnosticQueue diagnostics) {
        var typeHasDefaultValue = destination.HasDefaultValue();

        if (!typeHasDefaultValue && !isLowLevel) {
            if (destination.IsStructType())
                diagnostics.Push(Error.StructWithNoDefault(syntax.location, destination));
            else
                diagnostics.Push(Error.TypeWithNoDefault(syntax.location, destination));
        }

        if (typeHasDefaultValue && isLowLevel) {
            if (isLiteral)
                diagnostics.Push(Warning.UnnecessaryLowLevelDefaultLiteral(syntax.location, destination));
            else
                diagnostics.Push(Warning.UnnecessaryLowLevelDefaultExpression(syntax.location, destination));
        }
    }

    private BoundExpression CreateArrayLengthConversion(
        SyntaxNode syntax,
        BoundUnconvertedArrayLength arrayLength,
        Conversion conversion,
        TypeSymbol destination,
        BelteDiagnosticQueue diagnostics) {
        if (conversion.isIdentity)
            return new BoundArrayLength(arrayLength.syntax, arrayLength.receiver, destination);

        return new BoundCastExpression(
            syntax,
            BindToNaturalType(arrayLength, diagnostics),
            conversion,
            null,
            destination
        );
    }

    private BoundExpression CreateArrayLengthConversion(
        SyntaxNode syntax,
        BoundConditionalAccessExpression access,
        Conversion conversion,
        TypeSymbol destination,
        BelteDiagnosticQueue diagnostics) {
        var arrayLength = (BoundUnconvertedArrayLength)access.accessExpression;

        if (conversion.isIdentity) {
            return new BoundConditionalAccessExpression(
                access.syntax,
                access.receiver,
                new BoundArrayLength(arrayLength.syntax, arrayLength.receiver, destination),
                CorLibrary.GetOrCreateNullableType(destination)
            );
        }

        var naturalLength = BindToNaturalType(arrayLength, diagnostics);

        return new BoundCastExpression(
            syntax,
            new BoundConditionalAccessExpression(
                access.syntax,
                access.receiver,
                naturalLength,
                CorLibrary.GetOrCreateNullableType(naturalLength.type)
            ),
            conversion,
            null,
            destination
        );
    }

    private BoundExpression CreateTupleLiteralConversion(
        SyntaxNode syntax,
        BoundTupleLiteral sourceTuple,
        Conversion conversion,
        bool isCast,
        TypeSymbol destination,
        BelteDiagnosticQueue diagnostics) {
        var destinationWithoutNullable = destination;
        var conversionWithoutNullable = conversion;

        var targetType = (NamedTypeSymbol)destinationWithoutNullable;

        if (targetType.isTupleType) {
            if (sourceTuple.type is NamedTypeSymbol { isTupleType: true } sourceType) {
                targetType = targetType.WithTupleDataFrom(sourceType);
            } else {
                // We disallow literals to have argument names so this should not ever matter
                throw ExceptionUtilities.Unreachable();
                // var tupleSyntax = (TupleExpressionSyntax)sourceTuple.syntax;
                // var locationBuilder = ArrayBuilder<TextLocation>.GetInstance();

                // foreach (var argument in tupleSyntax.Arguments) {
                //     locationBuilder.Add(argument.NameColon?.Name.Location);
                // }

                // targetType = targetType.WithElementNames(sourceTuple.argu,
                //     locationBuilder.ToImmutableAndFree(),
                //     errorPositions: default,
                //     ImmutableArray.Create(tupleSyntax.Location));
            }
        }

        var arguments = sourceTuple.arguments;
        var convertedArguments = ArrayBuilder<BoundExpression>.GetInstance(arguments.Length);

        var targetElementTypes = targetType.tupleElementTypes;
        var underlyingConversions = conversionWithoutNullable.underlyingConversions;

        for (var i = 0; i < arguments.Length; i++) {
            var argument = arguments[i];
            var destType = targetElementTypes[i];
            var elementConversion = underlyingConversions[i];
            convertedArguments.Add(CreateConversion(
                argument.syntax,
                argument,
                elementConversion,
                isCast: isCast,
                destType.type.type,
                diagnostics
            ));
        }

        BoundExpression result = new BoundConvertedTupleLiteral(
            sourceTuple.syntax,
            sourceTuple,
            wasTargetTyped: true,
            convertedArguments.ToImmutableAndFree(),
            targetType
        );

        if (!TypeSymbol.Equals(sourceTuple.type, destination, TypeCompareKind.ConsiderEverything)) {
            result = new BoundCastExpression(
                sourceTuple.syntax,
                result,
                conversion,
                constantValue: null,
                type: destination
            );
        }

        if (isCast) {
            result = new BoundCastExpression(
                syntax,
                result,
                Conversion.Identity,
                constantValue: null,
                type: destination
            );
        }

        return result;
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

    private static BoundExpression ConvertObjectCreationExpression(
        BoundUnconvertedObjectCreationExpression node,
        TypeSymbol destination,
        Conversion conversion,
        BelteDiagnosticQueue diagnostics) {
        var arguments = AnalyzedArguments.GetInstance(
            node.arguments.Select(a => new BoundExpressionOrTypeOrConstant(a)).ToImmutableArray(),
            node.arguments.Select(a => false).ToImmutableArray(),
            node.arguments.Select(a => a.syntax).ToImmutableArray(),
            node.arguments.Select(a => a.type).ToImmutableArray(),
            node.argumentRefKinds,
            node.argumentNames
        );

        var expr = BindObjectCreationExpression(
            node.syntax,
            node.binder,
            destination.StrippedType(),
            arguments,
            diagnostics
        );

        arguments.Free();

        return expr;

        static BoundExpression BindObjectCreationExpression(
            SyntaxNode syntax,
            Binder binder,
            TypeSymbol type,
            AnalyzedArguments arguments,
            BelteDiagnosticQueue diagnostics) {
            switch (type.typeKind) {
                case TypeKind.Enum:
                case TypeKind.Struct:
                case TypeKind.Class:
                    return binder.BindClassCreationExpression(
                        syntax,
                        type.name,
                        typeNode: syntax,
                        (NamedTypeSymbol)type,
                        arguments,
                        diagnostics
                    );
                case TypeKind.TemplateParameter:
                    return binder.BindTemplateParameterCreationExpression(
                        syntax,
                        (TemplateParameterSymbol)type,
                        arguments,
                        typeSyntax: syntax,
                        wasTargetTyped: true,
                        diagnostics
                    );
                case TypeKind.Interface:
                    return binder.BindInterfaceCreationExpression(
                        syntax,
                        (NamedTypeSymbol)type,
                        diagnostics,
                        typeNode: syntax,
                        arguments,
                        wasTargetTyped: true
                    );
                case TypeKind.Array:
                case TypeKind.Pointer:
                case TypeKind.FunctionPointer:
                case TypeKind.Primitive:
                    diagnostics.Push(Error.ObjectCreationIllegalTargetType(syntax.location, type));
                    goto case TypeKind.Error;
                case TypeKind.Error:
                    return binder.MakeErrorExpressionForObjectCreation(syntax, type, arguments, syntax, diagnostics);
                default:
                    throw ExceptionUtilities.UnexpectedValue(type.typeKind);
            }
        }
    }

    private BoundExpression BindInterfaceCreationExpression(
        SyntaxNode node,
        NamedTypeSymbol type,
        BelteDiagnosticQueue diagnostics,
        SyntaxNode typeNode,
        AnalyzedArguments analyzedArguments,
        bool wasTargetTyped) {
        diagnostics.Push(Error.CannotCreateInterface(node.location, type));
        return MakeErrorExpressionForObjectCreation(node, type, analyzedArguments, typeNode, diagnostics);
    }

    private BoundExpression ConvertExtendedLiteralExpression(
        BoundUnconvertedExtendedLiteralExpression node,
        TypeSymbol destination,
        Conversion conversion,
        BelteDiagnosticQueue diagnostics) {
        if (conversion.method is not null)
            return node.literal;

        var candidates = GetExtendedLiteralCandidates(
            node,
            destination,
            out var analyzedArguments,
            out var memberGroup
        );

        var name = WellKnownMemberNames.GetLiteralOperatorName(node.suffix);

        if (candidates.results.Length == 0) {
            diagnostics.Push(Error.NoExtendedLiteralConversion(
                node.syntax.location,
                destination,
                node.literal.type,
                node.suffix
            ));

            return ErrorExpression(node.syntax, node);
        }

        if (!candidates.succeeded) {
            candidates.ReportDiagnostics(
                binder: this,
                location: node.syntax.location,
                node: node.syntax,
                diagnostics,
                name: name,
                receiver: null,
                invokedExpression: null,
                analyzedArguments,
                memberGroup: memberGroup,
                null
            );

            return CreateErrorCall(
                node: node.syntax,
                name: name,
                receiver: null,
                methods: memberGroup,
                resultKind: LookupResultKind.OverloadResolutionFailure,
                templateArguments: [],
                analyzedArguments: analyzedArguments
            );
        }

        var memberResult = candidates.bestResult;

        CheckAndCoerceArguments(
            node.syntax,
            memberResult,
            analyzedArguments,
            diagnostics,
            null,
            out var argsToParams
        );

        Debug.Assert(destination.Equals(memberResult.member.returnType));

        var arguments = analyzedArguments.arguments.Select(a => a.expression).ToImmutableArray();
        var refKinds = analyzedArguments.refKinds.ToImmutableAndFree();

        analyzedArguments.Free();

        return new BoundCallExpression(
            node.syntax,
            null,
            memberResult.member,
            arguments,
            refKinds,
            default,
            LookupResultKind.Viable,
            destination
        );
    }

    internal OverloadResolutionResult<MethodSymbol> GetExtendedLiteralCandidates(
        BoundUnconvertedExtendedLiteralExpression node,
        TypeSymbol destination,
        out AnalyzedArguments analyzedArguments,
        out ImmutableArray<MethodSymbol> memberGroup) {
        // TODO This should probably be moved to OverloadResolution
        var strippedType = destination.StrippedType();
        var result = OverloadResolutionResult<MethodSymbol>.GetInstance();

        if (OperatorFacts.NoUserDefinedOperators(strippedType)) {
            memberGroup = [];
            analyzedArguments = null;
            return result;
        }

        TypeSymbol constrainedToTypeOpt = strippedType as TemplateParameterSymbol;
        var operators = ArrayBuilder<MethodSymbol>.GetInstance();

        if (strippedType is not NamedTypeSymbol current)
            current = strippedType.baseType;

        if (current is null && strippedType.IsTemplateParameter())
            current = ((TemplateParameterSymbol)strippedType).effectiveBaseClass;

        var name = WellKnownMemberNames.GetLiteralOperatorName(node.suffix);
        var hadApplicableCandidates = false;

        for (; current is not null; current = current.baseType) {
            operators.Clear();
            GetDeclaredOperators(constrainedToTypeOpt, current, name, operators);

            if (HasCandidateOperators(operators, node.literal)) {
                hadApplicableCandidates = true;
                break;
            }
        }

        memberGroup = operators.ToImmutable();

        if (!hadApplicableCandidates) {
            analyzedArguments = null;
            return result;
        }

        analyzedArguments = AnalyzedArguments.GetInstance(
            [new BoundExpressionOrTypeOrConstant(node.literal)],
            [false],
            [node.literal.syntax],
            [node.literal.type],
            [RefKind.None],
            []
        );

        overloadResolution.MethodOverloadResolution(
            operators,
            [],
            null,
            analyzedArguments,
            result
        );

        return result;

        static void GetDeclaredOperators(
            TypeSymbol constrainedToTypeOpt,
            NamedTypeSymbol type,
            string name,
            ArrayBuilder<MethodSymbol> operators) {
            var typeOperators = ArrayBuilder<MethodSymbol>.GetInstance();
            type.AddLiteralOperators(name, typeOperators);

            foreach (var op in typeOperators) {
                if (op.parameterCount != 1 || op.returnsVoid)
                    continue;

                operators.Add(op);
            }

            typeOperators.Free();
        }

        bool HasCandidateOperators(ArrayBuilder<MethodSymbol> operators, BoundExpression expression) {
            var hadApplicableCandidate = false;

            foreach (var op in operators) {
                var conversion = conversions.ClassifyConversionFromExpression(
                    expression,
                    op.parameterTypesWithAnnotations[0].type
                );

                if (conversion.isImplicit) {
                    hadApplicableCandidate = true;
                    break;
                }
            }

            return hadApplicableCandidate;
        }
    }

    private BoundExpression ConvertImplicitEnumFieldExpression(
        BoundUnconvertedImplicitEnumFieldExpression node,
        TypeSymbol targetType,
        Conversion conversion,
        BelteDiagnosticQueue diagnostics) {
        if (!targetType.StrippedType().IsEnumType()) {
            diagnostics.Push(Error.WrongEnumTargetType(node.syntax.location));
            return ErrorExpression(node.syntax, node);
        }

        var enumType = targetType.StrippedType();
        var symbols = enumType.GetMembers(node.name).Where(s => s.kind == SymbolKind.Field).ToArray();

        if (symbols.Length == 0) {
            diagnostics.Push(Error.NoSuchMember(node.syntax.location, enumType, node.name));
            return ErrorExpression(node.syntax, node);
        } else if (symbols.Length == 1) {
            var field = symbols[0] as FieldSymbol;
            var constantValue = field.GetConstantValue(constantFieldsInProgress);
            return new BoundFieldAccessExpression(node.syntax, null, field, constantValue, enumType);
        } else {
            throw ExceptionUtilities.Unreachable();
        }
    }

    private BoundExpression ConvertNullptrExpression(
        BoundUnconvertedNullptrExpression node,
        TypeSymbol targetType,
        Conversion conversion,
        BelteDiagnosticQueue diagnostics) {
        return new BoundLiteralExpression(node.syntax, new ConstantValue(null, SpecialType.None), targetType);
    }

    private BoundInitializerList ConvertListExpression(
        BoundUnconvertedInitializerList node,
        TypeSymbol targetType,
        Conversion conversion,
        BelteDiagnosticQueue diagnostics) {
        if (conversion.isNullable) {
            targetType = targetType.GetNullableUnderlyingType();
            conversion = conversion.underlyingConversions[0];
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

    #region Attributes

    internal static void BindAttributeTypes(
        ImmutableArray<Binder> binders,
        ImmutableArray<AttributeSyntax> attributesToBind,
        Symbol ownerSymbol,
        NamedTypeSymbol[] boundAttributeTypes,
        Action<AttributeSyntax>? beforeAttributePartBound,
        Action<AttributeSyntax>? afterAttributePartBound,
        BelteDiagnosticQueue diagnostics) {
        for (var i = 0; i < attributesToBind.Length; i++) {
            if (boundAttributeTypes[i] is null) {
                var binder = binders[i];
                var attributeToBind = attributesToBind[i];

                beforeAttributePartBound?.Invoke(attributeToBind);
                var boundType = binder.BindType(attributeToBind.name, diagnostics);
                var boundTypeSymbol = (NamedTypeSymbol)boundType.type;

                // TODO We only have 1 attribute so this should be handled anyways
                // if (boundTypeSymbol.typeKind != TypeKind.Error) {
                //     binder.CheckDisallowedAttributeDependentType(boundType, attributeToBind.name, diagnostics);
                // }

                boundAttributeTypes[i] = boundTypeSymbol;

                afterAttributePartBound?.Invoke(attributeToBind);
            }
        }
    }

    internal static void GetAttributes(
        ImmutableArray<Binder> binders,
        ImmutableArray<AttributeSyntax> attributesToBind,
        ImmutableArray<NamedTypeSymbol> boundAttributeTypes,
        AttributeData?[] attributeDataArray,
        BoundAttribute?[]? boundAttributeArray,
        Action<AttributeSyntax>? beforeAttributePartBound,
        Action<AttributeSyntax>? afterAttributePartBound,
        BelteDiagnosticQueue diagnostics) {
        for (var i = 0; i < attributesToBind.Length; i++) {
            var attributeSyntax = attributesToBind[i];
            var boundAttributeType = boundAttributeTypes[i];
            var binder = binders[i];

            // TODO We only have well known attributes currently
            // var attribute = (SourceAttributeData?)attributeDataArray[i];

            // if (attribute == null) {
            (attributeDataArray[i], var boundAttribute) = binder.GetAttribute(
                attributeSyntax,
                boundAttributeType,
                beforeAttributePartBound,
                afterAttributePartBound,
                diagnostics
            );

            boundAttributeArray?[i] = boundAttribute;
            // } else {
            //     Debug.Assert(boundAttributeArray is null || boundAttributeArray[i] is not null);

            //     // attributesBuilder might contain some early bound well-known attributes, which had no errors.
            //     // We don't rebind the early bound attributes, but need to compute isConditionallyOmitted.
            //     // Note that AttributeData.IsConditionallyOmitted is required only during emit, but must be computed here as
            //     // its value depends on the values of conditional symbols, which in turn depends on the source file where the attribute is applied.

            //     Debug.Assert(!attribute.HasErrors);
            //     Debug.Assert(attribute.AttributeClass is object);
            //     CompoundUseSiteInfo<AssemblySymbol> useSiteInfo = binder.GetNewCompoundUseSiteInfo(diagnostics);
            //     bool isConditionallyOmitted = binder.IsAttributeConditionallyOmitted(attribute.AttributeClass, attributeSyntax.SyntaxTree, ref useSiteInfo);
            //     diagnostics.Add(attributeSyntax, useSiteInfo);
            //     attributeDataArray[i] = attribute.WithOmittedCondition(isConditionallyOmitted);
            // }
        }
    }

    internal (AttributeData, BoundAttribute) GetAttribute(
        AttributeSyntax node, NamedTypeSymbol boundAttributeType,
        Action<AttributeSyntax>? beforeAttributePartBound,
        Action<AttributeSyntax>? afterAttributePartBound,
        BelteDiagnosticQueue diagnostics) {
        beforeAttributePartBound?.Invoke(node);
        var boundAttribute = new ExecutableCodeBinder(node, containingMember, this)
            .BindAttribute(node, boundAttributeType, (this as ContextualAttributeBinder)?.attributedMember, diagnostics);
        afterAttributePartBound?.Invoke(node);
        return (GetAttribute(boundAttribute, diagnostics), boundAttribute);
    }

    internal BoundAttribute BindAttribute(
        AttributeSyntax node,
        NamedTypeSymbol attributeType,
        Symbol? attributedMember,
        BelteDiagnosticQueue diagnostics) {
        return BindAttributeCore(GetBinder(node), node, attributeType, attributedMember, diagnostics);
    }

    private static BoundAttribute BindAttributeCore(
        Binder binder,
        AttributeSyntax node,
        NamedTypeSymbol attributeType,
        Symbol? attributedMember,
        BelteDiagnosticQueue diagnostics) {
        binder = binder.WithAdditionalFlags(BinderFlags.AttributeArgument);

        var attributeTypeForBinding = attributeType;
        var resultKind = LookupResultKind.Viable;

        if (attributeTypeForBinding.IsErrorType()) {
            var errorType = (ErrorTypeSymbol)attributeTypeForBinding;
            resultKind = errorType.resultKind;

            if (errorType.candidateSymbols.Length == 1 && errorType.candidateSymbols[0] is NamedTypeSymbol symbol)
                attributeTypeForBinding = symbol;
        }

        var argumentListOpt = node.argumentList;
        var analyzedArguments = AnalyzedArguments.GetInstance();
        binder.BindArgumentsAndNames(argumentListOpt, diagnostics, analyzedArguments);

        ImmutableArray<int> argsToParamsOpt;
        var expanded = false;
        BitVector defaultArguments = default;
        MethodSymbol? attributeConstructor = null;
        ImmutableArray<BoundExpression> boundConstructorArguments;
        if (attributeTypeForBinding.IsErrorType()) {
            boundConstructorArguments = analyzedArguments.arguments.SelectAsArray(
                static (arg, binder) => binder.BindToTypeForErrorRecovery(arg.expression),
                binder);
            argsToParamsOpt = default;
        } else {
            var found = binder.TryPerformConstructorOverloadResolution(
                attributeTypeForBinding,
                analyzedArguments,
                attributeTypeForBinding.name,
                node.location,
                suppressResultDiagnostics: attributeType.IsErrorType(),
                diagnostics,
                out var memberResolutionResult,
                out var candidateConstructors,
                allowProtectedConstructorsOfBaseType: true
            );

            if (memberResolutionResult.isNotNull) {
                binder.CheckAndCoerceArguments(
                    node,
                    memberResolutionResult,
                    analyzedArguments,
                    diagnostics,
                    receiver: null,
                    out argsToParamsOpt
                );
            } else {
                argsToParamsOpt = memberResolutionResult.result.argsToParams;
            }

            attributeConstructor = memberResolutionResult.member;
            // expanded = memberResolutionResult.resolution == MemberResolutionKind.ApplicableInExpandedForm;

            if (!found) {
                // resultKind = resultKind.WorseResultKind(
                //     memberResolutionResult.IsValid && !binder.IsConstructorAccessible(memberResolutionResult.Member, ref useSiteInfo) ?
                //         LookupResultKind.Inaccessible :
                //         LookupResultKind.OverloadResolutionFailure);
                // boundConstructorArguments = binder.BuildArgumentsForErrorRecovery(analyzedArguments, candidateConstructors);
                // TODO Temporary because attributes are intrinsic right now
                boundConstructorArguments = analyzedArguments.arguments.Select(a => a.expression).ToImmutableArray();
            } else {
                binder.BindDefaultArguments(
                    node,
                    attributeConstructor.parameters,
                    analyzedArguments.arguments,
                    argumentRefKindsBuilder: null,
                    analyzedArguments.names,
                    ref argsToParamsOpt,
                    out defaultArguments,
                    expanded,
                    diagnostics
                // attributedMember: attributedMember
                );

                boundConstructorArguments = analyzedArguments.arguments.Select(a => a.expression).ToImmutableArray();

                // if (attributeConstructor.parameters.Any(static p => p.refKind is RefKind.In or RefKind.RefReadOnlyParameter)) {
                //     Error(diagnostics, ErrorCode.ERR_AttributeCtorInParameter, node, attributeConstructor.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));
                // }
            }
        }

        var boundConstructorArgumentNamesOpt = analyzedArguments.GetNames();
        // ImmutableArray<BoundAssignmentOperator> boundNamedArguments = analyzedArguments.namedArguments?.ToImmutableAndFree()
        //     ?? ImmutableArray<BoundAssignmentOperator>.Empty;
        var boundNamedArguments = ImmutableArray<BoundAssignmentOperator>.Empty;
        analyzedArguments.Free();

        return new BoundAttribute(
            node,
            attributeConstructor,
            boundConstructorArguments,
            boundConstructorArgumentNamesOpt,
            argsToParamsOpt,
            expanded,
            defaultArguments,
            boundNamedArguments,
            resultKind,
            attributeType,
            hasErrors: resultKind != LookupResultKind.Viable
        );
    }

    private AttributeData GetAttribute(BoundAttribute boundAttribute, BelteDiagnosticQueue diagnostics) {
        var attributeType = (NamedTypeSymbol)boundAttribute.type.StrippedType();
        var attributeConstructor = boundAttribute.constructor;
        var hasErrors = boundAttribute.hasAnyErrors;

        if (attributeType.IsErrorType() || attributeType.isAbstract || attributeConstructor is null) {
            hasErrors = true;
            // TODO Temp
            // return new SourceAttributeData(
            //     compilation,
            //     (AttributeSyntax)boundAttribute.syntax,
            //     attributeType,
            //     attributeConstructor,
            //     hasErrors
            // );
        }

        // ValidateTypeForAttributeParameters(
        //     attributeConstructor.parameters,
        //     ((AttributeSyntax)boundAttribute.syntax).name,
        //     diagnostics,
        //     ref hasErrors
        // );

        var visitor = new AttributeExpressionVisitor(this);
        var arguments = boundAttribute.constructorArguments;
        var constructorArgsArray = visitor.VisitArguments(arguments, diagnostics, ref hasErrors);
        // var namedArguments = visitor.VisitNamedArguments(boundAttribute.namedArguments, diagnostics, ref hasErrors);
        var namedBuilder = ArrayBuilder<KeyValuePair<string, TypedConstant>>.GetInstance();

        if (boundAttribute.constructorArgumentNames != default) {
            for (var i = 0; i < arguments.Length; i++) {
                var name = boundAttribute.constructorArgumentNames[i];
                var expression = arguments[i];

                if (name is null || expression.constantValue is null)
                    continue;

                var typedConstant = new TypedConstant(
                    expression.type,
                    TypedConstantKind.Primitive,
                    expression.constantValue.value
                );

                namedBuilder.Add(new KeyValuePair<string, TypedConstant>(name, typedConstant));
            }
        }

        var namedArguments = namedBuilder.ToImmutable();

        var argsToParamsOpt = boundAttribute.constructorArgumentsToParams;
        ImmutableArray<TypedConstant> rewrittenArguments;

        if (hasErrors || attributeConstructor.parameterCount == 0) {
            rewrittenArguments = constructorArgsArray;
        } else {
            rewrittenArguments = GetRewrittenAttributeConstructorArguments(
                attributeConstructor,
                constructorArgsArray,
                (AttributeSyntax)boundAttribute.syntax,
                argsToParamsOpt,
                diagnostics,
                ref hasErrors
            );
        }

        // bool isConditionallyOmitted = IsAttributeConditionallyOmitted(attributeType, boundAttribute.syntaxTree);

        return new SourceAttributeData(
            compilation,
            (AttributeSyntax)boundAttribute.syntax,
            attributeType,
            attributeConstructor,
            rewrittenArguments,
            MakeSourceIndices(),
            namedArguments,
            hasErrors,
            // isConditionallyOmitted
            false
        );

        ImmutableArray<int> MakeSourceIndices() {
            var lengthAfterRewriting = rewrittenArguments.Length;
            if (lengthAfterRewriting == 0 || hasErrors)
                return default;

            var defaultArguments = boundAttribute.constructorDefaultArguments;
            if (argsToParamsOpt.IsDefault && !boundAttribute.constructorExpanded) {
                var hasDefaultArgument = false;
                var lengthBeforeRewriting = arguments.Length;

                for (var i = 0; i < lengthBeforeRewriting; i++) {
                    if (defaultArguments[i]) {
                        hasDefaultArgument = true;
                        break;
                    }
                }

                if (!hasDefaultArgument)
                    return default;
            }

            var constructorArgumentSourceIndices = ArrayBuilder<int>.GetInstance(lengthAfterRewriting);
            constructorArgumentSourceIndices.Count = lengthAfterRewriting;

            for (var argIndex = 0; argIndex < lengthAfterRewriting; argIndex++) {
                var paramIndex = argsToParamsOpt.IsDefault ? argIndex : argsToParamsOpt[argIndex];
                constructorArgumentSourceIndices[paramIndex] = defaultArguments[argIndex] ? -1 : argIndex;
            }

            return constructorArgumentSourceIndices.ToImmutableAndFree();
        }
    }

    private ImmutableArray<TypedConstant> GetRewrittenAttributeConstructorArguments(
        MethodSymbol attributeConstructor,
        ImmutableArray<TypedConstant> constructorArgsArray,
        AttributeSyntax syntax,
        ImmutableArray<int> argumentsToParams,
        BelteDiagnosticQueue diagnostics,
        ref bool hasErrors) {
        var argumentsCount = constructorArgsArray.Length;
        var parameters = attributeConstructor.parameters;
        var parameterCount = parameters.Length;

        var reorderedArguments = new TypedConstant[parameterCount];

        for (var i = 0; i < argumentsCount; i++) {
            var paramIndex = argumentsToParams.IsDefault ? i : argumentsToParams[i];
            var parameter = parameters[paramIndex];
            var reorderedArgument = constructorArgsArray[i];

            if (!hasErrors) {
                if (reorderedArgument.kind == TypedConstantKind.Error) {
                    hasErrors = true;
                } else if (reorderedArgument.kind == TypedConstantKind.Array &&
                      parameter.type.typeKind == TypeKind.Array &&
                      !((TypeSymbol)reorderedArgument.type).Equals(parameter.type, TypeCompareKind.AllIgnoreOptions)) {
                    // diagnostics.Add(ErrorCode.ERR_BadAttributeArgument, syntax.Location);
                    hasErrors = true;
                }
            }

            reorderedArguments[paramIndex] = reorderedArgument;
        }

        return reorderedArguments.AsImmutable();
    }

    #endregion
}

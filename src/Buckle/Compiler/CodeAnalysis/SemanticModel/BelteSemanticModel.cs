using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal abstract class BelteSemanticModel : SemanticModel {
    /*
    internal new abstract Compilation compilation { get; }

    internal new abstract BelteSyntaxNode root { get; }

    internal static bool CanGetSemanticInfo(
        BelteSyntaxNode node,
        bool allowNamedArgumentName = false,
        bool isSpeculative = false) {
        if (!isSpeculative && IsInStructuredTriviaOtherThanNameAttribute(node))
            return false;

        switch (node.kind) {
            case SyntaxKind.IdentifierName:
                if (!isSpeculative && node.parent is not null && node.parent.kind == SyntaxKind.NameEquals &&
                    node.parent.parent.kind == SyntaxKind.UsingDirective) {
                    return false;
                }

                goto default;
            case SyntaxKind.OmittedArgument:
            case SyntaxKind.ReferenceExpression:
            case SyntaxKind.ReferenceType:
                return false;
            default:
                if (node.isFabricated)
                    return false;

                return
                    (node is ExpressionSyntax &&
                        (isSpeculative || allowNamedArgumentName || !SyntaxFacts.IsNamedArgumentName(node))) ||
                    (node is ConstructorInitializerSyntax) ||
                    (node is AttributeSyntax); ;
        }
    }

    internal abstract SymbolInfo GetSymbolInfoWorker(
        BelteSyntaxNode node,
        SymbolInfoOptions options,
        CancellationToken cancellationToken = default);

    internal abstract BelteTypeInfo GetTypeInfoWorker(
        BelteSyntaxNode node,
        CancellationToken cancellationToken = default);

    internal abstract BoundExpression GetSpeculativelyBoundExpression(
        int position,
        ExpressionSyntax expression,
        SpeculativeBindingOption bindingOption,
        out Binder binder);

    internal abstract ImmutableArray<Symbol> GetMemberGroupWorker(
        BelteSyntaxNode node,
        SymbolInfoOptions options,
        CancellationToken cancellationToken = default);

    internal abstract Optional<object> GetConstantValueWorker(
        BelteSyntaxNode node,
        CancellationToken cancellationToken = default);

    internal Binder GetSpeculativeBinder(
        int position,
        ExpressionSyntax expression,
        SpeculativeBindingOption bindingOption) {
        position = CheckAndAdjustPosition(position);

        if (bindingOption == SpeculativeBindingOption.BindAsTypeOrNamespace) {
            if (!(expression is TypeSyntax))
                return null;
        }

        var binder = GetEnclosingBinder(position);

        if (binder is null)
            return null;

        if (bindingOption == SpeculativeBindingOption.BindAsTypeOrNamespace && IsInTypeofExpression(position))
            binder = new TypeofBinder(expression, binder);

        // binder = new WithNullableContextBinder(SyntaxTree, position, binder);

        return new ExecutableCodeBinder(expression, binder.containingMember, binder).GetBinder(expression);
    }

    private Binder GetSpeculativeBinderForAttribute(int position, AttributeSyntax attribute) {
        position = CheckAndAdjustPositionForSpeculativeAttribute(position);

        var binder = GetEnclosingBinder(position);

        if (binder is null)
            return null;

        return new ExecutableCodeBinder(attribute, binder.containingMember, binder).GetBinder(attribute);
    }

    private static BoundExpression GetSpeculativelyBoundExpressionHelper(
        Binder binder,
        ExpressionSyntax expression,
        SpeculativeBindingOption bindingOption) {

        BoundExpression boundNode;

        if (bindingOption == SpeculativeBindingOption.BindAsTypeOrNamespace)
            boundNode = binder.BindNamespaceOrType(expression, BelteDiagnosticQueue.Discarded);
        else
            boundNode = binder.BindExpression(expression, BelteDiagnosticQueue.Discarded);

        return boundNode;
    }

    private protected BoundExpression GetSpeculativelyBoundExpressionWithoutNullability(
        int position,
        ExpressionSyntax expression,
        SpeculativeBindingOption bindingOption,
        out Binder binder) {
        if (expression is null)
            throw new ArgumentNullException(nameof(expression));

        expression = SyntaxFactory.GetStandaloneExpression(expression);

        binder = this.GetSpeculativeBinder(position, expression, bindingOption);

        if (binder is null)
            return null;

        var boundNode = GetSpeculativelyBoundExpressionHelper(binder, expression, bindingOption);
        return boundNode;
    }

    private BoundAttribute GetSpeculativelyBoundAttribute(int position, AttributeSyntax attribute, out Binder binder) {
        if (attribute is null)
            throw new ArgumentNullException(nameof(attribute));

        binder = this.GetSpeculativeBinderForAttribute(position, attribute);

        if (binder is null)
            return null;

        var attributeType = (NamedTypeSymbol)binder.BindType(attribute.name, BelteDiagnosticQueue.Discarded, out _)
            .type;

        var boundNode = new ExecutableCodeBinder(attribute, binder.containingMember, binder)
            .BindAttribute(attribute, attributeType, attributedMember: null, BelteDiagnosticQueue.Discarded);

        return boundNode;
    }

    private int CheckAndAdjustPositionForSpeculativeAttribute(int position) {
        position = CheckAndAdjustPosition(position);

        SyntaxToken token = root.FindToken(position);

        if (position == 0 && position != token.span.start)
            return position;

        var node = (BelteSyntaxNode)token.parent;

        if (position == node.span.start) {
            if (node is TypeDeclarationSyntax typeDecl)
                position = typeDecl.openBrace.span.start;

            var methodDecl = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();

            if (methodDecl?.span?.start == position)
                position = methodDecl.identifier.span.start;
        }

        return position;
    }

    private protected override IOperation GetOperationCore(SyntaxNode node, CancellationToken cancellationToken) {
        var bnode = (BelteSyntaxNode)node;
        CheckSyntaxNode(bnode);
        return this.GetOperationWorker(bnode, cancellationToken);
    }

    internal virtual IOperation GetOperationWorker(BelteSyntaxNode node, CancellationToken cancellationToken) {
        return null;
    }

    internal SymbolInfo GetSymbolInfo(ExpressionSyntax expression, CancellationToken cancellationToken = default) {
        CheckSyntaxNode(expression);

        if (!CanGetSemanticInfo(expression, allowNamedArgumentName: true)) {
            return SymbolInfo.None;
        } else if (SyntaxFacts.IsNamedArgumentName(expression)) {
            return this.GetNamedArgumentSymbolInfo((IdentifierNameSyntax)expression, cancellationToken);
        } else if (IsDeclarationExpressionType(expression, out DeclarationExpressionSyntax parent)) {
            return GetSymbolInfoFromSymbolOrNone(TypeFromVariable(parent.identifier, cancellationToken));
        } else if (expression is DeclarationExpressionSyntax declaration) {
            if (declaration.identifier.kind != SyntaxKind.IdentifierToken)
                return SymbolInfo.None;

            var symbol = GetDeclaredSymbol(declaration.identifier, cancellationToken);

            if (symbol is null)
                return SymbolInfo.None;

            return new SymbolInfo(symbol);
        }

        return this.GetSymbolInfoWorker(expression, SymbolInfoOptions.DefaultOptions, cancellationToken);
    }

    private static bool IsDeclarationExpressionType(SyntaxNode node, out DeclarationExpressionSyntax parent) {
        parent = node.ModifyingRefTypeOrSelf().parent as DeclarationExpressionSyntax;
        return node == parent?.type.SkipRef(out _);
    }

    private static SymbolInfo GetSymbolInfoFromSymbolOrNone(ITypeSymbol type) {
        if (type?.kind != SymbolKind.ErrorType)
            return new SymbolInfo(type);

        return SymbolInfo.None;
    }

    private ITypeSymbol TypeFromVariable(SyntaxToken identifier, CancellationToken cancellationToken) {
        var variable = GetDeclaredSymbol(identifier, cancellationToken);

        switch (variable) {
            case IDataContainerSymbol local:
                return local.type;
            case IFieldSymbol field:
                return field.type;
        }

        return default;
    }

    internal SymbolInfo GetSymbolInfo(
        ConstructorInitializerSyntax constructorInitializer,
        CancellationToken cancellationToken = default) {
        CheckSyntaxNode(constructorInitializer);

        return CanGetSemanticInfo(constructorInitializer)
            ? GetSymbolInfoWorker(constructorInitializer, SymbolInfoOptions.DefaultOptions, cancellationToken)
            : SymbolInfo.None;
    }

    internal SymbolInfo GetSymbolInfo(AttributeSyntax attributeSyntax, CancellationToken cancellationToken = default) {
        CheckSyntaxNode(attributeSyntax);

        return CanGetSemanticInfo(attributeSyntax)
            ? GetSymbolInfoWorker(attributeSyntax, SymbolInfoOptions.DefaultOptions, cancellationToken)
            : SymbolInfo.None;
    }

    internal SymbolInfo GetSpeculativeSymbolInfo(
        int position,
        ExpressionSyntax expression,
        SpeculativeBindingOption bindingOption) {
        if (!CanGetSemanticInfo(expression, isSpeculative: true))
            return SymbolInfo.None;

        BoundNode boundNode = GetSpeculativelyBoundExpression(
            position,
            expression,
            bindingOption,
            out var binder
        );

        if (boundNode is null)
            return SymbolInfo.None;

        var symbolInfo = this.GetSymbolInfoForNode(
            SymbolInfoOptions.DefaultOptions,
            boundNode,
            boundNode,
            boundNodeForSyntacticParent: null,
            binderOpt: binder
        );

        return symbolInfo;
    }

    internal SymbolInfo GetSpeculativeSymbolInfo(int position, AttributeSyntax attribute) {
        BoundNode boundNode = GetSpeculativelyBoundAttribute(position, attribute, out var binder);

        if (boundNode is null)
            return SymbolInfo.None;

        var symbolInfo = this.GetSymbolInfoForNode(
            SymbolInfoOptions.DefaultOptions,
            boundNode,
            boundNode,
            boundNodeForSyntacticParent: null,
            binderOpt: binder
        );

        return symbolInfo;
    }

    internal SymbolInfo GetSpeculativeSymbolInfo(int position, ConstructorInitializerSyntax constructorInitializer) {
        position = CheckAndAdjustPosition(position);

        if (constructorInitializer is null)
            throw new ArgumentNullException(nameof(constructorInitializer));

        var existingConstructorInitializer = this.root.FindToken(position).parent
            .AncestorsAndSelf()
            .OfType<ConstructorInitializerSyntax>()
            .FirstOrDefault();

        if (existingConstructorInitializer is null)
            return SymbolInfo.None;

        MemberSemanticModel memberModel = GetMemberModel(existingConstructorInitializer);

        if (memberModel is null)
            return SymbolInfo.None;

        var binder = memberModel.GetEnclosingBinder(position);

        if (binder is not null) {
            binder = new ExecutableCodeBinder(constructorInitializer, binder.containingMember, binder);

            BoundExpressionStatement bnode = binder.BindConstructorInitializer(
                constructorInitializer,
                BelteDiagnosticQueue.Discarded
            );

            var binfo = GetSymbolInfoFromBoundConstructorInitializer(memberModel, binder, bnode);
            return binfo;
        } else {
            return SymbolInfo.None;
        }
    }

    private static SymbolInfo GetSymbolInfoFromBoundConstructorInitializer(
        MemberSemanticModel memberModel,
        Binder binder,
        BoundExpressionStatement bnode) {
        var expression = bnode.expression;
        return memberModel.GetSymbolInfoForNode(
            SymbolInfoOptions.DefaultOptions,
            expression,
            expression,
            boundNodeForSyntacticParent: null,
            binderOpt: binder
        );
    }

    internal TypeInfo GetTypeInfo(
        ConstructorInitializerSyntax constructorInitializer,
        CancellationToken cancellationToken = default) {
        CheckSyntaxNode(constructorInitializer);

        return CanGetSemanticInfo(constructorInitializer)
            ? GetTypeInfoWorker(constructorInitializer, cancellationToken)
            : BelteTypeInfo.None;
    }

    internal TypeInfo GetTypeInfo(PatternSyntax pattern, CancellationToken cancellationToken = default) {
        CheckSyntaxNode(pattern);
        return GetTypeInfoWorker(pattern, cancellationToken);
    }

    internal TypeInfo GetTypeInfo(ExpressionSyntax expression, CancellationToken cancellationToken = default) {
        CheckSyntaxNode(expression);

        if (!CanGetSemanticInfo(expression)) {
            return BelteTypeInfo.None;
        } else if (IsDeclarationExpressionType(expression, out DeclarationExpressionSyntax parent)) {
            switch (parent.identifier.kind) {
                case SyntaxKind.IdentifierToken:
                    var declarationType = TypeFromVariable(parent.identifier, cancellationToken);
                    var declarationTypeSymbol = (TypeSymbol)declarationType;
                    return new BelteTypeInfo(declarationTypeSymbol, declarationTypeSymbol, Conversion.Identity);
            }
        }

        return GetTypeInfoWorker(expression, cancellationToken);
    }

    internal TypeInfo GetTypeInfo(AttributeSyntax attributeSyntax, CancellationToken cancellationToken = default) {
        CheckSyntaxNode(attributeSyntax);

        return CanGetSemanticInfo(attributeSyntax)
            ? GetTypeInfoWorker(attributeSyntax, cancellationToken)
            : BelteTypeInfo.None;
    }

    internal Conversion GetConversion(SyntaxNode expression, CancellationToken cancellationToken = default) {
        var csnode = (BelteSyntaxNode)expression;

        CheckSyntaxNode(csnode);

        var info = CanGetSemanticInfo(csnode)
            ? GetTypeInfoWorker(csnode, cancellationToken)
            : BelteTypeInfo.None;

        return info.implicitConversion;
    }

    internal TypeInfo GetSpeculativeTypeInfo(
        int position,
        ExpressionSyntax expression,
        SpeculativeBindingOption bindingOption) {
        return GetSpeculativeTypeInfoWorker(position, expression, bindingOption);
    }

    internal BelteTypeInfo GetSpeculativeTypeInfoWorker(
        int position,
        ExpressionSyntax expression,
        SpeculativeBindingOption bindingOption) {
        if (!CanGetSemanticInfo(expression, isSpeculative: true))
            return BelteTypeInfo.None;

        BoundNode boundNode = GetSpeculativelyBoundExpression(position, expression, bindingOption, out _);

        if (boundNode is null)
            return BelteTypeInfo.None;

        var typeInfo = GetTypeInfoForNode(boundNode, boundNode, boundNodeForSyntacticParent: null);

        return typeInfo;
    }

    internal Conversion GetSpeculativeConversion(
        int position,
        ExpressionSyntax expression,
        SpeculativeBindingOption bindingOption) {
        var info = this.GetSpeculativeTypeInfoWorker(position, expression, bindingOption);
        return info.implicitConversion;
    }

    internal ImmutableArray<ISymbol> GetMemberGroup(
        ExpressionSyntax expression,
        CancellationToken cancellationToken = default) {
        CheckSyntaxNode(expression);

        return CanGetSemanticInfo(expression)
            ? this.GetMemberGroupWorker(expression, SymbolInfoOptions.DefaultOptions, cancellationToken)
                .GetPublicSymbols()
            : ImmutableArray<ISymbol>.Empty;
    }

    internal ImmutableArray<ISymbol> GetMemberGroup(
        AttributeSyntax attribute,
        CancellationToken cancellationToken = default) {
        CheckSyntaxNode(attribute);

        return CanGetSemanticInfo(attribute)
            ? this.GetMemberGroupWorker(attribute, SymbolInfoOptions.DefaultOptions, cancellationToken)
                .GetPublicSymbols()
            : ImmutableArray<ISymbol>.Empty;
    }

    internal ImmutableArray<ISymbol> GetMemberGroup(
        ConstructorInitializerSyntax initializer,
        CancellationToken cancellationToken = default) {
        CheckSyntaxNode(initializer);

        return CanGetSemanticInfo(initializer)
            ? this.GetMemberGroupWorker(initializer, SymbolInfoOptions.DefaultOptions, cancellationToken)
                .GetPublicSymbols()
            : ImmutableArray<ISymbol>.Empty;
    }

    internal Optional<object> GetConstantValue(
        ExpressionSyntax expression,
        CancellationToken cancellationToken = default) {
        CheckSyntaxNode(expression);

        return CanGetSemanticInfo(expression)
            ? this.GetConstantValueWorker(expression, cancellationToken)
            : default(Optional<object>);
    }

    internal IAliasSymbol GetAliasInfo(IdentifierNameSyntax nameSyntax, CancellationToken cancellationToken = default) {
        CheckSyntaxNode(nameSyntax);

        if (!CanGetSemanticInfo(nameSyntax))
            return null;

        SymbolInfo info = GetSymbolInfoWorker(
            nameSyntax,
            SymbolInfoOptions.PreferTypeToConstructors | SymbolInfoOptions.PreserveAliases,
            cancellationToken
        );

        return info.symbol as IAliasSymbol;
    }

    internal IAliasSymbol GetSpeculativeAliasInfo(
        int position,
        IdentifierNameSyntax nameSyntax,
        SpeculativeBindingOption bindingOption) {
        BoundNode boundNode = GetSpeculativelyBoundExpression(position, nameSyntax, bindingOption, out var binder);

        if (boundNode is null)
            return null;

        var symbolInfo = this.GetSymbolInfoForNode(
            SymbolInfoOptions.PreferTypeToConstructors | SymbolInfoOptions.PreserveAliases,
            boundNode,
            boundNode,
            boundNodeForSyntacticParent: null,
            binderOpt: binder
        );

        return symbolInfo.symbol as IAliasSymbol;
    }

    internal Binder GetEnclosingBinder(int position) {
        Binder result = GetEnclosingBinderInternal(position);
        return result;
    }

    internal abstract Binder GetEnclosingBinderInternal(int position);

    internal abstract MemberSemanticModel GetMemberModel(SyntaxNode node);

    internal bool IsInTree(SyntaxNode node) {
        return node.syntaxTree == this.syntaxTree;
    }

    private static bool IsInStructuredTriviaOtherThanNameAttribute(BelteSyntaxNode node) {
        while (node is not null) {
            if (node.isStructuredTrivia)
                return true;
            else
                node = node.parentOrStructuredTriviaParent;
        }

        return false;
    }

    private protected int CheckAndAdjustPosition(int position) {
        SyntaxToken unused;
        return CheckAndAdjustPosition(position, out unused);
    }

    private protected int CheckAndAdjustPosition(int position, out SyntaxToken token) {
        int fullStart = this.root.position;
        int fullEnd = this.root.fullSpan.end;
        bool atEOF = position == fullEnd && position == this.syntaxTree.GetRoot().fullSpan.end;

        if ((fullStart <= position && position < fullEnd) || atEOF) {
            // TODO ?
            // token = (atEOF ? (BelteSyntaxNode)this.syntaxTree.GetRoot() : root).FindTokenIncludingCrefAndNameAttributes(position);
            token = (atEOF ? (BelteSyntaxNode)this.syntaxTree.GetRoot() : root).FindToken(position);

            if (position < token.span.start)
                token = token.GetPreviousToken();

            return Math.Max(token.span.start, fullStart);
        } else if (fullStart == fullEnd && position == fullEnd) {
            token = default(SyntaxToken);
            return fullStart;
        }

        throw new ArgumentOutOfRangeException(nameof(position), position, "PositionIsNotWithinSyntax");
    }

    private protected int GetAdjustedNodePosition(SyntaxNode node) {
        var fullSpan = this.root.fullSpan;
        var position = node.span.start;

        SyntaxToken firstToken = node.GetFirstToken(includeZeroWidth: false);

        if (firstToken.node is not null) {
            int betterPosition = firstToken.span.start;

            if (betterPosition < node.span.end)
                position = betterPosition;
        }

        if (fullSpan.length == 0) {
            return position;
        } else if (position == fullSpan.end) {
            return CheckAndAdjustPosition(position - 1);
        } else if (node.isFabricated || node.containsDiagnostics || node.width == 0 || node.IsPartOfStructuredTrivia()) {
            return CheckAndAdjustPosition(position);
        } else {
            return position;
        }
    }

    private protected void CheckSyntaxNode(BelteSyntaxNode syntax) {
        if (syntax is null)
            throw new ArgumentNullException(nameof(syntax));

        if (!IsInTree(syntax))
            throw new ArgumentException("SyntaxNodeIsNotWithinSynt");
    }

    private void CheckModelAndSyntaxNodeToSpeculate(BelteSyntaxNode syntax) {
        if (syntax is null)
            throw new ArgumentNullException(nameof(syntax));

        if (this.isSpeculativeSemanticModel)
            throw new InvalidOperationException("ChainingSpeculativeModelIsNotSupported");

        if (this.compilation.ContainsSyntaxTree(syntax.syntaxTree))
            throw new ArgumentException("SpeculatedSyntaxNodeCannotBelongToCurrentCompilation");
    }

    internal ImmutableArray<ISymbol> LookupSymbols(
        int position,
        NamespaceOrTypeSymbol container = null,
        string name = null) {
        var options = LookupOptions.Default;
        return LookupSymbolsInternal(position, container, name, options, useBaseReferenceAccessibility: false);
    }

    internal new ImmutableArray<ISymbol> LookupBaseMembers(int position, string name = null) {
        return LookupSymbolsInternal(
            position,
            container: null,
            name: name,
            options: LookupOptions.Default,
            useBaseReferenceAccessibility: true
        );
    }

    internal ImmutableArray<ISymbol> LookupStaticMembers(
        int position,
        NamespaceOrTypeSymbol container = null,
        string name = null) {
        return LookupSymbolsInternal(
            position,
            container,
            name,
            LookupOptions.MustNotBeInstance,
            useBaseReferenceAccessibility: false
        );
    }

    internal ImmutableArray<ISymbol> LookupNamespacesAndTypes(
        int position,
        NamespaceOrTypeSymbol container = null,
        string name = null) {
        return LookupSymbolsInternal(
            position,
            container,
            name,
            LookupOptions.NamespacesOrTypesOnly,
            useBaseReferenceAccessibility: false
        );
    }

    internal new ImmutableArray<ISymbol> LookupLabels(int position, string name = null) {
        // TODO
        throw new NotImplementedException();
        // return LookupSymbolsInternal(
        //     position,
        //     container: null,
        //     name: name,
        //     options: LookupOptions.LabelsOnly,
        //     useBaseReferenceAccessibility: false
        // );
    }

    private ImmutableArray<ISymbol> LookupSymbolsInternal(
        int position,
        NamespaceOrTypeSymbol container,
        string name,
        LookupOptions options,
        bool useBaseReferenceAccessibility) {
        if (useBaseReferenceAccessibility)
            options |= LookupOptions.UseBaseReferenceAccessibility;

        // TODO
        // options.ThrowIfInvalid();

        SyntaxToken token;
        position = CheckAndAdjustPosition(position, out token);

        var binder = GetEnclosingBinder(position);

        if (binder is null)
            return ImmutableArray<ISymbol>.Empty;

        if (useBaseReferenceAccessibility) {
            TypeSymbol containingType = binder.containingType;
            TypeSymbol baseType = null;

            if (containingType is null || (object)(baseType = containingType.baseType) is null) {
                throw new ArgumentException(
                    "Not a valid position for a call to LookupBaseMembers (must be in a type with a base type)",
                    nameof(position)
                );
            }

            container = baseType;
        }

        var info = LookupSymbolsInfo.GetInstance();
        info.filterName = name;

        if (container is null)
            binder.AddLookupSymbolsInfo(info, options);
        else
            binder.AddMemberLookupSymbolsInfo(info, container, options, binder);

        var results = ArrayBuilder<ISymbol>.GetInstance(info.Count);

        if (name is null) {
            foreach (string foundName in info.names)
                AppendSymbolsWithName(results, foundName, binder, container, options, info);
        } else {
            AppendSymbolsWithName(results, name, binder, container, options, info);
        }

        info.Free();

        if (name is null)
            results.RemoveWhere(static (symbol, _, _) => !symbol.canBeReferencedByName, arg: default(VoidResult));

        return results.ToImmutableAndFree();
    }

    private void AppendSymbolsWithName(
        ArrayBuilder<ISymbol> results,
        string name,
        Binder binder,
        NamespaceOrTypeSymbol container,
        LookupOptions options,
        LookupSymbolsInfo info) {
        LookupSymbolsInfo.IArityEnumerable arities;
        Symbol uniqueSymbol;

        if (info.TryGetAritiesAndUniqueSymbol(name, out arities, out uniqueSymbol)) {
            if (uniqueSymbol is not null) {
                results.Add(RemapSymbolIfNecessary(uniqueSymbol));
            } else {
                if (arities is not null) {
                    foreach (var arity in arities)
                        this.AppendSymbolsWithNameAndArity(results, name, arity, binder, container, options);
                } else {
                    this.AppendSymbolsWithNameAndArity(results, name, 0, binder, container, options);
                }
            }
        }
    }

    private void AppendSymbolsWithNameAndArity(
        ArrayBuilder<ISymbol> results,
        string name,
        int arity,
        Binder binder,
        NamespaceOrTypeSymbol container,
        LookupOptions options) {
        var lookupResult = LookupResult.GetInstance();

        binder.LookupSymbolsSimpleName(
            lookupResult,
            container,
            name,
            arity,
            basesBeingResolved: null,
            options: options,
            errorLocation: null,
            diagnose: false
        );

        if (lookupResult.isMultiViable) {
            if (lookupResult.symbols.Any(
                t => t.kind == SymbolKind.NamedType || t.kind == SymbolKind.Namespace || t.kind == SymbolKind.ErrorType
                )) {
                bool wasError;
                Symbol singleSymbol = binder.ResultSymbol(
                    lookupResult,
                    name,
                    arity,
                    this.root,
                    BelteDiagnosticQueue.Discarded,
                    out wasError,
                    container,
                    options
                );

                if (!wasError) {
                    results.Add(RemapSymbolIfNecessary(singleSymbol));
                } else {
                    foreach (var symbol in lookupResult.symbols)
                        results.Add(RemapSymbolIfNecessary(symbol));
                }
            } else {
                foreach (var symbol in lookupResult.symbols)
                    results.Add(RemapSymbolIfNecessary(symbol));
            }
        }

        lookupResult.Free();
    }

    private Symbol RemapSymbolIfNecessary(Symbol symbol) {
        switch (symbol) {
            case DataContainerSymbol _:
            case ParameterSymbol _:
            case MethodSymbol { methodKind: MethodKind.Lambda }:
                return RemapSymbolIfNecessaryCore(symbol);
            default:
                return symbol;
        }
    }

    internal abstract Symbol RemapSymbolIfNecessaryCore(Symbol symbol);

    internal bool IsAccessible(int position, Symbol symbol) {
        position = CheckAndAdjustPosition(position);

        if (symbol is null)
            throw new ArgumentNullException(nameof(symbol));

        var binder = this.GetEnclosingBinder(position);

        if (binder is not null)
            return binder.IsAccessible(symbol, null);

        return false;
    }

    private bool IsInTypeofExpression(int position) {
        var token = this.root.FindToken(position);
        var curr = token.parent;

        while (curr != this.root) {
            if (curr.kind == SyntaxKind.TypeOfExpression)
                return true;

            curr = curr.parentOrStructuredTriviaParent;
        }

        return false;
    }

    internal SymbolInfo GetSymbolInfoForNode(
        SymbolInfoOptions options,
        BoundNode lowestBoundNode,
        BoundNode highestBoundNode,
        BoundNode boundNodeForSyntacticParent,
        Binder binderOpt) {
        BoundExpression boundExpr;

        switch (lowestBoundNode) {
            case BoundExpression boundExpr2:
                boundExpr = boundExpr2;
                break;
            default:
                return SymbolInfo.None;
        }

        OneOrMany<Symbol> symbols = GetSemanticSymbols(
            boundExpr,
            boundNodeForSyntacticParent,
            binderOpt,
            options,
            out LookupResultKind resultKind,
            out ImmutableArray<Symbol> unusedMemberGroup
        );

        if (highestBoundNode is BoundExpression highestBoundExpr) {
            LookupResultKind highestResultKind;
            ImmutableArray<Symbol> unusedHighestMemberGroup;
            OneOrMany<Symbol> highestSymbols = GetSemanticSymbols(
                highestBoundExpr,
                boundNodeForSyntacticParent,
                binderOpt,
                options,
                out highestResultKind,
                out unusedHighestMemberGroup
            );

            if ((symbols.Count != 1 ||
                resultKind == LookupResultKind.OverloadResolutionFailure) && highestSymbols.Count > 0) {
                symbols = highestSymbols;
                resultKind = highestResultKind;
            } else if (highestResultKind != LookupResultKind.Empty && highestResultKind < resultKind) {
                resultKind = highestResultKind;
                // } else if (highestBoundExpr.kind == BoundKind.TypeOrValueExpression) {
                //     symbols = highestSymbols;
                //     resultKind = highestResultKind;
                //     isDynamic = highestIsDynamic;
            } else if (highestBoundExpr.kind == BoundKind.UnaryOperator) {
                if (IsUserDefinedTrueOrFalse((BoundUnaryOperator)highestBoundExpr)) {
                    symbols = highestSymbols;
                    resultKind = highestResultKind;
                }
            }
        }

        if (resultKind == LookupResultKind.Empty) {
            return SymbolInfoFactory.Create(ImmutableArray<Symbol>.Empty, LookupResultKind.Empty, isDynamic);
        } else {
            var builder = ArrayBuilder<Symbol>.GetInstance(symbols.Count);

            foreach (Symbol symbol in symbols) {
                AddUnwrappingErrorTypes(builder, symbol);
            }

            symbols = builder.ToOneOrManyAndFree();
        }

        if ((options & SymbolInfoOptions.ResolveAliases) != 0)
            symbols = UnwrapAliases(symbols);

        if (resultKind == LookupResultKind.Viable && symbols.Count > 1)
            resultKind = LookupResultKind.OverloadResolutionFailure;

        return SymbolInfoFactory.Create(symbols, resultKind, isDynamic);
    }

    private static void AddUnwrappingErrorTypes(ArrayBuilder<Symbol> builder, Symbol s) {
        var originalErrorSymbol = s.originalDefinition as ErrorTypeSymbol;

        if (originalErrorSymbol is not null)
            builder.AddRange(originalErrorSymbol.candidateSymbols);
        else
            builder.Add(s);
    }

    private static bool IsUserDefinedTrueOrFalse(BoundUnaryOperator @operator) {
        UnaryOperatorKind operatorKind = @operator.operatorKind;
        return operatorKind == UnaryOperatorKind.UserDefinedTrue || operatorKind == UnaryOperatorKind.UserDefinedFalse;
    }

    internal BelteTypeInfo GetTypeInfoForNode(
        BoundNode lowestBoundNode,
        BoundNode highestBoundNode,
        BoundNode boundNodeForSyntacticParent) {
        BoundPattern pattern = lowestBoundNode as BoundPattern ?? highestBoundNode as BoundPattern;

        if (pattern is not null) {
            return new BelteTypeInfo(
                pattern.InputType,
                pattern.NarrowedType,
                compilation.Conversions.ClassifyBuiltInConversion(pattern.InputType, pattern.NarrowedType, isChecked: false, ref discardedUseSiteInfo));
        }

        var boundExpr = lowestBoundNode as BoundExpression;
        var highestBoundExpr = highestBoundNode as BoundExpression;

        if (boundExpr != null &&
            !(boundNodeForSyntacticParent != null &&
              boundNodeForSyntacticParent.Syntax.kind == SyntaxKind.ObjectCreationExpression &&
              ((ObjectCreationExpressionSyntax)boundNodeForSyntacticParent.Syntax).Type == boundExpr.Syntax)) // Do not return any type information for a ObjectCreationExpressionSyntax.Type node.
        {
            // TODO: Should parenthesized expression really not have symbols? At least for C#, I'm not sure that
            // is right. For example, C# allows the assignment statement:
            //    (i) = 9;
            // So I don't assume this code should special case parenthesized expressions.
            TypeSymbol type = null;
            NullabilityInfo nullability = boundExpr.TopLevelNullability;

            if (boundExpr.HasExpressionType()) {
                type = boundExpr.Type;

                switch (boundExpr) {
                    case BoundLocal local: {
                            // Use of local before declaration requires some additional fixup.
                            // Due to complications around implicit locals and type inference, we do not
                            // try to obtain a type of a local when it is used before declaration, we use
                            // a special error type symbol. However, semantic model should return the same
                            // type information for usage of a local before and after its declaration.
                            // We will detect the use before declaration cases and replace the error type
                            // symbol with the one obtained from the local. It should be safe to get the type
                            // from the local at this point.
                            if (type is ExtendedErrorTypeSymbol extended && extended.VariableUsedBeforeDeclaration) {
                                type = local.LocalSymbol.Type;
                                nullability = local.LocalSymbol.TypeWithAnnotations.NullableAnnotation.ToNullabilityInfo(type);
                            }
                            break;
                        }
                    case BoundConvertedTupleLiteral { SourceTuple: BoundTupleLiteral original }: {
                            // The bound tree fully binds tuple literals. From the language point of
                            // view, however, converted tuple literals represent tuple conversions
                            // from tuple literal expressions which may or may not have types
                            type = original.Type;
                            break;
                        }
                }
            }

            // we match highestBoundExpr.Kind to various kind frequently, so cache it here.
            // use NoOp kind for the case when highestBoundExpr == null - NoOp will not match anything below.
            var highestBoundExprKind = highestBoundExpr?.Kind ?? BoundKind.NoOpStatement;
            TypeSymbol convertedType;
            NullabilityInfo convertedNullability;
            Conversion conversion;

            if (highestBoundExprKind == BoundKind.Lambda) // the enclosing conversion is explicit
            {
                var lambda = (BoundLambda)highestBoundExpr;
                convertedType = lambda.Type;
                // The bound tree always fully binds lambda and anonymous functions. From the language point of
                // view, however, anonymous functions converted to a real delegate type should only have a
                // ConvertedType, not a Type. So set Type to null here. Otherwise you get the edge case where both
                // Type and ConvertedType are the same, but the conversion isn't Identity.
                type = null;
                nullability = default;
                convertedNullability = new NullabilityInfo(CodeAnalysis.NullableAnnotation.NotAnnotated, CodeAnalysis.NullableFlowState.NotNull);
                conversion = new Conversion(ConversionKind.AnonymousFunction, lambda.Symbol, false);
            } else if ((highestBoundExpr as BoundConversion)?.Conversion.IsTupleLiteralConversion == true) {
                var tupleLiteralConversion = (BoundConversion)highestBoundExpr;
                if (tupleLiteralConversion.Operand.Kind == BoundKind.ConvertedTupleLiteral) {
                    var convertedTuple = (BoundConvertedTupleLiteral)tupleLiteralConversion.Operand;
                    type = convertedTuple.SourceTuple.Type;
                    nullability = convertedTuple.TopLevelNullability;
                } else {
                    (type, nullability) = getTypeAndNullability(tupleLiteralConversion.Operand);
                }

                (convertedType, convertedNullability) = getTypeAndNullability(tupleLiteralConversion);
                conversion = tupleLiteralConversion.Conversion;
            } else if (highestBoundExprKind == BoundKind.FixedLocalCollectionInitializer) {
                var initializer = (BoundFixedLocalCollectionInitializer)highestBoundExpr;
                (convertedType, convertedNullability) = getTypeAndNullability(initializer);
                (type, nullability) = getTypeAndNullability(initializer.Expression);

                // the most pertinent conversion is the pointer conversion
                conversion = BoundNode.GetConversion(initializer.ElementPointerConversion, initializer.ElementPointerPlaceholder);
            } else if (boundExpr is BoundConvertedSwitchExpression { WasTargetTyped: true } convertedSwitch) {
                if (highestBoundExpr is BoundConversion { ConversionKind: ConversionKind.SwitchExpression, Conversion: var convertedSwitchConversion }) {
                    // There was an implicit cast.
                    type = convertedSwitch.NaturalTypeOpt;
                    convertedType = convertedSwitch.Type;
                    convertedNullability = convertedSwitch.TopLevelNullability;
                    conversion = convertedSwitchConversion.IsValid ? convertedSwitchConversion : Conversion.NoConversion;
                } else {
                    // There was an explicit cast on top of this
                    type = convertedSwitch.NaturalTypeOpt;
                    (convertedType, convertedNullability) = (type, nullability);
                    conversion = Conversion.Identity;
                }
            } else if (boundExpr is BoundConditionalOperator { WasTargetTyped: true } cond) {
                if (highestBoundExpr is BoundConversion { ConversionKind: ConversionKind.ConditionalExpression }) {
                    // There was an implicit cast.
                    type = cond.NaturalTypeOpt;
                    convertedType = cond.Type;
                    convertedNullability = nullability;
                    conversion = Conversion.MakeConditionalExpression(ImmutableArray<Conversion>.Empty);
                } else {
                    // There was an explicit cast on top of this.
                    type = cond.NaturalTypeOpt;
                    (convertedType, convertedNullability) = (type, nullability);
                    conversion = Conversion.Identity;
                }
            } else if (boundExpr is BoundCollectionExpression convertedCollection) {
                type = null;
                if (highestBoundExpr is BoundConversion { ConversionKind: ConversionKind.CollectionExpression or ConversionKind.NoConversion, Conversion: var convertedCollectionConversion }) {
                    convertedType = highestBoundExpr.Type;
                    convertedNullability = convertedCollection.TopLevelNullability;
                    conversion = convertedCollectionConversion;
                } else if (highestBoundExpr is BoundConversion { ConversionKind: ConversionKind.ImplicitNullable, Conversion.UnderlyingConversions: [{ Kind: ConversionKind.CollectionExpression }] } boundConversion) {
                    convertedType = highestBoundExpr.Type;
                    convertedNullability = convertedCollection.TopLevelNullability;
                    conversion = boundConversion.Conversion;
                } else {
                    // Explicit cast or error scenario like `object x = [];`
                    convertedNullability = nullability;
                    convertedType = null;
                    conversion = Conversion.Identity;
                }
            } else if (highestBoundExpr != null && highestBoundExpr != boundExpr && highestBoundExpr.HasExpressionType()) {
                (convertedType, convertedNullability) = getTypeAndNullability(highestBoundExpr);
                if (highestBoundExprKind != BoundKind.Conversion) {
                    conversion = Conversion.Identity;
                } else if (((BoundConversion)highestBoundExpr).Operand.Kind != BoundKind.Conversion) {
                    conversion = highestBoundExpr.GetConversion();
                    if (conversion.Kind == ConversionKind.AnonymousFunction) {
                        // See comment above: anonymous functions do not have a type
                        type = null;
                        nullability = default;
                    }
                } else {
                    // There is a sequence of conversions; we use ClassifyConversionFromExpression to report the most pertinent.
                    var binder = this.GetEnclosingBinder(boundExpr.Syntax.Span.Start);
                    var discardedUseSiteInfo = CompoundUseSiteInfo<AssemblySymbol>.Discarded;
                    conversion = binder.Conversions.ClassifyConversionFromExpression(boundExpr, convertedType, isChecked: ((BoundConversion)highestBoundExpr).Checked, ref discardedUseSiteInfo);
                }
            } else if (boundNodeForSyntacticParent?.Kind == BoundKind.DelegateCreationExpression) {
                // A delegate creation expression takes the place of a method group or anonymous function conversion.
                var delegateCreation = (BoundDelegateCreationExpression)boundNodeForSyntacticParent;
                (convertedType, convertedNullability) = getTypeAndNullability(delegateCreation);
                switch (boundExpr.Kind) {
                    case BoundKind.MethodGroup: {
                            conversion = new Conversion(ConversionKind.MethodGroup, delegateCreation.MethodOpt, delegateCreation.IsExtensionMethod);
                            break;
                        }
                    case BoundKind.Lambda: {
                            var lambda = (BoundLambda)boundExpr;
                            conversion = new Conversion(ConversionKind.AnonymousFunction, lambda.Symbol, delegateCreation.IsExtensionMethod);
                            break;
                        }
                    case BoundKind.UnboundLambda: {
                            var lambda = ((UnboundLambda)boundExpr).BindForErrorRecovery();
                            conversion = new Conversion(ConversionKind.AnonymousFunction, lambda.Symbol, delegateCreation.IsExtensionMethod);
                            break;
                        }
                    default:
                        conversion = Conversion.Identity;
                        break;
                }
            } else if (boundExpr is BoundConversion { ConversionKind: ConversionKind.MethodGroup, Conversion: var exprConversion, Type: { TypeKind: TypeKind.FunctionPointer }, SymbolOpt: var symbol }) {
                // Because the method group is a separate syntax node from the &, the lowest bound node here is the BoundConversion. However,
                // the conversion represents an implicit method group conversion from a typeless method group to a function pointer type, so
                // we should reflect that in the types and conversion we return.
                convertedType = type;
                convertedNullability = nullability;
                conversion = exprConversion;
                type = null;
                nullability = new NullabilityInfo(CodeAnalysis.NullableAnnotation.NotAnnotated, CodeAnalysis.NullableFlowState.NotNull);
            } else {
                convertedType = type;
                convertedNullability = nullability;
                conversion = Conversion.Identity;
            }

            return new BelteTypeInfo(type, convertedType, nullability, convertedNullability, conversion);
        }

        return BelteTypeInfo.None;

        static (TypeSymbol, NullabilityInfo) getTypeAndNullability(BoundExpression expr) => (expr.Type, expr.TopLevelNullability);
    }

    // Gets the method or property group from a specific bound node.
    // lowestBoundNode: The lowest node in the bound tree associated with node
    // highestBoundNode: The highest node in the bound tree associated with node
    // boundNodeForSyntacticParent: The lowest node in the bound tree associated with node.Parent.
    internal ImmutableArray<Symbol> GetMemberGroupForNode(
        SymbolInfoOptions options,
        BoundNode lowestBoundNode,
        BoundNode boundNodeForSyntacticParent,
        Binder binderOpt) {
        if (lowestBoundNode is BoundExpression boundExpr) {
            LookupResultKind resultKind;
            ImmutableArray<Symbol> memberGroup;
            bool isDynamic;
            GetSemanticSymbols(boundExpr, boundNodeForSyntacticParent, binderOpt, options, out isDynamic, out resultKind, out memberGroup);

            return memberGroup;
        }

        return ImmutableArray<Symbol>.Empty;
    }

    // Gets the indexer group from a specific bound node.
    // lowestBoundNode: The lowest node in the bound tree associated with node
    // highestBoundNode: The highest node in the bound tree associated with node
    // boundNodeForSyntacticParent: The lowest node in the bound tree associated with node.Parent.
    internal ImmutableArray<IPropertySymbol> GetIndexerGroupForNode(
        BoundNode lowestBoundNode,
        Binder binderOpt) {
        var boundExpr = lowestBoundNode as BoundExpression;
        if (boundExpr != null && boundExpr.Kind != BoundKind.TypeExpression) {
            return GetIndexerGroupSemanticSymbols(boundExpr, binderOpt);
        }

        return ImmutableArray<IPropertySymbol>.Empty;
    }

    // Gets symbol info for a type or namespace or alias reference. It is assumed that any error cases will come in
    // as a type whose OriginalDefinition is an error symbol from which the ResultKind can be retrieved.
    internal static SymbolInfo GetSymbolInfoForSymbol(Symbol symbol, SymbolInfoOptions options) {
        Debug.Assert((object)symbol != null);

        // Determine type. Dig through aliases if necessary.
        Symbol unwrapped = UnwrapAlias(symbol);
        TypeSymbol type = unwrapped as TypeSymbol;

        // Determine symbols and resultKind.
        var originalErrorSymbol = (object)type != null ? type.OriginalDefinition as ErrorTypeSymbol : null;

        if ((object)originalErrorSymbol != null) {
            // Error case.
            var symbols = OneOrMany<Symbol>.Empty;

            LookupResultKind resultKind = originalErrorSymbol.ResultKind;
            if (resultKind != LookupResultKind.Empty) {
                symbols = OneOrMany.Create(originalErrorSymbol.CandidateSymbols);
            }

            if ((options & SymbolInfoOptions.ResolveAliases) != 0) {
                symbols = UnwrapAliases(symbols);
            }

            return SymbolInfoFactory.Create(symbols, resultKind, isDynamic: false);
        } else {
            // Non-error case. Use constructor that doesn't require creation of a Symbol array.
            var symbolToReturn = ((options & SymbolInfoOptions.ResolveAliases) != 0) ? unwrapped : symbol;
            return new SymbolInfo(symbolToReturn.GetPublicSymbol());
        }
    }

    // Gets TypeInfo for a type or namespace or alias reference.
    internal static BelteTypeInfo GetTypeInfoForSymbol(Symbol symbol) {
        Debug.Assert((object)symbol != null);

        // Determine type. Dig through aliases if necessary.
        TypeSymbol type = UnwrapAlias(symbol) as TypeSymbol;
        // https://github.com/dotnet/roslyn/issues/35033: Examine this and make sure that we're using the correct nullabilities
        return new BelteTypeInfo(type, type, default, default, Conversion.Identity);
    }

    protected static Symbol UnwrapAlias(Symbol symbol) {
        return symbol is AliasSymbol aliasSym ? aliasSym.Target : symbol;
    }

    protected static OneOrMany<Symbol> UnwrapAliases(OneOrMany<Symbol> symbols) {
        bool anyAliases = false;

        foreach (Symbol symbol in symbols) {
            if (symbol.Kind == SymbolKind.Alias)
                anyAliases = true;
        }

        if (!anyAliases)
            return symbols;

        ArrayBuilder<Symbol> builder = ArrayBuilder<Symbol>.GetInstance();
        foreach (Symbol symbol in symbols) {
            // Caas clients don't want ErrorTypeSymbol in the symbols, but the best guess
            // instead. If no best guess, then nothing is returned.
            AddUnwrappingErrorTypes(builder, UnwrapAlias(symbol));
        }

        return builder.ToOneOrManyAndFree();
    }

    // This is used by other binding APIs to invoke the right binder API
    internal virtual BoundNode Bind(Binder binder, BelteSyntaxNode node, BelteDiagnosticQueue diagnostics) {
        if (Compilation.TestOnlyCompilationData is MemberSemanticModel.MemberSemanticBindingCounter counter) {
            counter.BindCount++;
        }

        switch (node) {
            case ExpressionSyntax expression:
                var parent = expression.Parent;
                return parent.IsKind(SyntaxKind.GotoStatement)
                    ? binder.BindLabel(expression, diagnostics)
                    : binder.BindNamespaceOrTypeOrExpression(expression, diagnostics);
            case StatementSyntax statement:
                return binder.BindStatement(statement, diagnostics);
            case GlobalStatementSyntax globalStatement:
                BoundStatement bound = binder.BindStatement(globalStatement.Statement, diagnostics);
                return new BoundGlobalStatementInitializer(node, bound);
        }

        return null;
    }

    internal virtual ControlFlowAnalysis AnalyzeControlFlow(StatementSyntax firstStatement, StatementSyntax lastStatement) {
        // Only supported on a SyntaxTreeSemanticModel.
        throw new NotSupportedException();
    }

    internal virtual ControlFlowAnalysis AnalyzeControlFlow(StatementSyntax statement) {
        return AnalyzeControlFlow(statement, statement);
    }

    internal virtual DataFlowAnalysis AnalyzeDataFlow(ConstructorInitializerSyntax constructorInitializer) {
        // Only supported on a SyntaxTreeSemanticModel.
        throw new NotSupportedException();
    }

    internal virtual DataFlowAnalysis AnalyzeDataFlow(PrimaryConstructorBaseTypeSyntax primaryConstructorBaseType) {
        // Only supported on a SyntaxTreeSemanticModel.
        throw new NotSupportedException();
    }

    internal virtual DataFlowAnalysis AnalyzeDataFlow(ExpressionSyntax expression) {
        // Only supported on a SyntaxTreeSemanticModel.
        throw new NotSupportedException();
    }

    internal virtual DataFlowAnalysis AnalyzeDataFlow(StatementSyntax firstStatement, StatementSyntax lastStatement) {
        // Only supported on a SyntaxTreeSemanticModel.
        throw new NotSupportedException();
    }

    internal virtual DataFlowAnalysis AnalyzeDataFlow(StatementSyntax statement) {
        return AnalyzeDataFlow(statement, statement);
    }

    internal bool TryGetSpeculativeSemanticModelForMethodBody(int position, BaseMethodDeclarationSyntax method, out SemanticModel speculativeModel) {
        CheckModelAndSyntaxNodeToSpeculate(method);
        var result = TryGetSpeculativeSemanticModelForMethodBodyCore((SyntaxTreeSemanticModel)this, position, method, out PublicSemanticModel speculativeSyntaxTreeModel);
        speculativeModel = speculativeSyntaxTreeModel;
        return result;
    }

    internal abstract bool TryGetSpeculativeSemanticModelForMethodBodyCore(SyntaxTreeSemanticModel parentModel, int position, BaseMethodDeclarationSyntax method, out PublicSemanticModel speculativeModel);

    internal bool TryGetSpeculativeSemanticModelForMethodBody(int position, AccessorDeclarationSyntax accessor, out SemanticModel speculativeModel) {
        CheckModelAndSyntaxNodeToSpeculate(accessor);
        var result = TryGetSpeculativeSemanticModelForMethodBodyCore((SyntaxTreeSemanticModel)this, position, accessor, out PublicSemanticModel speculativeSyntaxTreeModel);
        speculativeModel = speculativeSyntaxTreeModel;
        return result;
    }

    internal abstract bool TryGetSpeculativeSemanticModelForMethodBodyCore(SyntaxTreeSemanticModel parentModel, int position, AccessorDeclarationSyntax accessor, out PublicSemanticModel speculativeModel);

    internal bool TryGetSpeculativeSemanticModel(int position, TypeSyntax type, out SemanticModel speculativeModel, SpeculativeBindingOption bindingOption = SpeculativeBindingOption.BindAsExpression) {
        CheckModelAndSyntaxNodeToSpeculate(type);
        var result = TryGetSpeculativeSemanticModelCore((SyntaxTreeSemanticModel)this, position, type, bindingOption, out PublicSemanticModel speculativeSyntaxTreeModel);
        speculativeModel = speculativeSyntaxTreeModel;
        return result;
    }

    internal abstract bool TryGetSpeculativeSemanticModelCore(SyntaxTreeSemanticModel parentModel, int position, TypeSyntax type, SpeculativeBindingOption bindingOption, out PublicSemanticModel speculativeModel);

    internal bool TryGetSpeculativeSemanticModel(int position, StatementSyntax statement, out SemanticModel speculativeModel) {
        CheckModelAndSyntaxNodeToSpeculate(statement);
        var result = TryGetSpeculativeSemanticModelCore((SyntaxTreeSemanticModel)this, position, statement, out PublicSemanticModel speculativeSyntaxTreeModel);
        speculativeModel = speculativeSyntaxTreeModel;
        return result;
    }

    internal abstract bool TryGetSpeculativeSemanticModelCore(SyntaxTreeSemanticModel parentModel, int position, StatementSyntax statement, out PublicSemanticModel speculativeModel);

    internal bool TryGetSpeculativeSemanticModel(int position, EqualsValueClauseSyntax initializer, out SemanticModel speculativeModel) {
        CheckModelAndSyntaxNodeToSpeculate(initializer);
        var result = TryGetSpeculativeSemanticModelCore((SyntaxTreeSemanticModel)this, position, initializer, out PublicSemanticModel speculativeSyntaxTreeModel);
        speculativeModel = speculativeSyntaxTreeModel;
        return result;
    }

    internal abstract bool TryGetSpeculativeSemanticModelCore(SyntaxTreeSemanticModel parentModel, int position, EqualsValueClauseSyntax initializer, out PublicSemanticModel speculativeModel);

    internal bool TryGetSpeculativeSemanticModel(int position, ArrowExpressionClauseSyntax expressionBody, out SemanticModel speculativeModel) {
        CheckModelAndSyntaxNodeToSpeculate(expressionBody);
        var result = TryGetSpeculativeSemanticModelCore((SyntaxTreeSemanticModel)this, position, expressionBody, out PublicSemanticModel speculativeSyntaxTreeModel);
        speculativeModel = speculativeSyntaxTreeModel;
        return result;
    }

    internal abstract bool TryGetSpeculativeSemanticModelCore(SyntaxTreeSemanticModel parentModel, int position, ArrowExpressionClauseSyntax expressionBody, out PublicSemanticModel speculativeModel);

    internal bool TryGetSpeculativeSemanticModel(int position, ConstructorInitializerSyntax constructorInitializer, out SemanticModel speculativeModel) {
        CheckModelAndSyntaxNodeToSpeculate(constructorInitializer);
        var result = TryGetSpeculativeSemanticModelCore((SyntaxTreeSemanticModel)this, position, constructorInitializer, out PublicSemanticModel speculativeSyntaxTreeModel);
        speculativeModel = speculativeSyntaxTreeModel;
        return result;
    }

    internal abstract bool TryGetSpeculativeSemanticModelCore(SyntaxTreeSemanticModel parentModel, int position, ConstructorInitializerSyntax constructorInitializer, out PublicSemanticModel speculativeModel);

    internal bool TryGetSpeculativeSemanticModel(int position, PrimaryConstructorBaseTypeSyntax constructorInitializer, out SemanticModel speculativeModel) {
        CheckModelAndSyntaxNodeToSpeculate(constructorInitializer);
        var result = TryGetSpeculativeSemanticModelCore((SyntaxTreeSemanticModel)this, position, constructorInitializer, out PublicSemanticModel speculativeSyntaxTreeModel);
        speculativeModel = speculativeSyntaxTreeModel;
        return result;
    }

    internal abstract bool TryGetSpeculativeSemanticModelCore(SyntaxTreeSemanticModel parentModel, int position, PrimaryConstructorBaseTypeSyntax constructorInitializer, out PublicSemanticModel speculativeModel);

    internal bool TryGetSpeculativeSemanticModel(int position, CrefSyntax crefSyntax, out SemanticModel speculativeModel) {
        CheckModelAndSyntaxNodeToSpeculate(crefSyntax);
        var result = TryGetSpeculativeSemanticModelCore((SyntaxTreeSemanticModel)this, position, crefSyntax, out PublicSemanticModel speculativeSyntaxTreeModel);
        speculativeModel = speculativeSyntaxTreeModel;
        return result;
    }

    internal abstract bool TryGetSpeculativeSemanticModelCore(SyntaxTreeSemanticModel parentModel, int position, CrefSyntax crefSyntax, out PublicSemanticModel speculativeModel);

    internal bool TryGetSpeculativeSemanticModel(int position, AttributeSyntax attribute, out SemanticModel speculativeModel) {
        CheckModelAndSyntaxNodeToSpeculate(attribute);

        var binder = GetSpeculativeBinderForAttribute(position, attribute);
        if (binder == null) {
            speculativeModel = null;
            return false;
        }

        AliasSymbol aliasOpt;
        var attributeType = (NamedTypeSymbol)binder.BindType(attribute.Name, BelteDiagnosticQueue.Discarded, out aliasOpt).Type;
        speculativeModel = ((SyntaxTreeSemanticModel)this).CreateSpeculativeAttributeSemanticModel(position, attribute, binder, aliasOpt, attributeType);
        return true;
    }

    internal new abstract CSharpSemanticModel ParentModel {
        get;
    }

    internal new abstract SyntaxTree syntaxTree {
        get;
    }

    internal abstract Conversion ClassifyConversion(ExpressionSyntax expression, ITypeSymbol destination, bool isExplicitInSource = false);

    internal Conversion ClassifyConversion(int position, ExpressionSyntax expression, ITypeSymbol destination, bool isExplicitInSource = false) {
        if ((object)destination == null) {
            throw new ArgumentNullException(nameof(destination));
        }

        TypeSymbol cdestination = destination.EnsureCSharpSymbolOrNull(nameof(destination));

        if (expression.kind == SyntaxKind.DeclarationExpression) {
            // Conversion from a declaration is unspecified.
            return Conversion.NoConversion;
        }

        if (isExplicitInSource) {
            return ClassifyConversionForCast(position, expression, cdestination);
        }

        // Note that it is possible for an expression to be convertible to a type
        // via both an implicit user-defined conversion and an explicit built-in conversion.
        // In that case, this method chooses the implicit conversion.

        position = CheckAndAdjustPosition(position);
        var binder = this.GetEnclosingBinder(position);
        if (binder != null) {
            var bnode = binder.BindExpression(expression, BelteDiagnosticQueue.Discarded);

            if (bnode != null && !cdestination.IsErrorType()) {
                var discardedUseSiteInfo = CompoundUseSiteInfo<AssemblySymbol>.Discarded;

                return binder.Conversions.ClassifyConversionFromExpression(bnode, cdestination, isChecked: binder.CheckOverflowAtRuntime, ref discardedUseSiteInfo);
            }
        }

        return Conversion.NoConversion;
    }

    internal abstract Conversion ClassifyConversionForCast(ExpressionSyntax expression, TypeSymbol destination);

    internal Conversion ClassifyConversionForCast(int position, ExpressionSyntax expression, TypeSymbol destination) {
        if ((object)destination == null) {
            throw new ArgumentNullException(nameof(destination));
        }

        position = CheckAndAdjustPosition(position);
        var binder = this.GetEnclosingBinder(position);
        if (binder != null) {
            var bnode = binder.BindExpression(expression, BelteDiagnosticQueue.Discarded);

            if (bnode != null && !destination.IsErrorType()) {
                var discardedUseSiteInfo = CompoundUseSiteInfo<AssemblySymbol>.Discarded;

                return binder.Conversions.ClassifyConversionFromExpression(bnode, destination, isChecked: binder.CheckOverflowAtRuntime, ref discardedUseSiteInfo, forCast: true);
            }
        }

        return Conversion.NoConversion;
    }

    internal abstract ISymbol GetDeclaredSymbol(
        MemberDeclarationSyntax declarationSyntax,
        CancellationToken cancellationToken = default);

    internal abstract IMethodSymbol GetDeclaredSymbol(
        LocalFunctionStatementSyntax declarationSyntax,
        CancellationToken cancellationToken = default);

    internal abstract IMethodSymbol GetDeclaredSymbol(
        CompilationUnitSyntax declarationSyntax,
        CancellationToken cancellationToken = default);

    internal abstract INamespaceSymbol GetDeclaredSymbol(
        NamespaceDeclarationSyntax declarationSyntax,
        CancellationToken cancellationToken = default);

    internal abstract INamespaceSymbol GetDeclaredSymbol(
        FileScopedNamespaceDeclarationSyntax declarationSyntax,
        CancellationToken cancellationToken = default);

    internal abstract INamedTypeSymbol GetDeclaredSymbol(
        TypeDeclarationSyntax declarationSyntax,
        CancellationToken cancellationToken = default);

    internal abstract IFieldSymbol GetDeclaredSymbol(
        EnumMemberDeclarationSyntax declarationSyntax,
        CancellationToken cancellationToken = default);

    internal abstract IMethodSymbol GetDeclaredSymbol(
        BaseMethodDeclarationSyntax declarationSyntax,
        CancellationToken cancellationToken = default);

    internal abstract ISymbol GetDeclaredSymbol(
        ArgumentSyntax declaratorSyntax,
        CancellationToken cancellationToken = default);

    internal abstract ISymbol GetDeclaredSymbol(
        VariableDeclarationSyntax declarationSyntax,
        CancellationToken cancellationToken = default);

    internal abstract ISymbol GetDeclaredSymbol(
        SyntaxToken declarationSyntax,
        CancellationToken cancellationToken = default);

    internal abstract ILabelSymbol GetDeclaredSymbol(
        SwitchLabelSyntax declarationSyntax,
        CancellationToken cancellationToken = default);

    internal abstract IAliasSymbol GetDeclaredSymbol(
        UsingDirectiveSyntax declarationSyntax,
        CancellationToken cancellationToken = default);

    internal abstract IParameterSymbol GetDeclaredSymbol(
        ParameterSyntax declarationSyntax,
        CancellationToken cancellationToken = default);

    internal abstract ImmutableArray<ISymbol> GetDeclaredSymbols(
        FieldDeclarationSyntax declarationSyntax,
        CancellationToken cancellationToken = default);

    private protected ParameterSymbol GetParameterSymbol(
        ImmutableArray<ParameterSymbol> parameters,
        ParameterSyntax parameter,
        CancellationToken cancellationToken = default) {
        foreach (var symbol in parameters) {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var location in symbol.locations) {
                cancellationToken.ThrowIfCancellationRequested();

                if (location.tree == this.syntaxTree && parameter.span.Contains(location.span))
                    return symbol;
            }
        }

        return null;
    }

    internal abstract ITypeParameterSymbol GetDeclaredSymbol(TypeParameterSyntax typeParameter, CancellationToken cancellationToken = default(CancellationToken));

    internal BinderFlags GetSemanticModelBinderFlags() {
        return this.IgnoresAccessibility
            ? BinderFlags.SemanticModel | BinderFlags.IgnoreAccessibility
            : BinderFlags.SemanticModel;
    }

    internal ILocalSymbol GetDeclaredSymbol(ForEachStatementSyntax forEachStatement) {
        Binder enclosingBinder = this.GetEnclosingBinder(GetAdjustedNodePosition(forEachStatement));

        if (enclosingBinder == null) {
            return null;
        }

        Binder foreachBinder = enclosingBinder.GetBinder(forEachStatement);

        // Binder.GetBinder can fail in presence of syntax errors.
        if (foreachBinder == null) {
            return null;
        }

        LocalSymbol local = foreachBinder.GetDeclaredLocalsForScope(forEachStatement).FirstOrDefault();
        return (local is SourceLocalSymbol { DeclarationKind: LocalDeclarationKind.ForEachIterationVariable } sourceLocal
            ? GetAdjustedLocalSymbol(sourceLocal)
            : local).GetPublicSymbol();
    }

    internal abstract LocalSymbol GetAdjustedLocalSymbol(SourceLocalSymbol originalSymbol);

    internal ILocalSymbol GetDeclaredSymbol(CatchDeclarationSyntax catchDeclaration) {
        BelteSyntaxNode catchClause = catchDeclaration.Parent; //Syntax->Binder map is keyed on clause, not decl
        Debug.Assert(catchClause.kind == SyntaxKind.CatchClause);
        Binder enclosingBinder = this.GetEnclosingBinder(GetAdjustedNodePosition(catchClause));

        if (enclosingBinder == null) {
            return null;
        }

        Binder catchBinder = enclosingBinder.GetBinder(catchClause);

        // Binder.GetBinder can fail in presence of syntax errors.
        if (catchBinder == null) {
            return null;
        }

        catchBinder = enclosingBinder.GetBinder(catchClause);
        LocalSymbol local = catchBinder.GetDeclaredLocalsForScope(catchClause).FirstOrDefault();
        return ((object)local != null && local.DeclarationKind == LocalDeclarationKind.CatchVariable)
            ? local.GetPublicSymbol()
            : null;
    }

    private OneOrMany<Symbol> GetSemanticSymbols(
        BoundExpression boundNode,
        BoundNode boundNodeForSyntacticParent,
        Binder binderOpt,
        SymbolInfoOptions options,
        out LookupResultKind resultKind,
        out ImmutableArray<Symbol> memberGroup) {
        memberGroup = ImmutableArray<Symbol>.Empty;
        OneOrMany<Symbol> symbols = OneOrMany<Symbol>.Empty;
        resultKind = LookupResultKind.Viable;

        switch (boundNode.kind) {
            case BoundKind.MethodGroup:
                symbols = GetMethodGroupSemanticSymbols((BoundMethodGroup)boundNode, boundNodeForSyntacticParent, binderOpt, out resultKind, out isDynamic, out memberGroup);
                break;

            case BoundKind.PropertyGroup:
                symbols = GetPropertyGroupSemanticSymbols((BoundPropertyGroup)boundNode, boundNodeForSyntacticParent, binderOpt, out resultKind, out memberGroup);
                break;

            case BoundKind.BadExpression: {
                    var expr = (BoundBadExpression)boundNode;
                    resultKind = expr.ResultKind;

                    if (expr.Syntax.kind is SyntaxKind.ObjectCreationExpression or SyntaxKind.ImplicitObjectCreationExpression) {
                        if (resultKind == LookupResultKind.NotCreatable) {
                            return OneOrMany.Create(expr.Symbols);
                        } else if (expr.Type.IsDelegateType()) {
                            resultKind = LookupResultKind.Empty;
                            return symbols;
                        }

                        memberGroup = expr.Symbols;
                    }

                    return OneOrMany.Create(expr.Symbols);
                }

            case BoundKind.DelegateCreationExpression:
                break;

            case BoundKind.TypeExpression: {
                    var boundType = (BoundTypeExpression)boundNode;

                    // Watch out for not creatable types within object creation syntax
                    if (boundNodeForSyntacticParent != null &&
                       boundNodeForSyntacticParent.Syntax.kind == SyntaxKind.ObjectCreationExpression &&
                       ((ObjectCreationExpressionSyntax)boundNodeForSyntacticParent.Syntax).Type == boundType.Syntax &&
                       boundNodeForSyntacticParent.Kind == BoundKind.BadExpression &&
                       ((BoundBadExpression)boundNodeForSyntacticParent).ResultKind == LookupResultKind.NotCreatable) {
                        resultKind = LookupResultKind.NotCreatable;
                    }

                    // could be a type or alias.
                    var typeSymbol = boundType.AliasOpt ?? (Symbol)boundType.Type;

                    var originalErrorType = typeSymbol.OriginalDefinition as ErrorTypeSymbol;
                    if ((object)originalErrorType != null) {
                        resultKind = originalErrorType.ResultKind;
                        symbols = OneOrMany.Create(originalErrorType.CandidateSymbols);
                    } else {
                        symbols = OneOrMany.Create(typeSymbol);
                    }
                }
                break;

            case BoundKind.TypeOrValueExpression: {
                    // If we're seeing a node of this kind, then we failed to resolve the member access
                    // as either a type or a property/field/event/local/parameter.  In such cases,
                    // the second interpretation applies so just visit the node for that.
                    BoundExpression valueExpression = ((BoundTypeOrValueExpression)boundNode).Data.ValueExpression;
                    return GetSemanticSymbols(valueExpression, boundNodeForSyntacticParent, binderOpt, options, out isDynamic, out resultKind, out memberGroup);
                }

            case BoundKind.Call: {
                    // Either overload resolution succeeded for this call or it did not. If it
                    // did not succeed then we've stashed the original method symbols from the
                    // method group, and we should use those as the symbols displayed for the
                    // call. If it did succeed then we did not stash any symbols; just fall
                    // through to the default case.

                    var call = (BoundCall)boundNode;
                    if (call.OriginalMethodsOpt.IsDefault) {
                        if ((object)call.Method != null) {
                            symbols = CreateReducedExtensionMethodIfPossible(call);
                            resultKind = call.ResultKind;
                        }
                    } else {
                        symbols = StaticCast<Symbol>.From(CreateReducedExtensionMethodsFromOriginalsIfNecessary(call, Compilation));
                        resultKind = call.ResultKind;
                    }
                }
                break;

            case BoundKind.FunctionPointerInvocation: {
                    var invocation = (BoundFunctionPointerInvocation)boundNode;
                    symbols = OneOrMany.Create<Symbol>(invocation.FunctionPointer);
                    resultKind = invocation.ResultKind;
                    break;
                }

            case BoundKind.UnconvertedAddressOfOperator: {
                    // We try to match the results given for a similar piece of syntax here: bad invocations.
                    // A BoundUnconvertedAddressOfOperator represents this syntax: &M
                    // Similarly, a BoundCall for a bad invocation represents this syntax: M(args)
                    // Calling GetSymbolInfo on the syntax will return an array of candidate symbols that were
                    // looked up, but calling GetMemberGroup will return an empty array. So, we ignore the member
                    // group result in the call below.
                    symbols = GetMethodGroupSemanticSymbols(
                        ((BoundUnconvertedAddressOfOperator)boundNode).Operand,
                        boundNodeForSyntacticParent, binderOpt, out resultKind, out isDynamic, methodGroup: out _);
                    break;
                }

            case BoundKind.IndexerAccess: {
                    // As for BoundCall, pull out stashed candidates if overload resolution failed.

                    BoundIndexerAccess indexerAccess = (BoundIndexerAccess)boundNode;
                    Debug.Assert((object)indexerAccess.Indexer != null);

                    resultKind = indexerAccess.ResultKind;

                    ImmutableArray<PropertySymbol> originalIndexersOpt = indexerAccess.OriginalIndexersOpt;
                    symbols = originalIndexersOpt.IsDefault ? OneOrMany.Create<Symbol>(indexerAccess.Indexer) : StaticCast<Symbol>.From(OneOrMany.Create(originalIndexersOpt));
                }
                break;

            case BoundKind.ImplicitIndexerAccess:
                return GetSemanticSymbols(((BoundImplicitIndexerAccess)boundNode).IndexerOrSliceAccess,
                    boundNodeForSyntacticParent, binderOpt, options, out isDynamic, out resultKind, out memberGroup);

            case BoundKind.EventAssignmentOperator:
                var eventAssignment = (BoundEventAssignmentOperator)boundNode;
                isDynamic = eventAssignment.IsDynamic;
                var eventSymbol = eventAssignment.Event;
                var methodSymbol = eventAssignment.IsAddition ? eventSymbol.AddMethod : eventSymbol.RemoveMethod;
                if ((object)methodSymbol == null) {
                    symbols = OneOrMany<Symbol>.Empty;
                    resultKind = LookupResultKind.Empty;
                } else {
                    symbols = OneOrMany.Create<Symbol>(methodSymbol);
                    resultKind = eventAssignment.ResultKind;
                }
                break;

            case BoundKind.EventAccess when boundNodeForSyntacticParent is BoundEventAssignmentOperator { ResultKind: LookupResultKind.Viable } parentOperator &&
                                            boundNode.ExpressionSymbol is Symbol accessSymbol &&
                                            boundNode != parentOperator.Argument &&
                                            parentOperator.Event.Equals(accessSymbol, TypeCompareKind.AllNullableIgnoreOptions):
                // When we're looking at the left-hand side of an event assignment, we synthesize a BoundEventAccess node. This node does not have
                // nullability information, however, so if we're in that case then we need to grab the event symbol from the parent event assignment
                // which does have the nullability-reinferred symbol
                symbols = OneOrMany.Create<Symbol>(parentOperator.Event);
                resultKind = parentOperator.ResultKind;
                break;

            case BoundKind.Conversion:
                var conversion = (BoundConversion)boundNode;
                isDynamic = conversion.ConversionKind.IsDynamic();
                if (!isDynamic) {
                    if ((conversion.ConversionKind == ConversionKind.MethodGroup) && conversion.IsExtensionMethod) {
                        var symbol = conversion.SymbolOpt;
                        Debug.Assert((object)symbol != null);
                        symbols = OneOrMany.Create<Symbol>(ReducedExtensionMethodSymbol.Create(symbol));
                        resultKind = conversion.ResultKind;
                    } else if (conversion.ConversionKind.IsUserDefinedConversion()) {
                        GetSymbolsAndResultKind(conversion, conversion.SymbolOpt, conversion.OriginalUserDefinedConversionsOpt, out symbols, out resultKind);
                    } else {
                        goto default;
                    }
                }
                break;

            case BoundKind.BinaryOperator:
                GetSymbolsAndResultKind((BoundBinaryOperator)boundNode, out isDynamic, ref resultKind, ref symbols);
                break;

            case BoundKind.UnaryOperator:
                GetSymbolsAndResultKind((BoundUnaryOperator)boundNode, out isDynamic, ref resultKind, ref symbols);
                break;

            case BoundKind.UserDefinedConditionalLogicalOperator:
                var @operator = (BoundUserDefinedConditionalLogicalOperator)boundNode;
                isDynamic = false;
                GetSymbolsAndResultKind(@operator, @operator.LogicalOperator, @operator.OriginalUserDefinedOperatorsOpt, out symbols, out resultKind);
                break;

            case BoundKind.CompoundAssignmentOperator:
                GetSymbolsAndResultKind((BoundCompoundAssignmentOperator)boundNode, out isDynamic, ref resultKind, ref symbols);
                break;

            case BoundKind.IncrementOperator:
                GetSymbolsAndResultKind((BoundIncrementOperator)boundNode, out isDynamic, ref resultKind, ref symbols);
                break;

            case BoundKind.AwaitExpression:
                var await = (BoundAwaitExpression)boundNode;
                isDynamic = await.AwaitableInfo.IsDynamic;
                goto default;

            case BoundKind.ConditionalOperator:
                var conditional = (BoundConditionalOperator)boundNode;
                Debug.Assert(conditional.ExpressionSymbol is null);
                isDynamic = conditional.IsDynamic;
                goto default;

            case BoundKind.Attribute: {
                    Debug.Assert(boundNodeForSyntacticParent == null);
                    var attribute = (BoundAttribute)boundNode;
                    resultKind = attribute.ResultKind;

                    // If attribute name bound to a single named type or an error type
                    // with a single named type candidate symbol, we will return constructors
                    // of the named type in the semantic info.
                    // Otherwise, we will return the error type candidate symbols.

                    var namedType = (NamedTypeSymbol)attribute.Type;
                    if (namedType.IsErrorType()) {
                        Debug.Assert(resultKind != LookupResultKind.Viable);
                        var errorType = (ErrorTypeSymbol)namedType;
                        var candidateSymbols = errorType.CandidateSymbols;

                        // If error type has a single named type candidate symbol, we want to
                        // use that type for symbol info.
                        if (candidateSymbols.Length == 1 && candidateSymbols[0] is NamedTypeSymbol) {
                            namedType = (NamedTypeSymbol)candidateSymbols[0];
                        } else {
                            symbols = OneOrMany.Create(candidateSymbols);
                            break;
                        }
                    }

                    AdjustSymbolsForObjectCreation(attribute, namedType, attribute.Constructor, binderOpt, ref resultKind, ref symbols, ref memberGroup);
                }
                break;

            case BoundKind.QueryClause: {
                    var query = (BoundQueryClause)boundNode;
                    var builder = ArrayBuilder<Symbol>.GetInstance();
                    if (query.Operation != null && (object)query.Operation.ExpressionSymbol != null) builder.Add(query.Operation.ExpressionSymbol);
                    if ((object)query.DefinedSymbol != null) builder.Add(query.DefinedSymbol);
                    if (query.Cast != null && (object)query.Cast.ExpressionSymbol != null) builder.Add(query.Cast.ExpressionSymbol);
                    symbols = builder.ToOneOrManyAndFree();
                }
                break;

            case BoundKind.DynamicInvocation:
                var dynamicInvocation = (BoundDynamicInvocation)boundNode;
                Debug.Assert(dynamicInvocation.ExpressionSymbol is null);
                memberGroup = dynamicInvocation.ApplicableMethods.Cast<MethodSymbol, Symbol>();
                symbols = OneOrMany.Create(memberGroup);
                isDynamic = true;
                break;

            case BoundKind.DynamicCollectionElementInitializer:
                var collectionInit = (BoundDynamicCollectionElementInitializer)boundNode;
                Debug.Assert(collectionInit.ExpressionSymbol is null);
                memberGroup = collectionInit.ApplicableMethods.Cast<MethodSymbol, Symbol>();
                symbols = OneOrMany.Create(memberGroup);
                isDynamic = true;
                break;

            case BoundKind.DynamicIndexerAccess:
                var dynamicIndexer = (BoundDynamicIndexerAccess)boundNode;
                Debug.Assert(dynamicIndexer.ExpressionSymbol is null);
                memberGroup = dynamicIndexer.ApplicableIndexers.Cast<PropertySymbol, Symbol>();
                symbols = OneOrMany.Create(memberGroup);
                isDynamic = true;
                break;

            case BoundKind.DynamicMemberAccess:
                Debug.Assert((object)boundNode.ExpressionSymbol == null);
                isDynamic = true;
                break;

            case BoundKind.DynamicObjectCreationExpression:
                var objectCreation = (BoundDynamicObjectCreationExpression)boundNode;
                memberGroup = objectCreation.ApplicableMethods.Cast<MethodSymbol, Symbol>();
                symbols = OneOrMany.Create(memberGroup);
                isDynamic = true;
                break;

            case BoundKind.ObjectCreationExpression:
                var boundObjectCreation = (BoundObjectCreationExpression)boundNode;

                if ((object)boundObjectCreation.Constructor != null) {
                    Debug.Assert(boundObjectCreation.ConstructorsGroup.Contains(boundObjectCreation.Constructor));
                    symbols = OneOrMany.Create<Symbol>(boundObjectCreation.Constructor);
                } else if (boundObjectCreation.ConstructorsGroup.Length > 0) {
                    symbols = StaticCast<Symbol>.From(OneOrMany.Create(boundObjectCreation.ConstructorsGroup));
                    resultKind = resultKind.WorseResultKind(LookupResultKind.OverloadResolutionFailure);
                }

                memberGroup = boundObjectCreation.ConstructorsGroup.Cast<MethodSymbol, Symbol>();
                break;

            case BoundKind.ThisReference:
            case BoundKind.BaseReference: {
                    Binder binder = binderOpt ?? GetEnclosingBinder(GetAdjustedNodePosition(boundNode.Syntax));
                    NamedTypeSymbol containingType = binder.ContainingType;
                    var containingMember = binder.ContainingMember();

                    var thisParam = GetThisParameter(boundNode.Type, containingType, containingMember, out resultKind);
                    symbols = thisParam != null ? OneOrMany.Create<Symbol>(thisParam) : OneOrMany<Symbol>.Empty;
                }
                break;

            case BoundKind.FromEndIndexExpression: {
                    var fromEndIndexExpression = (BoundFromEndIndexExpression)boundNode;
                    if ((object)fromEndIndexExpression.MethodOpt != null) {
                        symbols = OneOrMany.Create<Symbol>(fromEndIndexExpression.MethodOpt);
                    }
                    break;
                }

            case BoundKind.RangeExpression: {
                    var rangeExpression = (BoundRangeExpression)boundNode;
                    if ((object)rangeExpression.MethodOpt != null) {
                        symbols = OneOrMany.Create<Symbol>(rangeExpression.MethodOpt);
                    }
                    break;
                }

            default: {
                    if (boundNode.ExpressionSymbol is Symbol symbol) {
                        symbols = OneOrMany.Create(symbol);
                        resultKind = boundNode.ResultKind;
                    }
                }
                break;
        }

        if (boundNodeForSyntacticParent != null && (options & SymbolInfoOptions.PreferConstructorsToType) != 0) {
            // Adjust symbols to get the constructors if we're T in a "new T(...)".
            AdjustSymbolsForObjectCreation(boundNode, boundNodeForSyntacticParent, binderOpt, ref resultKind, ref symbols, ref memberGroup);
        }

        return symbols;
    }

    private static ParameterSymbol GetThisParameter(TypeSymbol typeOfThis, NamedTypeSymbol containingType, Symbol containingMember, out LookupResultKind resultKind) {
        if ((object)containingMember == null || (object)containingType == null) {
            // not in a member of a type (can happen when speculating)
            resultKind = LookupResultKind.NotReferencable;
            return new ThisParameterSymbol(containingMember as MethodSymbol, typeOfThis);
        }

        ParameterSymbol thisParam;

        switch (containingMember.Kind) {
            case SymbolKind.Method:
            case SymbolKind.Field:
            case SymbolKind.Property:
                if (containingMember.IsStatic) {
                    // in a static member
                    resultKind = LookupResultKind.StaticInstanceMismatch;
                    thisParam = new ThisParameterSymbol(containingMember as MethodSymbol, containingType);
                } else {
                    if ((object)typeOfThis == ErrorTypeSymbol.UnknownResultType) {
                        // in an instance member, but binder considered this/base unreferenceable
                        thisParam = new ThisParameterSymbol(containingMember as MethodSymbol, containingType);
                        resultKind = LookupResultKind.NotReferencable;
                    } else {
                        switch (containingMember.Kind) {
                            case SymbolKind.Method:
                                resultKind = LookupResultKind.Viable;
                                thisParam = containingMember.EnclosingThisSymbol();
                                break;

                            // Fields and properties can't access 'this' since
                            // initializers are run in the constructor
                            case SymbolKind.Field:
                            case SymbolKind.Property:
                                resultKind = LookupResultKind.NotReferencable;
                                thisParam = containingMember.EnclosingThisSymbol() ?? new ThisParameterSymbol(null, containingType);
                                break;

                            default:
                                throw ExceptionUtilities.UnexpectedValue(containingMember.Kind);
                        }
                    }
                }
                break;

            default:
                thisParam = new ThisParameterSymbol(containingMember as MethodSymbol, typeOfThis);
                resultKind = LookupResultKind.NotReferencable;
                break;
        }

        return thisParam;
    }

    private static void GetSymbolsAndResultKind(BoundUnaryOperator unaryOperator, out bool isDynamic, ref LookupResultKind resultKind, ref OneOrMany<Symbol> symbols) {
        UnaryOperatorKind operandType = unaryOperator.OperatorKind.OperandTypes();
        isDynamic = unaryOperator.OperatorKind.IsDynamic();

        if (operandType == 0 || operandType == UnaryOperatorKind.UserDefined || unaryOperator.ResultKind != LookupResultKind.Viable) {
            if (!isDynamic) {
                GetSymbolsAndResultKind(unaryOperator, unaryOperator.MethodOpt, unaryOperator.OriginalUserDefinedOperatorsOpt, out symbols, out resultKind);
            }
        } else {
            Debug.Assert((object)unaryOperator.MethodOpt == null && unaryOperator.OriginalUserDefinedOperatorsOpt.IsDefaultOrEmpty);
            UnaryOperatorKind op = unaryOperator.OperatorKind.Operator();
            symbols = OneOrMany.Create<Symbol>(new SynthesizedIntrinsicOperatorSymbol(unaryOperator.Operand.Type.StrippedType(),
                                                                                      OperatorFacts.UnaryOperatorNameFromOperatorKind(op, isChecked: unaryOperator.OperatorKind.IsChecked()),
                                                                                      unaryOperator.Type.StrippedType()));
            resultKind = unaryOperator.ResultKind;
        }
    }

    private static void GetSymbolsAndResultKind(BoundIncrementOperator increment, out bool isDynamic, ref LookupResultKind resultKind, ref OneOrMany<Symbol> symbols) {
        UnaryOperatorKind operandType = increment.OperatorKind.OperandTypes();
        isDynamic = increment.OperatorKind.IsDynamic();

        if (operandType == 0 || operandType == UnaryOperatorKind.UserDefined || increment.ResultKind != LookupResultKind.Viable) {
            if (!isDynamic) {
                GetSymbolsAndResultKind(increment, increment.MethodOpt, increment.OriginalUserDefinedOperatorsOpt, out symbols, out resultKind);
            }
        } else {
            Debug.Assert((object)increment.MethodOpt == null && increment.OriginalUserDefinedOperatorsOpt.IsDefaultOrEmpty);
            UnaryOperatorKind op = increment.OperatorKind.Operator();
            TypeSymbol opType = increment.Operand.Type.StrippedType();
            symbols = OneOrMany.Create<Symbol>(new SynthesizedIntrinsicOperatorSymbol(opType,
                                                                                      OperatorFacts.UnaryOperatorNameFromOperatorKind(op, isChecked: increment.OperatorKind.IsChecked()),
                                                                                      opType));
            resultKind = increment.ResultKind;
        }
    }

    private static void GetSymbolsAndResultKind(BoundBinaryOperator binaryOperator, out bool isDynamic, ref LookupResultKind resultKind, ref OneOrMany<Symbol> symbols) {
        BinaryOperatorKind operandType = binaryOperator.OperatorKind.OperandTypes();
        BinaryOperatorKind op = binaryOperator.OperatorKind.Operator();
        isDynamic = binaryOperator.OperatorKind.IsDynamic();

        if (operandType == 0 || operandType == BinaryOperatorKind.UserDefined || binaryOperator.ResultKind != LookupResultKind.Viable || binaryOperator.OperatorKind.IsLogical()) {
            if (!isDynamic) {
                GetSymbolsAndResultKind(binaryOperator, binaryOperator.Method, binaryOperator.OriginalUserDefinedOperatorsOpt, out symbols, out resultKind);
            }
        } else {
            Debug.Assert((object)binaryOperator.Method == null && binaryOperator.OriginalUserDefinedOperatorsOpt.IsDefaultOrEmpty);

            if (!isDynamic &&
                (op == BinaryOperatorKind.Equal || op == BinaryOperatorKind.NotEqual) &&
                ((binaryOperator.Left.IsLiteralNull() && binaryOperator.Right.Type.IsNullableType()) ||
                 (binaryOperator.Right.IsLiteralNull() && binaryOperator.Left.Type.IsNullableType())) &&
                binaryOperator.Type.SpecialType == SpecialType.System_Boolean) {
                // Comparison of a nullable type with null, return corresponding operator for Object.
                var objectType = binaryOperator.Type.ContainingAssembly.GetSpecialType(SpecialType.System_Object);

                symbols = OneOrMany.Create<Symbol>(new SynthesizedIntrinsicOperatorSymbol(objectType,
                                                                                          OperatorFacts.BinaryOperatorNameFromOperatorKind(op, isChecked: binaryOperator.OperatorKind.IsChecked()),
                                                                                          objectType,
                                                                                          binaryOperator.Type));
            } else {
                symbols = OneOrMany.Create(GetIntrinsicOperatorSymbol(op, isDynamic,
                                                                      binaryOperator.Left.Type,
                                                                      binaryOperator.Right.Type,
                                                                      binaryOperator.Type,
                                                                      binaryOperator.OperatorKind.IsChecked()));
            }

            resultKind = binaryOperator.ResultKind;
        }
    }

    private static Symbol GetIntrinsicOperatorSymbol(BinaryOperatorKind op, bool isDynamic, TypeSymbol leftType, TypeSymbol rightType, TypeSymbol returnType, bool isChecked) {
        if (!isDynamic) {
            leftType = leftType.StrippedType();
            rightType = rightType.StrippedType();
            returnType = returnType.StrippedType();
        } else {
            Debug.Assert(returnType.IsDynamic());

            if ((object)leftType == null) {
                Debug.Assert(rightType.IsDynamic());
                leftType = rightType;
            } else if ((object)rightType == null) {
                Debug.Assert(leftType.IsDynamic());
                rightType = leftType;
            }
        }
        return new SynthesizedIntrinsicOperatorSymbol(leftType,
                                                      OperatorFacts.BinaryOperatorNameFromOperatorKind(op, isChecked),
                                                      rightType,
                                                      returnType);
    }

    private static void GetSymbolsAndResultKind(BoundCompoundAssignmentOperator compoundAssignment, out bool isDynamic, ref LookupResultKind resultKind, ref OneOrMany<Symbol> symbols) {
        BinaryOperatorKind operandType = compoundAssignment.Operator.Kind.OperandTypes();
        BinaryOperatorKind op = compoundAssignment.Operator.Kind.Operator();
        isDynamic = compoundAssignment.Operator.Kind.IsDynamic();

        if (operandType == 0 || operandType == BinaryOperatorKind.UserDefined || compoundAssignment.ResultKind != LookupResultKind.Viable) {
            if (!isDynamic) {
                GetSymbolsAndResultKind(compoundAssignment, compoundAssignment.Operator.Method, compoundAssignment.OriginalUserDefinedOperatorsOpt, out symbols, out resultKind);
            }
        } else {
            Debug.Assert((object)compoundAssignment.Operator.Method == null && compoundAssignment.OriginalUserDefinedOperatorsOpt.IsDefaultOrEmpty);

            symbols = OneOrMany.Create(GetIntrinsicOperatorSymbol(op, isDynamic,
                                                                  compoundAssignment.Operator.LeftType,
                                                                  compoundAssignment.Operator.RightType,
                                                                  compoundAssignment.Operator.ReturnType,
                                                                  compoundAssignment.Operator.Kind.IsChecked()));
            resultKind = compoundAssignment.ResultKind;
        }
    }

    private static void GetSymbolsAndResultKind(BoundExpression node, Symbol symbolOpt, ImmutableArray<MethodSymbol> originalCandidates, out OneOrMany<Symbol> symbols, out LookupResultKind resultKind) {
        if (!ReferenceEquals(symbolOpt, null)) {
            symbols = OneOrMany.Create(symbolOpt);
            resultKind = node.ResultKind;
        } else if (!originalCandidates.IsDefault) {
            symbols = StaticCast<Symbol>.From(OneOrMany.Create(originalCandidates));
            resultKind = node.ResultKind;
        } else {
            symbols = OneOrMany<Symbol>.Empty;
            resultKind = LookupResultKind.Empty;
        }
    }

    // In cases where we are binding C in "[C(...)]", the bound nodes return the symbol for the type. However, we've
    // decided that we want this case to return the constructor of the type instead. This affects attributes.
    // This method checks for this situation and adjusts the syntax and method or property group.
    private void AdjustSymbolsForObjectCreation(
        BoundExpression boundNode,
        BoundNode boundNodeForSyntacticParent,
        Binder binderOpt,
        ref LookupResultKind resultKind,
        ref OneOrMany<Symbol> symbols,
        ref ImmutableArray<Symbol> memberGroup) {
        NamedTypeSymbol typeSymbol = null;
        MethodSymbol constructor = null;

        // Check if boundNode.Syntax is the type-name child of an Attribute.
        SyntaxNode parentSyntax = boundNodeForSyntacticParent.Syntax;
        if (parentSyntax != null &&
            parentSyntax == boundNode.Syntax.Parent &&
            parentSyntax.kind == SyntaxKind.Attribute && ((AttributeSyntax)parentSyntax).Name == boundNode.Syntax) {
            var unwrappedSymbols = UnwrapAliases(symbols);

            switch (boundNodeForSyntacticParent.Kind) {
                case BoundKind.Attribute:
                    BoundAttribute boundAttribute = (BoundAttribute)boundNodeForSyntacticParent;

                    if (unwrappedSymbols.Count == 1 && unwrappedSymbols[0].Kind == SymbolKind.NamedType) {
                        Debug.Assert(resultKind != LookupResultKind.Viable ||
                            TypeSymbol.Equals((TypeSymbol)unwrappedSymbols[0], boundAttribute.Type.GetNonErrorGuess(), TypeCompareKind.ConsiderEverything2));

                        typeSymbol = (NamedTypeSymbol)unwrappedSymbols[0];
                        constructor = boundAttribute.Constructor;
                        resultKind = resultKind.WorseResultKind(boundAttribute.ResultKind);
                    }
                    break;

                case BoundKind.BadExpression:
                    BoundBadExpression boundBadExpression = (BoundBadExpression)boundNodeForSyntacticParent;
                    if (unwrappedSymbols.Count == 1) {
                        resultKind = resultKind.WorseResultKind(boundBadExpression.ResultKind);
                        typeSymbol = unwrappedSymbols[0] as NamedTypeSymbol;
                    }
                    break;

                default:
                    throw ExceptionUtilities.UnexpectedValue(boundNodeForSyntacticParent.Kind);
            }

            AdjustSymbolsForObjectCreation(boundNode, typeSymbol, constructor, binderOpt, ref resultKind, ref symbols, ref memberGroup);
        }
    }

    private void AdjustSymbolsForObjectCreation(
        BoundNode lowestBoundNode,
        NamedTypeSymbol typeSymbolOpt,
        MethodSymbol constructorOpt,
        Binder binderOpt,
        ref LookupResultKind resultKind,
        ref OneOrMany<Symbol> symbols,
        ref ImmutableArray<Symbol> memberGroup) {
        Debug.Assert(lowestBoundNode != null);
        Debug.Assert(binderOpt != null || IsInTree(lowestBoundNode.Syntax));

        if ((object)typeSymbolOpt != null) {
            Debug.Assert(lowestBoundNode.Syntax != null);

            // Filter typeSymbol's instance constructors by accessibility.
            // If all the instance constructors are inaccessible, we retain
            // all of them for correct semantic info.
            Binder binder = binderOpt ?? GetEnclosingBinder(GetAdjustedNodePosition(lowestBoundNode.Syntax));
            ImmutableArray<MethodSymbol> candidateConstructors;

            if (binder != null) {
                var instanceConstructors = typeSymbolOpt.IsInterfaceType() && (object)typeSymbolOpt.ComImportCoClass != null ?
                    typeSymbolOpt.ComImportCoClass.InstanceConstructors :
                    typeSymbolOpt.InstanceConstructors;

                var discardedUseSiteInfo = CompoundUseSiteInfo<AssemblySymbol>.Discarded;
                candidateConstructors = binder.FilterInaccessibleConstructors(instanceConstructors, allowProtectedConstructorsOfBaseType: false, useSiteInfo: ref discardedUseSiteInfo);

                if ((object)constructorOpt == null ? !candidateConstructors.Any() : !candidateConstructors.Contains(constructorOpt)) {
                    // All instance constructors are inaccessible or if the specified constructor
                    // isn't a candidate, then we retain all of them for correct semantic info.
                    Debug.Assert(resultKind != LookupResultKind.Viable);
                    candidateConstructors = instanceConstructors;
                }
            } else {
                candidateConstructors = ImmutableArray<MethodSymbol>.Empty;
            }

            if ((object)constructorOpt != null) {
                Debug.Assert(candidateConstructors.Contains(constructorOpt));
                symbols = OneOrMany.Create<Symbol>(constructorOpt);
            } else if (candidateConstructors.Length > 0) {
                symbols = StaticCast<Symbol>.From(OneOrMany.Create(candidateConstructors));
                Debug.Assert(resultKind != LookupResultKind.Viable);
                resultKind = resultKind.WorseResultKind(LookupResultKind.OverloadResolutionFailure);
            }

            memberGroup = candidateConstructors.Cast<MethodSymbol, Symbol>();
        }
    }

    private static ImmutableArray<MethodSymbol> FilterOverriddenOrHiddenMethods(ImmutableArray<MethodSymbol> methods) {
        if (methods.Length <= 1)
            return methods;

        var hiddenSymbols = new HashSet<Symbol>();

        foreach (MethodSymbol method in methods) {
            OverriddenOrHiddenMembersResult overriddenOrHiddenMembers = method.OverriddenOrHiddenMembers;

            foreach (Symbol overridden in overriddenOrHiddenMembers.OverriddenMembers)
                hiddenSymbols.Add(overridden);

            foreach (Symbol hidden in overriddenOrHiddenMembers.HiddenMembers)
                hiddenSymbols.Add(hidden);
        }

        return methods.WhereAsArray((m, hiddenSymbols) => !hiddenSymbols.Contains(m), hiddenSymbols);
    }

    // Get the symbols and possible method group associated with a method group bound node, as
    // they should be exposed through GetSemanticInfo.
    // NB: It is not safe to pass a null binderOpt during speculative binding.
    //
    // If the parent node of the method group syntax node provides information (such as arguments)
    // that allows us to return more specific symbols (a specific overload or applicable candidates)
    // we return these. The complete set of symbols of the method group is then returned in methodGroup parameter.
    private OneOrMany<Symbol> GetMethodGroupSemanticSymbols(
        BoundMethodGroup boundNode,
        BoundNode boundNodeForSyntacticParent,
        Binder binderOpt,
        out LookupResultKind resultKind,
        out bool isDynamic,
        out ImmutableArray<Symbol> methodGroup) {
        Debug.Assert(binderOpt != null || IsInTree(boundNode.Syntax));

        OneOrMany<Symbol> symbols = OneOrMany<Symbol>.Empty;

        resultKind = boundNode.ResultKind;
        if (resultKind == LookupResultKind.Empty) {
            resultKind = LookupResultKind.Viable;
        }

        isDynamic = false;

        // The method group needs filtering.
        Binder binder = binderOpt ?? GetEnclosingBinder(GetAdjustedNodePosition(boundNode.Syntax));
        methodGroup = GetReducedAndFilteredMethodGroupSymbols(binder, boundNode).Cast<MethodSymbol, Symbol>();

        // We want to get the actual node chosen by overload resolution, if possible.
        if (boundNodeForSyntacticParent != null) {
            switch (boundNodeForSyntacticParent.Kind) {
                case BoundKind.Call:
                    // If we are looking for info on M in M(args), we want the symbol that overload resolution
                    // chose for M.
                    var call = (BoundCall)boundNodeForSyntacticParent;
                    InvocationExpressionSyntax invocation = call.Syntax as InvocationExpressionSyntax;
                    if (invocation != null && invocation.Expression.SkipParens() == ((ExpressionSyntax)boundNode.Syntax).SkipParens() && (object)call.Method != null) {
                        if (call.OriginalMethodsOpt.IsDefault) {
                            // Overload resolution succeeded.
                            symbols = CreateReducedExtensionMethodIfPossible(call);
                            resultKind = LookupResultKind.Viable;
                        } else {
                            resultKind = call.ResultKind.WorseResultKind(LookupResultKind.OverloadResolutionFailure);
                            symbols = StaticCast<Symbol>.From(CreateReducedExtensionMethodsFromOriginalsIfNecessary(call, Compilation));
                        }
                    }
                    break;

                case BoundKind.DelegateCreationExpression:
                    // If we are looking for info on "M" in "new Action(M)"
                    // we want to get the symbol that overload resolution chose for M, not the whole method group M.
                    var delegateCreation = (BoundDelegateCreationExpression)boundNodeForSyntacticParent;
                    if (delegateCreation.Argument == boundNode && (object)delegateCreation.MethodOpt != null) {
                        symbols = CreateReducedExtensionMethodIfPossible(delegateCreation, boundNode.ReceiverOpt);
                    }
                    break;

                case BoundKind.Conversion:
                    // If we are looking for info on "M" in "(Action)M"
                    // we want to get the symbol that overload resolution chose for M, not the whole method group M.
                    var conversion = (BoundConversion)boundNodeForSyntacticParent;

                    var method = conversion.SymbolOpt;
                    if ((object)method != null) {
                        Debug.Assert(conversion.ConversionKind == ConversionKind.MethodGroup);

                        if (conversion.IsExtensionMethod) {
                            method = ReducedExtensionMethodSymbol.Create(method);
                        }

                        symbols = OneOrMany.Create((Symbol)method);
                        resultKind = conversion.ResultKind;
                    } else {
                        goto default;
                    }

                    break;

                case BoundKind.DynamicInvocation:
                    var dynamicInvocation = (BoundDynamicInvocation)boundNodeForSyntacticParent;
                    symbols = OneOrMany.Create(dynamicInvocation.ApplicableMethods.Cast<MethodSymbol, Symbol>());
                    isDynamic = true;
                    break;

                case BoundKind.BadExpression:
                    // If the bad expression has symbol(s) from this method group, it better indicates any problems.
                    ImmutableArray<Symbol> myMethodGroup = methodGroup;

                    symbols = OneOrMany.Create(((BoundBadExpression)boundNodeForSyntacticParent).Symbols.WhereAsArray((sym, myMethodGroup) => myMethodGroup.Contains(sym), myMethodGroup));
                    if (symbols.Any()) {
                        resultKind = ((BoundBadExpression)boundNodeForSyntacticParent).ResultKind;
                    }
                    break;

                case BoundKind.NameOfOperator:
                    symbols = OneOrMany.Create(methodGroup);
                    resultKind = resultKind.WorseResultKind(LookupResultKind.MemberGroup);
                    break;

                default:
                    symbols = OneOrMany.Create(methodGroup);
                    if (symbols.Count > 0) {
                        resultKind = resultKind.WorseResultKind(LookupResultKind.OverloadResolutionFailure);
                    }
                    break;
            }
        } else if (methodGroup.Length == 1 && !boundNode.HasAnyErrors) {
            // During speculative binding, there won't be a parent bound node. The parent bound
            // node may also be absent if the syntactic parent has errors or if one is simply
            // not specified (see SemanticModel.GetSymbolInfoForNode). However, if there's exactly
            // one candidate, then we should probably succeed.

            symbols = OneOrMany.Create(methodGroup);
            if (symbols.Count > 0) {
                resultKind = resultKind.WorseResultKind(LookupResultKind.OverloadResolutionFailure);
            }
        }

        if (!symbols.Any()) {
            // If we didn't find a better set of symbols, then assume this is a method group that didn't
            // get resolved. Return all members of the method group, with a resultKind of OverloadResolutionFailure
            // (unless the method group already has a worse result kind).
            symbols = OneOrMany.Create(methodGroup);
            if (!isDynamic && resultKind > LookupResultKind.OverloadResolutionFailure) {
                resultKind = LookupResultKind.OverloadResolutionFailure;
            }
        }

        return symbols;
    }

    // NB: It is not safe to pass a null binderOpt during speculative binding.
    private OneOrMany<Symbol> GetPropertyGroupSemanticSymbols(
        BoundPropertyGroup boundNode,
        BoundNode boundNodeForSyntacticParent,
        Binder binderOpt,
        out LookupResultKind resultKind,
        out ImmutableArray<Symbol> propertyGroup) {
        Debug.Assert(binderOpt != null || IsInTree(boundNode.Syntax));

        OneOrMany<Symbol> symbols = OneOrMany<Symbol>.Empty;

        resultKind = boundNode.ResultKind;
        if (resultKind == LookupResultKind.Empty) {
            resultKind = LookupResultKind.Viable;
        }

        // The property group needs filtering.
        propertyGroup = boundNode.Properties.Cast<PropertySymbol, Symbol>();

        // We want to get the actual node chosen by overload resolution, if possible.
        if (boundNodeForSyntacticParent != null) {
            switch (boundNodeForSyntacticParent.Kind) {
                case BoundKind.IndexerAccess:
                    // If we are looking for info on P in P[args], we want the symbol that overload resolution
                    // chose for P.
                    var indexer = (BoundIndexerAccess)boundNodeForSyntacticParent;
                    var elementAccess = indexer.Syntax as ElementAccessExpressionSyntax;
                    if (elementAccess != null && elementAccess.Expression == boundNode.Syntax && (object)indexer.Indexer != null) {
                        if (indexer.OriginalIndexersOpt.IsDefault) {
                            // Overload resolution succeeded.
                            symbols = OneOrMany.Create<Symbol>(indexer.Indexer);
                            resultKind = LookupResultKind.Viable;
                        } else {
                            resultKind = indexer.ResultKind.WorseResultKind(LookupResultKind.OverloadResolutionFailure);
                            symbols = StaticCast<Symbol>.From(OneOrMany.Create(indexer.OriginalIndexersOpt));
                        }
                    }
                    break;

                case BoundKind.BadExpression:
                    // If the bad expression has symbol(s) from this property group, it better indicates any problems.
                    ImmutableArray<Symbol> myPropertyGroup = propertyGroup;

                    symbols = OneOrMany.Create(((BoundBadExpression)boundNodeForSyntacticParent).Symbols.WhereAsArray((sym, myPropertyGroup) => myPropertyGroup.Contains(sym), myPropertyGroup));
                    if (symbols.Any()) {
                        resultKind = ((BoundBadExpression)boundNodeForSyntacticParent).ResultKind;
                    }
                    break;
            }
        } else if (propertyGroup.Length == 1 && !boundNode.HasAnyErrors) {
            // During speculative binding, there won't be a parent bound node. The parent bound
            // node may also be absent if the syntactic parent has errors or if one is simply
            // not specified (see SemanticModel.GetSymbolInfoForNode). However, if there's exactly
            // one candidate, then we should probably succeed.

            // If we're speculatively binding and there's exactly one candidate, then we should probably succeed.
            symbols = OneOrMany.Create(propertyGroup);
        }

        if (!symbols.Any()) {
            // If we didn't find a better set of symbols, then assume this is a property group that didn't
            // get resolved. Return all members of the property group, with a resultKind of OverloadResolutionFailure
            // (unless the property group already has a worse result kind).
            symbols = OneOrMany.Create(propertyGroup);
            if (resultKind > LookupResultKind.OverloadResolutionFailure) {
                resultKind = LookupResultKind.OverloadResolutionFailure;
            }
        }

        return symbols;
    }

    private SymbolInfo GetNamedArgumentSymbolInfo(IdentifierNameSyntax identifierNameSyntax, CancellationToken cancellationToken) {
        Debug.Assert(SyntaxFacts.IsNamedArgumentName(identifierNameSyntax));

        // Argument names do not have bound nodes associated with them, so we cannot use the usual
        // GetSymbolInfo mechanism. Instead, we just do the following:
        //   1. Find the containing invocation.
        //   2. Call GetSymbolInfo on that.
        //   3. For each method or indexer in the return semantic info, find the argument
        //      with the given name (if any).
        //   4. Use the ResultKind in that semantic info and any symbols to create the semantic info
        //      for the named argument.
        //   5. Type is always null, as is constant value.

        string argumentName = identifierNameSyntax.Identifier.ValueText;
        if (argumentName.Length == 0)
            return SymbolInfo.None;    // missing name.

        // argument could be an argument of a tuple expression
        // var x = (Identifier: 1, AnotherIdentifier: 2);
        var parent3 = identifierNameSyntax.Parent.Parent.Parent;
        if (parent3.IsKind(SyntaxKind.TupleExpression)) {
            var tupleArgument = (ArgumentSyntax)identifierNameSyntax.Parent.Parent;
            var tupleElement = GetDeclaredSymbol(tupleArgument, cancellationToken);
            return (object)tupleElement == null ? SymbolInfo.None : new SymbolInfo(tupleElement);
        }

        if (parent3.IsKind(SyntaxKind.PropertyPatternClause) || parent3.IsKind(SyntaxKind.PositionalPatternClause)) {
            return GetSymbolInfoWorker(identifierNameSyntax, SymbolInfoOptions.DefaultOptions, cancellationToken);
        }

        BelteSyntaxNode containingInvocation = parent3.Parent;
        SymbolInfo containingInvocationInfo = GetSymbolInfoWorker(containingInvocation, SymbolInfoOptions.PreferConstructorsToType | SymbolInfoOptions.ResolveAliases, cancellationToken);

        if ((object)containingInvocationInfo.Symbol != null) {
            ParameterSymbol param = FindNamedParameter(containingInvocationInfo.Symbol.GetSymbol().GetParameters(), argumentName);
            return (object)param == null ? SymbolInfo.None : new SymbolInfo(param.GetPublicSymbol());
        } else {
            var symbols = ArrayBuilder<ISymbol>.GetInstance();

            foreach (ISymbol invocationSym in containingInvocationInfo.CandidateSymbols) {
                switch (invocationSym.Kind) {
                    case SymbolKind.Method:
                    case SymbolKind.Property:
                        break; // Could have parameters.
                    default:
                        continue; // Definitely doesn't have parameters.
                }
                ParameterSymbol param = FindNamedParameter(invocationSym.GetSymbol().GetParameters(), argumentName);
                if ((object)param != null) {
                    symbols.Add(param.GetPublicSymbol());
                }
            }

            if (symbols.Count == 0) {
                symbols.Free();
                return SymbolInfo.None;
            } else {
                return new SymbolInfo(symbols.ToImmutableAndFree(), containingInvocationInfo.CandidateReason);
            }
        }
    }

    private static ParameterSymbol FindNamedParameter(ImmutableArray<ParameterSymbol> parameters, string argumentName) {
        foreach (ParameterSymbol param in parameters) {
            if (param.Name == argumentName)
                return param;
        }

        return null;
    }

    internal static ImmutableArray<MethodSymbol> GetReducedAndFilteredMethodGroupSymbols(Binder binder, BoundMethodGroup node) {
        var methods = ArrayBuilder<MethodSymbol>.GetInstance();
        var filteredMethods = ArrayBuilder<MethodSymbol>.GetInstance();
        var resultKind = LookupResultKind.Empty;
        var typeArguments = node.TypeArgumentsOpt;

        // Non-extension methods.
        if (node.Methods.Any()) {
            // This is the only place we care about overridden/hidden methods.  If there aren't methods
            // in the method group, there's only one fallback candidate and extension methods never override
            // or hide instance methods or other extension methods.
            ImmutableArray<MethodSymbol> nonHiddenMethods = FilterOverriddenOrHiddenMethods(node.Methods);
            Debug.Assert(nonHiddenMethods.Any()); // Something must be hiding, so can't all be hidden.

            foreach (var method in nonHiddenMethods) {
                MergeReducedAndFilteredMethodGroupSymbol(
                    methods,
                    filteredMethods,
                    new SingleLookupResult(node.ResultKind, method, node.LookupError),
                    typeArguments,
                    null,
                    ref resultKind,
                    binder.Compilation);
            }
        } else {
            var otherSymbol = node.LookupSymbolOpt;
            if (((object)otherSymbol != null) && (otherSymbol.Kind == SymbolKind.Method)) {
                MergeReducedAndFilteredMethodGroupSymbol(
                    methods,
                    filteredMethods,
                    new SingleLookupResult(node.ResultKind, otherSymbol, node.LookupError),
                    typeArguments,
                    null,
                    ref resultKind,
                    binder.Compilation);
            }
        }

        var receiver = node.ReceiverOpt;
        var name = node.Name;

        // Extension methods, all scopes.
        if (node.SearchExtensionMethods) {
            Debug.Assert(receiver != null);
            int arity;
            LookupOptions options;
            if (typeArguments.IsDefault) {
                arity = 0;
                options = LookupOptions.AllMethodsOnArityZero;
            } else {
                arity = typeArguments.Length;
                options = LookupOptions.Default;
            }

            binder = binder.WithAdditionalFlags(BinderFlags.SemanticModel);
            foreach (var scope in new ExtensionMethodScopes(binder)) {
                var extensionMethods = ArrayBuilder<MethodSymbol>.GetInstance();
                var otherBinder = scope.Binder;
                otherBinder.GetCandidateExtensionMethods(extensionMethods,
                                                         name,
                                                         arity,
                                                         options,
                                                         originalBinder: binder);

                foreach (var method in extensionMethods) {
                    var discardedUseSiteInfo = CompoundUseSiteInfo<AssemblySymbol>.Discarded;
                    MergeReducedAndFilteredMethodGroupSymbol(
                        methods,
                        filteredMethods,
                        binder.CheckViability(method, arity, options, accessThroughType: null, diagnose: false, useSiteInfo: ref discardedUseSiteInfo),
                        typeArguments,
                        receiver.Type,
                        ref resultKind,
                        binder.Compilation);
                }

                extensionMethods.Free();
            }
        }

        methods.Free();
        return filteredMethods.ToImmutableAndFree();
    }

    // Reduce extension methods to their reduced form, and remove:
    //   a) Extension methods are aren't applicable to receiverType
    //   including constraint checking.
    //   b) Duplicate methods
    //   c) Methods that are hidden or overridden by another method in the group.
    private static bool AddReducedAndFilteredMethodGroupSymbol(
        ArrayBuilder<MethodSymbol> methods,
        ArrayBuilder<MethodSymbol> filteredMethods,
        MethodSymbol method,
        ImmutableArray<TypeWithAnnotations> typeArguments,
        TypeSymbol receiverType,
        CSharpCompilation compilation) {
        MethodSymbol constructedMethod;
        if (!typeArguments.IsDefaultOrEmpty && method.Arity == typeArguments.Length) {
            constructedMethod = method.Construct(typeArguments);
            Debug.Assert((object)constructedMethod != null);
        } else {
            constructedMethod = method;
        }

        if ((object)receiverType != null) {
            constructedMethod = constructedMethod.ReduceExtensionMethod(receiverType, compilation);
            if ((object)constructedMethod == null) {
                return false;
            }
        }

        // Don't add exact duplicates.
        if (filteredMethods.Contains(constructedMethod)) {
            return false;
        }

        methods.Add(method);
        filteredMethods.Add(constructedMethod);
        return true;
    }

    private static void MergeReducedAndFilteredMethodGroupSymbol(
        ArrayBuilder<MethodSymbol> methods,
        ArrayBuilder<MethodSymbol> filteredMethods,
        SingleLookupResult singleResult,
        ImmutableArray<TypeWithAnnotations> typeArguments,
        TypeSymbol receiverType,
        ref LookupResultKind resultKind,
        CSharpCompilation compilation) {
        if (singleResult.Symbol is null) {
            return;
        }

        Debug.Assert(singleResult.Symbol.Kind == SymbolKind.Method);

        var singleKind = singleResult.Kind;
        if (resultKind > singleKind) {
            return;
        } else if (resultKind < singleKind) {
            methods.Clear();
            filteredMethods.Clear();
            resultKind = LookupResultKind.Empty;
        }

        var method = (MethodSymbol)singleResult.Symbol;
        if (AddReducedAndFilteredMethodGroupSymbol(methods, filteredMethods, method, typeArguments, receiverType, compilation)) {
            Debug.Assert(methods.Count > 0);
            if (resultKind < singleKind) {
                resultKind = singleKind;
            }
        }

        Debug.Assert((methods.Count == 0) == (resultKind == LookupResultKind.Empty));
        Debug.Assert(methods.Count == filteredMethods.Count);
    }

    private static OneOrMany<MethodSymbol> CreateReducedExtensionMethodsFromOriginalsIfNecessary(BoundCall call, CSharpCompilation compilation) {
        var methods = call.OriginalMethodsOpt;
        TypeSymbol extensionThisType = null;
        Debug.Assert(!methods.IsDefault);

        if (call.InvokedAsExtensionMethod) {
            // If the call was invoked as an extension method, the receiver
            // should be non-null and all methods should be extension methods.
            if (call.ReceiverOpt != null) {
                extensionThisType = call.ReceiverOpt.Type;
            } else {
                extensionThisType = call.Arguments[0].Type;
            }

            Debug.Assert((object)extensionThisType != null);
        }

        var methodBuilder = ArrayBuilder<MethodSymbol>.GetInstance();
        var filteredMethodBuilder = ArrayBuilder<MethodSymbol>.GetInstance();
        foreach (var method in FilterOverriddenOrHiddenMethods(methods)) {
            AddReducedAndFilteredMethodGroupSymbol(methodBuilder, filteredMethodBuilder, method, default(ImmutableArray<TypeWithAnnotations>), extensionThisType, compilation);
        }
        methodBuilder.Free();
        return filteredMethodBuilder.ToOneOrManyAndFree();
    }

    private OneOrMany<Symbol> CreateReducedExtensionMethodIfPossible(BoundCall call) {
        var method = call.Method;
        Debug.Assert((object)method != null);

        if (call.InvokedAsExtensionMethod && method.IsExtensionMethod && method.MethodKind != MethodKind.ReducedExtension) {
            Debug.Assert(call.Arguments.Length > 0);
            BoundExpression receiver = call.Arguments[0];
            MethodSymbol reduced = method.ReduceExtensionMethod(receiver.Type, Compilation);
            // If the extension method can't be applied to the receiver of the given
            // type, we should also return the original call method.
            method = reduced ?? method;
        }
        return OneOrMany.Create<Symbol>(method);
    }

    private OneOrMany<Symbol> CreateReducedExtensionMethodIfPossible(BoundDelegateCreationExpression delegateCreation, BoundExpression receiverOpt) {
        var method = delegateCreation.MethodOpt;
        Debug.Assert((object)method != null);

        if (delegateCreation.IsExtensionMethod && method.IsExtensionMethod && (receiverOpt != null)) {
            MethodSymbol reduced = method.ReduceExtensionMethod(receiverOpt.Type, Compilation);
            method = reduced ?? method;
        }
        return OneOrMany.Create<Symbol>(method);
    }

    internal PreprocessingSymbolInfo GetPreprocessingSymbolInfo(IdentifierNameSyntax node) {
        CheckSyntaxNode(node);

        if (node.Ancestors().Any(n => SyntaxFacts.IsPreprocessorDirective(n.kind))) {
            bool isDefined = this.syntaxTree.IsPreprocessorSymbolDefined(node.Identifier.ValueText, node.Identifier.SpanStart);
            return new PreprocessingSymbolInfo(new Symbols.PublicModel.PreprocessingSymbol(node.Identifier.ValueText), isDefined);
        }

        return PreprocessingSymbolInfo.None;
    }

    [Flags]
    internal enum SymbolInfoOptions {
        PreferTypeToConstructors = 0x1,
        PreferConstructorsToType = 0x2,
        ResolveAliases = 0x4,
        PreserveAliases = 0x8,

        DefaultOptions = PreferConstructorsToType | ResolveAliases
    }

    internal ISymbol GetEnclosingSymbol(int position) {
        position = CheckAndAdjustPosition(position);
        var binder = GetEnclosingBinder(position);
        return binder is null ? null : binder.containingMember;
    }

    private protected sealed override Compilation _compilationCore => compilation;

    private protected sealed override SemanticModel _parentModelCore => parentModel;

    private protected sealed override SyntaxTree _syntaxTreeCore => syntaxTree;

    private protected sealed override SyntaxNode _rootCore => root;

    private SymbolInfo GetSymbolInfoFromNode(SyntaxNode node, CancellationToken cancellationToken) {
        switch (node) {
            case null:
                throw new ArgumentNullException(nameof(node));
            case ExpressionSyntax expression:
                return GetSymbolInfo(expression, cancellationToken);
            case ConstructorInitializerSyntax initializer:
                return GetSymbolInfo(initializer, cancellationToken);
            case PrimaryConstructorBaseTypeSyntax initializer:
                return GetSymbolInfo(initializer, cancellationToken);
            case AttributeSyntax attribute:
                return GetSymbolInfo(attribute, cancellationToken);
            case CrefSyntax cref:
                return GetSymbolInfo(cref, cancellationToken);
            case SelectOrGroupClauseSyntax selectOrGroupClause:
                return GetSymbolInfo(selectOrGroupClause, cancellationToken);
            case OrderingSyntax orderingSyntax:
                return GetSymbolInfo(orderingSyntax, cancellationToken);
            case PositionalPatternClauseSyntax ppcSyntax:
                return GetSymbolInfo(ppcSyntax, cancellationToken);
        }

        return SymbolInfo.None;
    }

    private TypeInfo GetTypeInfoFromNode(SyntaxNode node, CancellationToken cancellationToken) {
        switch (node) {
            case null:
                throw new ArgumentNullException(nameof(node));
            case ExpressionSyntax expression:
                return GetTypeInfo(expression, cancellationToken);
            case ConstructorInitializerSyntax initializer:
                return GetTypeInfo(initializer, cancellationToken);
            case AttributeSyntax attribute:
                return GetTypeInfo(attribute, cancellationToken);
        }

        return BelteTypeInfo.None;
    }

    private ImmutableArray<ISymbol> GetMemberGroupFromNode(SyntaxNode node, CancellationToken cancellationToken) {
        switch (node) {
            case null:
                throw new ArgumentNullException(nameof(node));
            case ExpressionSyntax expression:
                return this.GetMemberGroup(expression, cancellationToken);
            case ConstructorInitializerSyntax initializer:
                return this.GetMemberGroup(initializer, cancellationToken);
            case AttributeSyntax attribute:
                return this.GetMemberGroup(attribute, cancellationToken);
        }

        return ImmutableArray<ISymbol>.Empty;
    }

    private protected sealed override ImmutableArray<ISymbol> GetMemberGroupCore(SyntaxNode node, CancellationToken cancellationToken) {
        var methodGroup = this.GetMemberGroupFromNode(node, cancellationToken);
        return StaticCast<ISymbol>.From(methodGroup);
    }

    private protected sealed override SymbolInfo GetSpeculativeSymbolInfoCore(
        int position,
        SyntaxNode node,
        SpeculativeBindingOption bindingOption) {
        switch (node) {
            case ExpressionSyntax expression:
                return GetSpeculativeSymbolInfo(position, expression, bindingOption);
            case ConstructorInitializerSyntax initializer:
                return GetSpeculativeSymbolInfo(position, initializer);
            case AttributeSyntax attribute:
                return GetSpeculativeSymbolInfo(position, attribute);
        }

        return SymbolInfo.None;
    }

    private protected sealed override TypeInfo GetSpeculativeTypeInfoCore(
        int position,
        SyntaxNode node,
        SpeculativeBindingOption bindingOption) {
        return node is ExpressionSyntax expression
            ? GetSpeculativeTypeInfo(position, expression, bindingOption)
            : BelteTypeInfo.None;
    }

    private protected sealed override IAliasSymbol GetSpeculativeAliasInfoCore(
        int position,
        SyntaxNode nameSyntax,
        SpeculativeBindingOption bindingOption) {
        return nameSyntax is IdentifierNameSyntax identifier
            ? GetSpeculativeAliasInfo(position, identifier, bindingOption)
            : null;
    }

    private protected sealed override SymbolInfo GetSymbolInfoCore(
        SyntaxNode node,
        CancellationToken cancellationToken) {
        return GetSymbolInfoFromNode(node, cancellationToken);
    }

    private protected sealed override TypeInfo GetTypeInfoCore(SyntaxNode node, CancellationToken cancellationToken) {
        return GetTypeInfoFromNode(node, cancellationToken);
    }

    private protected sealed override IAliasSymbol GetAliasInfoCore(
        SyntaxNode node,
        CancellationToken cancellationToken) {
        return node is IdentifierNameSyntax nameSyntax ? GetAliasInfo(nameSyntax, cancellationToken) : null;
    }

    private protected sealed override PreprocessingSymbolInfo GetPreprocessingSymbolInfoCore(SyntaxNode node) {
        return node is IdentifierNameSyntax nameSyntax
            ? GetPreprocessingSymbolInfo(nameSyntax)
            : PreprocessingSymbolInfo.None;
    }

    private protected sealed override ISymbol GetDeclaredSymbolCore(
        SyntaxNode node,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        switch (node) {
            case TypeDeclarationSyntax type:
                return GetDeclaredSymbol(type, cancellationToken);
            case MemberDeclarationSyntax member:
                return GetDeclaredSymbol(member, cancellationToken);
        }

        switch (node.kind) {
            case SyntaxKind.LocalFunctionStatement:
                return GetDeclaredSymbol((LocalFunctionStatementSyntax)node, cancellationToken);
            case SyntaxKind.CaseSwitchLabel:
            case SyntaxKind.DefaultSwitchLabel:
                return GetDeclaredSymbol((SwitchLabelSyntax)node, cancellationToken);
            case SyntaxKind.Argument:
                return GetDeclaredSymbol((ArgumentSyntax)node, cancellationToken);
            case SyntaxKind.VariableDeclaration:
                return GetDeclaredSymbol((VariableDeclarationSyntax)node, cancellationToken);
            case SyntaxKind.IdentifierToken:
                return GetDeclaredSymbol((SyntaxToken)node, cancellationToken);
            case SyntaxKind.NamespaceDeclaration:
                return GetDeclaredSymbol((NamespaceDeclarationSyntax)node, cancellationToken);
            case SyntaxKind.FileScopedNamespaceDeclaration:
                return GetDeclaredSymbol((FileScopedNamespaceDeclarationSyntax)node, cancellationToken);
            case SyntaxKind.Parameter:
                return GetDeclaredSymbol((ParameterSyntax)node, cancellationToken);
            case SyntaxKind.UsingDirective:
                var usingDirective = (UsingDirectiveSyntax)node;

                if (usingDirective.alias is null)
                    break;

                return GetDeclaredSymbol(usingDirective, cancellationToken);
            case SyntaxKind.ForEachStatement:
                return GetDeclaredSymbol((ForEachStatementSyntax)node);
            case SyntaxKind.CompilationUnit:
                return GetDeclaredSymbol((CompilationUnitSyntax)node, cancellationToken);
        }

        return null;
    }

    private protected sealed override ImmutableArray<ISymbol> GetDeclaredSymbolsCore(
        SyntaxNode declaration,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();

        if (declaration is FieldDeclarationSyntax field)
            return this.GetDeclaredSymbols(field, cancellationToken);

        if (declaration is TypeDeclarationSyntax typeDeclaration) {
            var namedType = GetDeclaredSymbol(typeDeclaration, cancellationToken);
            return [namedType];
        }

        var symbol = GetDeclaredSymbolCore(declaration, cancellationToken);
        return symbol is not null
            ? ImmutableArray.Create(symbol)
            : ImmutableArray<ISymbol>.Empty;
    }

    internal override void ComputeDeclarationsInSpan(
        TextSpan span,
        bool getSymbol,
        ArrayBuilder<DeclarationInfo> builder,
        CancellationToken cancellationToken) {
        CSharpDeclarationComputer.ComputeDeclarationsInSpan(this, span, getSymbol, builder, cancellationToken);
    }

    internal override void ComputeDeclarationsInNode(
        SyntaxNode node,
        ISymbol associatedSymbol,
        bool getSymbol,
        ArrayBuilder<DeclarationInfo> builder,
        CancellationToken cancellationToken,
        int? levelsToCompute = null) {
        CSharpDeclarationComputer.ComputeDeclarationsInNode(
            this,
            associatedSymbol,
            node,
            getSymbol,
            builder,
            cancellationToken,
            levelsToCompute
        );
    }

    internal abstract override Func<SyntaxNode, bool> GetSyntaxNodesToAnalyzeFilter(
        SyntaxNode declaredNode,
        ISymbol declaredSymbol);

    internal abstract override bool ShouldSkipSyntaxNodeAnalysis(SyntaxNode node, ISymbol containingSymbol);

    protected internal override SyntaxNode GetTopmostNodeForDiagnosticAnalysis(
        ISymbol symbol,
        SyntaxNode declaringSyntax) {
        switch (symbol.Kind) {
            case SymbolKind.Field:
                var fieldDecl = declaringSyntax.FirstAncestorOrSelf<BaseFieldDeclarationSyntax>();

                if (fieldDecl is not null)
                    return fieldDecl;

                break;
        }

        return declaringSyntax;
    }

    private protected sealed override ImmutableArray<ISymbol> LookupSymbolsCore(
        int position,
        INamespaceOrTypeSymbol container,
        string name,
        bool includeReducedExtensionMethods) {
        return LookupSymbols(
            position,
            container.EnsureCSharpSymbolOrNull(nameof(container)),
            name,
            includeReducedExtensionMethods
        );
    }

    private protected sealed override ImmutableArray<ISymbol> LookupBaseMembersCore(int position, string name) {
        return LookupBaseMembers(position, name);
    }

    private protected sealed override ImmutableArray<ISymbol> LookupStaticMembersCore(
        int position,
        INamespaceOrTypeSymbol container,
        string name) {
        return LookupStaticMembers(position, container.EnsureCSharpSymbolOrNull(nameof(container)), name);
    }

    private protected sealed override ImmutableArray<ISymbol> LookupNamespacesAndTypesCore(
        int position,
        INamespaceOrTypeSymbol container,
        string name) {
        return LookupNamespacesAndTypes(position, container.EnsureCSharpSymbolOrNull(nameof(container)), name);
    }

    private protected sealed override ImmutableArray<ISymbol> LookupLabelsCore(int position, string name) {
        return LookupLabels(position, name);
    }

    private protected sealed override Optional<object> GetConstantValueCore(
        SyntaxNode node,
        CancellationToken cancellationToken) {
        if (node is null)
            throw new ArgumentNullException(nameof(node));

        return node is ExpressionSyntax expression
            ? GetConstantValue(expression, cancellationToken)
            : default;
    }

    private protected sealed override ISymbol GetEnclosingSymbolCore(
        int position,
        CancellationToken cancellationToken) {
        return this.GetEnclosingSymbol(position);
    }

    private protected sealed override bool IsAccessibleCore(int position, ISymbol symbol) {
        return this.IsAccessible(position, symbol.EnsureCSharpSymbolOrNull(nameof(symbol)));
    }
    */
}
